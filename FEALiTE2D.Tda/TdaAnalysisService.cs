using System;
using System.Collections.Generic;
using System.Linq;
using FEALiTE2D.Api.Contracts.Enums;
using FEALiTE2D.Api.Contracts.Mapping;
using FEALiTE2D.Api.Contracts.Requests;
using FEALiTE2D.Api.Contracts.Responses;
using FEALiTE2D.Api.Contracts.Validation;

namespace FEALiTE2D.Tda;

/// <summary>
/// Analysis of an <see cref="AnalysisRequest"/> by TDA.
/// <list type="bullet">
/// <item>Without <see cref="AnalysisRequest.TimeDependent"/>: linear elastic analysis of every load case (one TDA run
/// each, one time node, non-creeping materials) → <see cref="AnalysisResponse.LoadCaseResults"/> and combinations, the
/// same numbers as FEALiTE2D up to the single precision of TDA.</item>
/// <item>With it: one TDA run over the construction stages → <see cref="AnalysisResponse.TimeDependentResults"/>.</item>
/// </list>
/// Units of the request are SI (N, m, Pa); TDA runs in kN, m, kPa.
/// </summary>
public sealed class TdaAnalysisService
{
    /// <summary>As = factor · A: TDA elements include shear deformation, FEALiTE2D is Euler–Bernoulli.</summary>
    public const double ShearAreaFactor = 1e6;

    /// <summary>Request units (N) → TDA units (kN).</summary>
    private const double K = 1000.0;

    private readonly ITdaSolver _solver;

    public TdaAnalysisService(ITdaSolver solver) => _solver = solver;

    public AnalysisResponse Analyze(AnalysisRequest request)
    {
        var validation = new AnalysisRequestValidator().Validate(request);
        if (!validation.IsValid) return AnalysisResponse.Failed(validation.Errors.ToArray());
        try
        {
            var model = new Model(request);
            return request.TimeDependent is null ? Elastic(model) : TimeDependent(model);
        }
        catch (TdaException e)
        {
            return AnalysisResponse.Failed($"TDA error {e.Code}: {e.Message}");
        }
        catch (NotSupportedException e)
        {
            return AnalysisResponse.Failed(e.Message);
        }
    }

    // =================================================================================== elastic analysis

    private AnalysisResponse Elastic(Model m)
    {
        var response = new AnalysisResponse();
        foreach (var lc in m.Request.LoadCases)
        {
            var input = new TdaInput { NodeCount = m.Nodes.Count, Ifi = 12 };
            input.Times.Add(0);
            var ev = new TdaEvent();
            input.Events.Add(ev);
            foreach (var mem in m.Members)
            {
                input.Elements.Add((mem.StartNode + 1, mem.EndNode + 1, input.AddGeometry(m.Geometry(mem)),
                                    input.AddPhysical(m.SteelFk(mem))));
                ev.Install.Add(mem.Index + 1);
            }
            var prescribed = lc.SupportDisplacements.ToDictionary(s => s.NodeLabel, s => new[] { s.Ux, s.Uy, s.Rz });
            var restraints = new bool[m.Nodes.Count][];
            for (int n = 0; n < m.Nodes.Count; n++)
            {
                restraints[n] = m.RequestRestraints(n);
                var v = prescribed.GetValueOrDefault(m.Nodes[n].Label) ?? new double[3];
                for (int d = 0; d < 3; d++)
                    if (restraints[n][d]) ev.InstallSupports[d].Add((n + 1, ToTda(v[d], (TdaDof)d, isLoad: false)));
            }
            var loads = m.ElementLoads(lc);
            m.AddLoads(ev, lc, loads);
            var json = TdaJson.Parse(_solver.Run(input.ToZa()));
            var snap = json.Steps[^1].Totals;
            response.LoadCaseResults[lc.Label] = m.ToCaseResult(snap, loads, restraints, m.Members);
        }
        foreach (var combo in m.Request.LoadCombinations)
            response.LoadCombinationResults[combo.Label] = Combine(combo.Factors.Select(kv => (response.LoadCaseResults[kv.Key], kv.Value)));
        return response;
    }

    // ============================================================================ time-dependent analysis

