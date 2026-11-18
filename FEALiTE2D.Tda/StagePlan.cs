using System;
using System.Collections.Generic;
using System.Linq;
using FEALiTE2D.Api.Contracts.Enums;
using FEALiTE2D.Api.Contracts.Requests;
using FEALiTE2D.Api.Contracts.Responses;

namespace FEALiTE2D.Tda;

/// <summary>
/// <see cref="TimeDependentDto"/> → TDA input over time, and the bookkeeping to read the results back (which
/// supports, elements and loads exist at a time).
/// <list type="bullet">
/// <item>Times are shifted so that the earliest stage / casting is TDA time 0; stages at the same time get +0.01 d
/// each (one TDA change step per stage, so the effect of every stage stays separate).</item>
/// <item>Concrete element: created in TDA at its casting time (age from casting), included in the stiffness at
/// its activating stage (Dt2), shrinkage from casting + curing (Dt3); h0 from the section drying data.
/// At the moment of casting the concrete has no stiffness: a stage casting and loading concrete at the same time
/// must hold its nodes (supports of the formwork / falsework).</item>
/// <item>Other materials: non-creeping (h0 &lt; 0), created at the activating stage.</item>
/// <item>Load cases of a stage: nodal loads + consistent nodal loads of element loads (they stay from the stage
/// on); support displacements are not supported over time.</item>
/// <item>Prestress: axial strain −σp0/Ep of the tendon element at the stage.</item>
/// </list>
/// </summary>
internal sealed class StagePlan
{
    private const double SameTimeStep = 0.01;
    private readonly TdaAnalysisService.Model _m;
    private readonly TimeDependentDto _td;
    private readonly double _shift;
    private readonly Dictionary<double, int> _nodeOf = new();
    private readonly Dictionary<FrameMember, int> _activation = new();     // stage index
    private readonly Dictionary<FrameMember, int> _deactivation = new();
    private readonly List<bool[][]> _restraintsAfter = new();               // per stage
    private readonly List<List<(FrameMember Member, ElementLoadDto Load)>> _stageLoads = new();
    private readonly Dictionary<FrameMember, List<double>> _mesh = new();

    public sealed record Stage(ConstructionStageDto Dto, double TdaTime, int Index);

