using System;
using System.Collections.Generic;
using FEALiTE2D.Api.Contracts.Requests;
using FEALiTE2D.CrossSections;
using FEALiTE2D.Elements;
using FEALiTE2D.Loads;
using FEALiTE2D.Materials;

namespace FEALiTE2D.Api.Contracts.Mapping;

/// <summary>
/// Maps an <see cref="AnalysisRequest"/> DTO into a fully-configured
/// <see cref="FEALiTE2D.Structure.Structure"/> ready for <c>Solve()</c>.
/// </summary>
public sealed class StructureBuilder
{
    public Dictionary<string, Node2D> Nodes { get; } = new();
    public Dictionary<string, IMaterial> Materials { get; } = new();
    public Dictionary<string, Frame2DSection> Sections { get; } = new();
    public Dictionary<string, IElement> Elements { get; } = new();
    public Dictionary<string, LoadCase> LoadCases { get; } = new();
    public Dictionary<string, LoadCombination> LoadCombinations { get; } = new();

    public FEALiTE2D.Structure.Structure Build(AnalysisRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var structure = new FEALiTE2D.Structure.Structure();

        BuildNodes(request, structure);
        BuildMaterials(request);
        BuildSections(request);
        BuildElements(request, structure);
        BuildLoadCases(request, structure);
        BuildLoadCombinations(request);

        structure.LinearMesher.NumberSegments = request.Settings?.MeshSegments ?? 10;

        return structure;
    }

    private void BuildNodes(AnalysisRequest request, FEALiTE2D.Structure.Structure structure)
    {
        foreach (var dto in request.Nodes)
        {
            var node = new Node2D(dto.X, dto.Y, dto.Label)
            {
                RotaionAngle = dto.RotationAngle,
                Support = MapSupport(dto.Support)
            };
            Nodes.Add(dto.Label, node);
            structure.AddNode(node);
        }
    }

    private static NodalSupport? MapSupport(SupportDto? dto) => dto switch
    {
        null => null,
        RigidSupportDto r => new NodalSupport(r.Ux, r.Uy, r.Rz),
        SpringSupportDto s => new NodalSpringSupport(s.Kx, s.Ky, s.Cz),
        _ => throw new NotSupportedException($"Unsupported support type {dto.GetType().Name}.")
    };

    private void BuildMaterials(AnalysisRequest request)
    {
        foreach (var dto in request.Materials)
        {
            var mat = new GenericIsotropicMaterial
            {
                Label = dto.Label,
                E = dto.E,
                U = dto.U,
                Alpha = dto.Alpha,
                Gama = dto.Gama,
                MaterialType = dto.MaterialType.ToDomain()
            };
            Materials.Add(dto.Label, mat);
        }
    }

    private void BuildSections(AnalysisRequest request)
    {
        foreach (var dto in request.Sections)
        {
            if (!Materials.TryGetValue(dto.MaterialLabel, out var mat))
                throw new InvalidOperationException(
                    $"Section '{dto.Label}': material '{dto.MaterialLabel}' not found.");

            Frame2DSection section = dto switch
            {
                GenericSectionDto g => new Generic2DSection(
                    g.A, g.Az, g.Ay, g.Iz, g.Iy, g.J, g.MaxHeight, g.MaxWidth, mat),
                RectangularSectionDto r => new RectangularSection(r.B, r.T, mat),
                CircularSectionDto c => new CircularSection(c.D, mat),
                IPESectionDto ipe => new IPESection(ipe.Tf, ipe.Tw, ipe.B, ipe.H, ipe.R, mat),
                HollowTubeSectionDto h => new HollowTube(h.D, h.Thickness, mat),
                _ => throw new NotSupportedException($"Unsupported section type {dto.GetType().Name}.")
            };
            section.Label = dto.Label;
            Sections.Add(dto.Label, section);
        }
    }