    private AnalysisResponse TimeDependent(Model m)
    {
        var plan = new StagePlan(m);
        var json = TdaJson.Parse(_solver.Run(plan.Input.ToZa()));
        var results = new TimeDependentResultsDto { RheologyModel = m.Td!.RheologyModel };
        results.Warnings.AddRange(plan.Warnings);

        // steps by time node: change step (if any) and the last step
        var byNode = json.Steps.GroupBy(s => s.TimeNode).ToDictionary(g => g.Key, g => g.ToList());
        TdaSnapshot TotalsBefore(int node)
        {
            var steps = byNode.GetValueOrDefault(node);
            var change = steps?.FindIndex(s => s.Kind == "change") ?? -1;
            if (steps is null) return TdaSnapshot.Zero(m);
            if (change > 0) return steps[change - 1].Totals;
            if (change == 0) return node > 0 ? Last(node - 1) : TdaSnapshot.Zero(m);
            return steps[^1].Totals;
        }
        TdaSnapshot Last(int node)
        {
            for (int k = node; k >= 0; k--)
                if (byNode.TryGetValue(k, out var s)) return s[^1].Totals;
            return TdaSnapshot.Zero(m);
        }
        TdaStep? ChangeStep(int node) => byNode.GetValueOrDefault(node)?.FirstOrDefault(s => s.Kind == "change");

        // ---- states
        var stageNodes = new HashSet<int>();
        foreach (var st in plan.Stages)
        {
            int node = plan.NodeOf(st.TdaTime);
            stageNodes.Add(node);
            results.States.Add(plan.State(st.TdaTime, TimeStateKindDto.BeforeStage, TotalsBefore(node), before: true));
            results.States.Add(plan.State(st.TdaTime, TimeStateKindDto.AfterStage, Last(node), before: false));
        }
        foreach (double t in plan.OutputTimes)
        {
            var kind = t == plan.EndTime ? TimeStateKindDto.End : TimeStateKindDto.Output;
            if (plan.Stages.Any(s => s.TdaTime == t) && kind == TimeStateKindDto.Output) continue;
            results.States.Add(plan.State(t, kind, Last(plan.NodeOf(t)), before: false));
        }
        results.States = results.States.OrderBy(s => s.Time).ThenBy(s => s.Kind == TimeStateKindDto.BeforeStage ? 0 : 1).ToList();

        // ---- effects: stage change steps + creep and shrinkage between reported times
        var boundaries = plan.Stages.Select(s => s.TdaTime).Concat(plan.OutputTimes).Distinct().OrderBy(t => t).ToList();
        foreach (var st in plan.Stages)
        {
            var step = ChangeStep(plan.NodeOf(st.TdaTime));
            var inc = step?.Increments ?? TdaSnapshot.Zero(m);
            results.Effects.Add(new EffectDto
            {
                Label = st.Dto.Label, Kind = EffectKindDto.Stage, Stage = st.Dto.Label,
                TimeFrom = plan.ToRequestTime(st.TdaTime), TimeTo = plan.ToRequestTime(st.TdaTime),
                Result = plan.CaseResult(inc, plan.LoadsOfStage(st), st.TdaTime, before: false)
            });
        }
        for (int i = 0; i + 1 < boundaries.Count; i++)
        {
            double ta = boundaries[i], tb = boundaries[i + 1];
            var steps = json.Steps.Where(s => s.Time > ta + 1e-9 && s.Time <= tb + 1e-9
                                              && !(s.Kind == "change" && stageNodes.Contains(s.TimeNode))).ToList();
            if (steps.Count == 0) continue;
            var sum = TdaSnapshot.Zero(m);
            foreach (var s in steps) sum = sum.Add(s.Increments);
            results.Effects.Add(new EffectDto
            {
                Label = $"CS {Fmt(plan.ToRequestTime(ta))}–{Fmt(plan.ToRequestTime(tb))} d",
                Kind = EffectKindDto.CreepShrinkage,
                TimeFrom = plan.ToRequestTime(ta), TimeTo = plan.ToRequestTime(tb),
                Result = plan.CaseResult(sum, Array.Empty<(FrameMember, ElementLoadDto)>(), tb, before: false)
            });
        }
        results.Effects = results.Effects.OrderBy(e => e.TimeTo).ThenBy(e => e.Kind == EffectKindDto.CreepShrinkage ? 0 : 1).ToList();

        // ---- histories (last step of every time node)
        results.Histories = plan.Histories(byNode.OrderBy(kv => kv.Key).Select(kv => kv.Value[^1]).ToList());
        return new AnalysisResponse { TimeDependentResults = results };
    }