    public StagePlan(TdaAnalysisService.Model m)
    {
        _m = m;
        _td = m.Td!;
        var ordered = _td.Stages.Select((s, i) => (s, i)).OrderBy(x => x.s.Time).ThenBy(x => x.i).Select(x => x.s).ToList();
        var ages = _td.Elements.ToDictionary(a => a.ElementLabel);
        var cast = m.Members.Where(IsConcrete).Select(x => ages.GetValueOrDefault(x.Label)?.CastingTime).OfType<double>();
        _shift = ordered.Select(s => s.Time).Concat(cast).DefaultIfEmpty(0).Min();

        // stages: TDA times, same time → +0.01 d
        double prev = double.NaN;
        int same = 0;
        foreach (var s in ordered)
        {
            double t = s.Time - _shift;
            same = t == prev ? same + 1 : 0;
            prev = t;
            Stages.Add(new Stage(s, Rt(t + same * SameTimeStep), Stages.Count));
        }
        foreach (var st in Stages)
        {
            foreach (var l in st.Dto.ActivateElements) _activation.TryAdd(m.MemberIndex[l], st.Index);
            foreach (var l in st.Dto.DeactivateElements) _deactivation.TryAdd(m.MemberIndex[l], st.Index);
        }
        EndTime = Rt(_td.AnalysisEnd - _shift);
        OutputTimes = _td.OutputTimes.Select(t => Rt(t - _shift)).Append(EndTime).Where(t => t >= 0).Distinct().OrderBy(t => t).ToList();

        // members: install time, physical type
        var input = new TdaInput
        {
            NodeCount = m.Nodes.Count,
            Ifi = _td.RheologyModel == RheologyModelDto.En1992_2023 ? 15 : 12,
            Humidity = _td.RelativeHumidity
        };
        var install = new Dictionary<FrameMember, double>();
        var keyTimes = new SortedSet<double> { 0, EndTime };
        foreach (var st in Stages) keyTimes.Add(st.TdaTime);
        foreach (var t in OutputTimes) keyTimes.Add(t);
        var concretes = _td.Concretes.ToDictionary(c => c.MaterialLabel);
        var drying = _td.Sections.ToDictionary(s => s.SectionLabel);
        var warned = new HashSet<string>();
        foreach (var mem in m.Members)
        {
            if (!_activation.TryGetValue(mem, out int a))
                throw new NotSupportedException($"TDA: element '{mem.Label}' is not activated by any stage.");
            double active = Stages[a].TdaTime;
            double[] fk;
            if (IsConcrete(mem))
            {
                var age = ages.GetValueOrDefault(mem.Label);
                double c = age?.CastingTime is { } ct ? Rt(ct - _shift) : active;
                double curingEnd = Rt(c + (age?.CuringDuration ?? 3));
                var rheo = concretes[mem.Material.Label];
                var dry = drying[mem.Section];
                double h0 = dry.NotionalSize ?? 2 * mem.A / dry.PerimeterExposed!.Value;
                fk = m.BaseFk(mem);
                fk[7] = h0 * 1000;
                fk[8] = active;
                fk[9] = curingEnd;
                double eMpa = mem.Material.E / 1e6;
                var dt = ConcreteParameters.Dt(rheo.Fck / 1e6, rheo.CementClass.ToString(), rheo.AggregateType.ToString(),
                                               input.Ifi == 15, eMpa, out double eFormula);
                foreach (var (i, v) in dt) fk[6 + i] = v;
                if (Math.Abs(eMpa - eFormula) > 0.2 * eFormula && warned.Add(mem.Material.Label))
                    Warnings.Add($"Concrete '{mem.Material.Label}': E = {eMpa:0} MPa differs from EN 1992-1-1:2023 (5.1) " +
                                 $"Ecm = {eFormula:0} MPa ({rheo.AggregateType}) by more than 20 %; the aggregate modulus was calibrated to E.");
                install[mem] = c;
                keyTimes.Add(c);
                keyTimes.Add(curingEnd);
            }
            else
            {
                fk = m.SteelFk(mem);
                install[mem] = active;
            }
            input.Elements.Add((mem.StartNode + 1, mem.EndNode + 1, input.AddGeometry(m.Geometry(mem)), input.AddPhysical(fk)));
        }

        // time axis
        foreach (var t in LogFill(keyTimes.ToList(), Math.Max(1, _td.StepsPerDecade))) input.Times.Add(t);
        for (int i = 0; i < input.Times.Count; i++)
        {
            _nodeOf[input.Times[i]] = i;
            input.Events.Add(new TdaEvent());
        }
        TdaEvent At(double t) => input.Events[_nodeOf[t]];
        foreach (var (mem, t) in install) At(t).Install.Add(mem.Index + 1);
        foreach (var (mem, s) in _deactivation) At(Stages[s].TdaTime).Remove.Add(mem.Index + 1);

        // supports, loads, prestress per stage
        var mentioned = _td.Stages.SelectMany(s => s.AddSupports.Concat(s.RemoveSupports)).Select(x => x.NodeLabel).ToHashSet();
        var current = m.Nodes.Select(_ => new bool[3]).ToArray();
        var loadCases = m.Request.LoadCases.ToDictionary(l => l.Label);
        foreach (var st in Stages)
        {
            var ev = At(st.TdaTime);
            var add = st.Dto.AddSupports.Select(s => (Node: m.NodeIndex[s.NodeLabel], Dofs: new[] { s.Ux, s.Uy, s.Rz })).ToList();
            if (st.Index == 0)
            {
                for (int n = 0; n < m.Nodes.Count; n++)
                    if (!mentioned.Contains(m.Nodes[n].Label)) add.Add((n, m.RequestRestraints(n)));
            }
            foreach (var s in st.Dto.RemoveSupports)
            {
                int n = m.NodeIndex[s.NodeLabel];
                var dofs = new[] { s.Ux, s.Uy, s.Rz };
                for (int d = 0; d < 3; d++)
                    if (dofs[d] && current[n][d]) { ev.RemoveSupports[d].Add(n + 1); current[n][d] = false; }
            }
            foreach (var (n, dofs) in add)
                for (int d = 0; d < 3; d++)
                    if (dofs[d] && !current[n][d]) { ev.InstallSupports[d].Add((n + 1, 0)); current[n][d] = true; }
            _restraintsAfter.Add(current.Select(r => (bool[])r.Clone()).ToArray());

            var loads = new List<(FrameMember, ElementLoadDto)>();
            foreach (var label in st.Dto.LoadCases)
            {
                var lc = loadCases[label];
                if (lc.SupportDisplacements.Count > 0)
                    throw new NotSupportedException($"TDA: load case '{label}' has support displacements (not supported over time).");
                var l = m.ElementLoads(lc);
                m.AddLoads(ev, lc, l);
                loads.AddRange(l);
            }
            _stageLoads.Add(loads);

            foreach (var p in st.Dto.Prestress)
            {
                var mem = m.MemberIndex[p.ElementLabel];
                ev.AxialStrain.Add((mem.Index + 1, -p.StressStart / mem.Material.E, -p.StressEnd / mem.Material.E));
            }
        }

        // one mesh per member for all states and effects (load points of every stage)
        foreach (var mem in m.Members)
        {
            var host = mem.Host ?? mem;
            var all = _stageLoads.SelectMany(x => x).Where(x => x.Member == host).Select(x => x.Load).ToList();
            _mesh[mem] = host.MeshPoints(all, m.MeshSegments);
        }
        Input = input;
    }

    public TdaInput Input { get; }
    public List<Stage> Stages { get; } = new();
    public List<double> OutputTimes { get; }
    public double EndTime { get; }
    public List<string> Warnings { get; } = new();

    public int NodeOf(double tdaTime) => _nodeOf[tdaTime];

    public double ToRequestTime(double tdaTime) => Rt(tdaTime + _shift);