    private void BuildElements(AnalysisRequest request, FEALiTE2D.Structure.Structure structure)
    {
        foreach (var dto in request.Elements)
        {
            var start = ResolveNode(dto.StartNodeLabel, dto.Label, "startNodeLabel");
            var end = ResolveNode(dto.EndNodeLabel, dto.Label, "endNodeLabel");

            IElement element = dto switch
            {
                FrameElementDto f => BuildFrameElement(f, start, end),
                SpringElementDto s => new SpringElement2D(start, end, s.Label) { K = s.K, R = s.R },
                _ => throw new NotSupportedException($"Unsupported element type {dto.GetType().Name}.")
            };
            Elements.Add(dto.Label, element);
            structure.AddElement(element);
        }
    }

    private FrameElement2D BuildFrameElement(FrameElementDto dto, Node2D start, Node2D end)
    {
        if (!Sections.TryGetValue(dto.SectionLabel, out var section))
            throw new InvalidOperationException(
                $"Element '{dto.Label}': section '{dto.SectionLabel}' not found.");

        return new FrameElement2D(start, end, dto.Label)
        {
            CrossSection = section,
            EndRelease = dto.EndRelease.ToDomain()
        };
    }

    private Node2D ResolveNode(string label, string ownerLabel, string field)
    {
        if (!Nodes.TryGetValue(label, out var node))
            throw new InvalidOperationException(
                $"Element '{ownerLabel}': {field} '{label}' not found.");
        return node;
    }

    private void BuildLoadCases(AnalysisRequest request, FEALiTE2D.Structure.Structure structure)
    {
        foreach (var dto in request.LoadCases)
        {
            var lc = new LoadCase(dto.Label, dto.LoadCaseType.ToDomain())
            {
                LoadCaseDuration = dto.LoadCaseDuration.ToDomain()
            };
            LoadCases.Add(dto.Label, lc);
            structure.LoadCasesToRun.Add(lc);

            foreach (var nl in dto.NodalLoads)
            {
                var node = ResolveLoadTarget(Nodes, nl.NodeLabel, "NodalLoad", "nodeLabel");
                node.NodalLoads.Add(new NodalLoad(nl.Fx, nl.Fy, nl.Mz, nl.Direction.ToDomain(), lc));
            }

            foreach (var sd in dto.SupportDisplacements)
            {
                var node = ResolveLoadTarget(Nodes, sd.NodeLabel, "SupportDisplacement", "nodeLabel");
                node.SupportDisplacementLoad.Add(new SupportDisplacementLoad(sd.Ux, sd.Uy, sd.Rz, lc)
                {
                    LoadDirection = sd.Direction.ToDomain()
                });
            }

            foreach (var el in dto.ElementLoads)
            {
                var element = ResolveLoadTarget(Elements, el.ElementLabel, "ElementLoad", "elementLabel");
                element.Loads.Add(MapElementLoad(el, lc));
            }
        }
    }

    private static ILoad MapElementLoad(ElementLoadDto dto, LoadCase lc) => dto switch
    {
        PointLoadDto p => new FramePointLoad(p.Fx, p.Fy, p.Mz, p.L1, p.Direction.ToDomain(), lc),
        UniformLoadDto u => new FrameUniformLoad(u.Wx, u.Wy, u.Direction.ToDomain(), lc, u.L1, u.L2),
        TrapezoidalLoadDto t => new FrameTrapezoidalLoad(
            t.Wx1, t.Wx2, t.Wy1, t.Wy2, t.Direction.ToDomain(), lc, t.L1, t.L2),
        _ => throw new NotSupportedException($"Unsupported element load type {dto.GetType().Name}.")
    };

    private void BuildLoadCombinations(AnalysisRequest request)
    {
        foreach (var dto in request.LoadCombinations)
        {
            var combo = new LoadCombination(dto.Label);
            foreach (var (lcLabel, factor) in dto.Factors)
            {
                if (!LoadCases.TryGetValue(lcLabel, out var lc))
                    throw new InvalidOperationException(
                        $"LoadCombination '{dto.Label}': loadCase '{lcLabel}' not found.");
                combo.Add(lc, factor);
            }
            LoadCombinations.Add(dto.Label, combo);
        }
    }

    private static T ResolveLoadTarget<T>(IReadOnlyDictionary<string, T> map, string label,
        string loadKind, string field)
    {
        if (!map.TryGetValue(label, out var value))
            throw new InvalidOperationException($"{loadKind}: {field} '{label}' not found.");
        return value;
    }
}