    private static string Fmt(double t) => t.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    internal static double ToTda(double v, TdaDof dof, bool isLoad)
    {
        double s = dof == TdaDof.W ? -v : v;
        return isLoad ? s / K : s;
    }

    private static CaseResultDto Combine(IEnumerable<(CaseResultDto Result, double Factor)> parts)
    {
        var list = parts.ToList();
        var first = list[0].Result;
        var r = new CaseResultDto();
        foreach (var (label, _) in first.NodeDisplacements)
            r.NodeDisplacements[label] = new DisplacementDto
            {
                Ux = list.Sum(p => p.Factor * p.Result.NodeDisplacements[label].Ux),
                Uy = list.Sum(p => p.Factor * p.Result.NodeDisplacements[label].Uy),
                Rz = list.Sum(p => p.Factor * p.Result.NodeDisplacements[label].Rz)
            };
        foreach (var (label, _) in first.SupportReactions)
            r.SupportReactions[label] = new ForceDto
            {
                Fx = list.Sum(p => p.Factor * p.Result.SupportReactions[label].Fx),
                Fy = list.Sum(p => p.Factor * p.Result.SupportReactions[label].Fy),
                Mz = list.Sum(p => p.Factor * p.Result.SupportReactions[label].Mz)
            };
        foreach (var (label, segs) in first.ElementForces)
        {
            r.ElementForces[label] = segs.Select((s, i) => new SegmentDto
            {
                X1 = s.X1, X2 = s.X2,
                StartForce = Sum(list.Select(p => (p.Result.ElementForces[label][i].StartForce, p.Factor))),
                EndForce = Sum(list.Select(p => (p.Result.ElementForces[label][i].EndForce, p.Factor)))
            }).ToList();
        }
        return r;
    }

    internal static ForceDto Sum(IEnumerable<(ForceDto F, double Factor)> parts)
    {
        var f = new ForceDto();
        foreach (var (p, k) in parts)
        {
            f.Fx += k * p.Fx;
            f.Fy += k * p.Fy;
            f.Mz += k * p.Mz;
        }
        return f;
    }

    // ===================================================================================== model

    /// <summary>The request reduced to nodes and frame members (request elements + additional elements).</summary>
    internal sealed class Model
    {
        public Model(AnalysisRequest request)
        {
            Request = request;
            Td = request.TimeDependent;
            Nodes = request.Nodes;
            foreach (var n in Nodes)
            {
                if (n.Support is SpringSupportDto) throw new NotSupportedException($"TDA: node '{n.Label}' has a spring support (not supported).");
                if (n.RotationAngle != 0) throw new NotSupportedException($"TDA: node '{n.Label}' has a rotated support (not supported).");
            }
            NodeIndex = Nodes.Select((n, i) => (n.Label, i)).ToDictionary(x => x.Label, x => x.i);
            var builder = new StructureBuilder();
            builder.Build(request);
            var materials = request.Materials.ToDictionary(x => x.Label);
            var sectionMaterial = request.Sections.ToDictionary(s => s.Label, s => materials[s.MaterialLabel]);
            var frames = request.Elements.Select(e => (e, false))
                .Concat((Td?.AdditionalElements ?? new()).Select(e => ((ElementDto)e, true)));
            foreach (var (dto, additional) in frames)
            {
                if (dto is not FrameElementDto f) throw new NotSupportedException($"TDA: element '{dto.Label}' is a spring element (not supported).");
                var sec = builder.Sections[f.SectionLabel];
                int s = NodeIndex[f.StartNodeLabel], e = NodeIndex[f.EndNodeLabel];
                var mem = new FrameMember(f.Label, Members.Count, s, e, Nodes[s], Nodes[e], sec.A, sec.Iz,
                                          sectionMaterial[f.SectionLabel], f.EndRelease, additional)
                { Section = f.SectionLabel };
                Members.Add(mem);
            }
            MemberIndex = Members.ToDictionary(x => x.Label);
            foreach (var age in Td?.Elements ?? new())
            {
                var mem = MemberIndex[age.ElementLabel];
                mem.OffsetStart = age.OffsetStart;
                mem.OffsetEnd = age.OffsetEnd;
            }
            // parts of composite members: additional elements between the same nodes as a request element
            foreach (var add in Members.Where(x => x.IsAdditional))
            {
                var host = Members.FirstOrDefault(h => !h.IsAdditional && h.StartNode == add.StartNode && h.EndNode == add.EndNode);
                if (host is null) continue;
                add.Host = host;
                host.Parts.Add(add);
            }
            MeshSegments = request.Settings?.MeshSegments ?? 10;
        }