    public IReadOnlyCollection<(FrameMember, ElementLoadDto)> LoadsOfStage(Stage st) => _stageLoads[st.Index];

    private int LastStage(double t, bool before) =>
        Stages.FindLastIndex(s => before ? s.TdaTime < t : s.TdaTime <= t);

    private bool[][] RestraintsAt(double t, bool before)
    {
        int s = LastStage(t, before);
        return s < 0 ? _m.Nodes.Select(_ => new bool[3]).ToArray() : _restraintsAfter[s];
    }

    private List<FrameMember> ActiveAt(double t, bool before)
    {
        int s = LastStage(t, before);
        return _m.Members.Where(x => _activation[x] <= s && !(_deactivation.TryGetValue(x, out int d) && d <= s)).ToList();
    }

    private List<(FrameMember, ElementLoadDto)> LoadsUpTo(double t, bool before)
    {
        int s = LastStage(t, before);
        return _stageLoads.Take(s + 1).SelectMany(x => x).ToList();
    }

    public CaseResultDto CaseResult(TdaSnapshot snap, IReadOnlyCollection<(FrameMember, ElementLoadDto)> loads, double t, bool before) =>
        _m.ToCaseResult(snap, loads, RestraintsAt(t, before), ActiveAt(t, before), mem => _mesh[mem]);

    public TimeStateDto State(double t, TimeStateKindDto kind, TdaSnapshot totals, bool before)
    {
        int s = LastStage(t, before: false);   // BeforeStage: the stage the state precedes
        return new TimeStateDto
        {
            Time = ToRequestTime(t),
            Kind = kind,
            Stage = s >= 0 ? Stages[s].Dto.Label : string.Empty,
            ActiveElements = ActiveAt(t, before).Select(x => x.Label).ToList(),
            Total = CaseResult(totals, LoadsUpTo(t, before), t, before)
        };
    }

    public TimeHistoriesDto Histories(IReadOnlyList<TdaStep> steps)
    {
        var h = new TimeHistoriesDto();
        var everRestrained = Enumerable.Range(0, _m.Nodes.Count)
            .Where(n => _restraintsAfter.Any(r => r[n].Any(x => x))).ToHashSet();
        for (int n = 0; n < _m.Nodes.Count; n++)
        {
            var nh = new NodeHistoryDto();
            if (everRestrained.Contains(n)) { nh.Fx = new(); nh.Fy = new(); nh.Mz = new(); }
            h.Nodes[_m.Nodes[n].Label] = nh;
        }
        foreach (var mem in _m.Members) h.Elements[mem.Label] = new ElementHistoryDto();
        foreach (var step in steps)
        {
            h.Times.Add(ToRequestTime(step.Time));
            var snap = step.Totals;
            var restraints = RestraintsAt(step.Time, before: false);
            for (int n = 0; n < _m.Nodes.Count; n++)
            {
                var nh = h.Nodes[_m.Nodes[n].Label];
                var d = snap.Displacements[n];
                nh.Ux.Add(d[0]); nh.Uy.Add(-d[1]); nh.Rz.Add(d[2]);
                if (nh.Fx is null) continue;
                var r = snap.Reactions[n];
                nh.Fx.Add(restraints[n][0] ? -r[0] * 1000 : 0);
                nh.Fy!.Add(restraints[n][1] ? r[1] * 1000 : 0);
                nh.Mz!.Add(restraints[n][2] ? -r[2] * 1000 : 0);
            }
            var loads = LoadsUpTo(step.Time, before: false);
            foreach (var mem in _m.Members)
            {
                var own = loads.Where(x => x.Item1 == mem).Select(x => x.Item2).ToList();
                var e = _m.EndForces(mem, snap, own);
                var eh = h.Elements[mem.Label];
                eh.NStart.Add(e[0]); eh.NEnd.Add(e[1]); eh.MStart.Add(e[2]); eh.MEnd.Add(e[3]); eh.V.Add(e[4]);
            }
        }
        return h;
    }

    private static bool IsConcrete(FrameMember m) => m.Material.MaterialType == MaterialTypeDto.Concrete;

    /// <summary>Key times plus logarithmic fill: n = ⌈k·log10(1+Δt)⌉ points t1 + (1+Δt)^(j/n) − 1 (as CSSC TdaModelBuilder).</summary>
    internal static List<double> LogFill(IReadOnlyList<double> keys, int perDecade)
    {
        var result = new List<double> { keys[0] };
        for (int i = 1; i < keys.Count; i++)
        {
            double t1 = keys[i - 1], t2 = keys[i], span = t2 - t1;
            int n = Math.Max(1, (int)Math.Ceiling(perDecade * Math.Log10(1 + span)));
            double last = t1;
            for (int j = 1; j < n; j++)
            {
                double t = Rt(t1 + Math.Pow(1 + span, (double)j / n) - 1);
                if (t - last >= 0.05 && t2 - t >= 0.05) { result.Add(t); last = t; }
            }
            result.Add(t2);
        }
        return result;
    }

    internal static double Rt(double t) => Math.Floor(t * 1e4 + 0.5) / 1e4;
}