        public AnalysisRequest Request { get; }
        public TimeDependentDto? Td { get; }
        public List<NodeDto> Nodes { get; }
        public Dictionary<string, int> NodeIndex { get; }
        public List<FrameMember> Members { get; } = new();
        public Dictionary<string, FrameMember> MemberIndex { get; }
        public int MeshSegments { get; }

        public bool[] RequestRestraints(int node) =>
            Nodes[node].Support is RigidSupportDto r ? new[] { r.Ux, r.Uy, r.Rz } : new bool[3];

        public TdaGeometryType Geometry(FrameMember mem) =>
            new(mem.Dx, -mem.Dy, -mem.OffsetStart, -mem.OffsetEnd);

        /// <summary>FK of a non-creeping member (steel, or any material in the elastic analysis).</summary>
        public double[] SteelFk(FrameMember mem)
        {
            var fk = BaseFk(mem);
            fk[7] = -1;
            return fk;
        }

        public double[] BaseFk(FrameMember mem)
        {
            var fk = new double[40];
            fk[0] = mem.A;
            fk[1] = ShearAreaFactor * mem.A;
            fk[2] = mem.Iz;
            fk[3] = mem.Release switch
            {
                EndReleaseDto.StartRelease => 10, EndReleaseDto.EndRelease => 1, EndReleaseDto.FullRelease => 11, _ => 0
            };
            double nu = mem.Material.U > 0 ? mem.Material.U : 0.3;
            fk[4] = mem.Material.E / K;
            fk[5] = mem.Material.E / (2 * (1 + nu)) / K;
            fk[6] = mem.Material.Alpha > 0 ? mem.Material.Alpha : 1e-5;
            return fk;
        }

        /// <summary>Element loads of a load case by member (load cases act on request elements).</summary>
        public List<(FrameMember Member, ElementLoadDto Load)> ElementLoads(LoadCaseDto lc) =>
            lc.ElementLoads.Select(l => (MemberIndex[l.ElementLabel], l)).ToList();

        /// <summary>Nodal loads + consistent nodal loads of the element loads of a load case into an event.</summary>
        public void AddLoads(TdaEvent ev, LoadCaseDto lc, List<(FrameMember Member, ElementLoadDto Load)> loads)
        {
            foreach (var nl in lc.NodalLoads)
            {
                int n = NodeIndex[nl.NodeLabel] + 1;
                ev.AddLoad(n, TdaDof.U, ToTda(nl.Fx, TdaDof.U, true));
                ev.AddLoad(n, TdaDof.W, ToTda(nl.Fy, TdaDof.W, true));
                ev.AddLoad(n, TdaDof.FI, ToTda(nl.Mz, TdaDof.FI, true));
            }
            foreach (var g in loads.GroupBy(x => x.Member))
            {
                var f = g.Key.FixedEndForces(g.Select(x => x.Load));
                foreach (var (off, node) in new[] { (0, g.Key.StartNode), (3, g.Key.EndNode) })
                    for (int d = 0; d < 3; d++)
                        ev.AddLoad(node + 1, (TdaDof)d, ToTda(-f[off + d], (TdaDof)d, true));
            }
        }

        /// <summary>
        /// TDA snapshot → <see cref="CaseResultDto"/>: displacements, reactions of the restrained dofs, segment forces of
        /// the members (TDA end forces + fixed-end forces of <paramref name="loads"/>), resultants of composite members.
        /// </summary>
        public CaseResultDto ToCaseResult(TdaSnapshot snap, IReadOnlyCollection<(FrameMember Member, ElementLoadDto Load)> loads,
                                          bool[][] restraints, IEnumerable<FrameMember> active,
                                          Func<FrameMember, IReadOnlyList<double>>? mesh = null)
        {
            var r = new CaseResultDto();
            for (int n = 0; n < Nodes.Count; n++)
            {
                var d = snap.Displacements[n];
                r.NodeDisplacements[Nodes[n].Label] = new DisplacementDto { Ux = d[0], Uy = -d[1], Rz = d[2] };
                if (restraints[n].Any(x => x))
                {
                    var re = snap.Reactions[n];
                    r.SupportReactions[Nodes[n].Label] = new ForceDto
                    {
                        Fx = restraints[n][0] ? -re[0] * K : 0,
                        Fy = restraints[n][1] ? re[1] * K : 0,
                        Mz = restraints[n][2] ? -re[2] * K : 0
                    };
                }
            }
            var activeSet = active.ToHashSet();
            var byMember = loads.GroupBy(x => x.Member).ToDictionary(g => g.Key, g => g.Select(x => x.Load).ToList());
            List<SegmentDto> Own(FrameMember mem, IReadOnlyList<double> pts)
            {
                var memLoads = byMember.GetValueOrDefault(mem) ?? new List<ElementLoadDto>();
                return mem.Segments(StartForce(mem, snap, memLoads), memLoads, pts);
            }
            foreach (var mem in Members.Where(x => activeSet.Contains(x) && x.Host is null))
            {
                var pts = mesh?.Invoke(mem) ?? mem.MeshPoints(byMember.GetValueOrDefault(mem) ?? new(), MeshSegments);
                var own = Own(mem, pts);
                var parts = mem.Parts.Where(activeSet.Contains).ToList();
                if (mem.Parts.Count == 0)
                {
                    r.ElementForces[mem.Label] = own;
                    continue;
                }
                var partForces = new Dictionary<string, List<SegmentDto>> { [mem.Label] = own };
                foreach (var p in parts) partForces[p.Label] = Own(p, pts);
                r.PartForces ??= new();
                r.PartForces[mem.Label] = partForces;
                r.ElementForces[mem.Label] = own.Select((s, i) => new SegmentDto
                {
                    X1 = s.X1, X2 = s.X2,
                    StartForce = Resultant(partForces, i, s.X1, start: true),
                    EndForce = Resultant(partForces, i, s.X2, start: false)
                }).ToList();
            }
            return r;
        }

        private ForceDto Resultant(Dictionary<string, List<SegmentDto>> parts, int i, double x, bool start)
        {
            var f = new ForceDto();
            foreach (var (label, segs) in parts)
            {
                var p = start ? segs[i].StartForce : segs[i].EndForce;
                double off = MemberIndex[label].OffsetAt(x);
                f.Fx += p.Fx;
                f.Fy += p.Fy;
                f.Mz += p.Mz - off * p.Fx;
            }
            return f;
        }

        /// <summary>Local start forces (fx = −N, fy = V, mz = −M) = TDA end forces + fixed-end forces.</summary>
        public double[] StartForce(FrameMember mem, TdaSnapshot snap, IReadOnlyCollection<ElementLoadDto> loads)
        {
            var e = snap.Forces[mem.Index];   // N0 NL M0 ML T
            var f = loads.Count > 0 ? mem.FixedEndForces(loads) : new double[6];
            var (fx, fy) = mem.ToLocal(f[0], f[1]);
            return new[] { -e[0] * K + fx, e[4] * K + fy, -e[2] * K + f[2] };
        }

        /// <summary>Engineering end forces (N0, NL, M0, ML, V0) = TDA end forces + fixed-end forces.</summary>
        public double[] EndForces(FrameMember mem, TdaSnapshot snap, IReadOnlyCollection<ElementLoadDto> loads)
        {
            var e = snap.Forces[mem.Index];
            var f = loads.Count > 0 ? mem.FixedEndForces(loads) : new double[6];
            var s = mem.ToLocal(f[0], f[1]);
            var t = mem.ToLocal(f[3], f[4]);
            return new[] { e[0] * K - s.X, e[1] * K + t.X, e[2] * K - f[2], e[3] * K + f[5], e[4] * K + s.Y };
        }
    }
}
