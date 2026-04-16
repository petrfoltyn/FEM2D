using System;
using System.Collections.Generic;
using FEALiTE2D.Api.Contracts.Requests;

namespace FEALiTE2D.Api.Contracts.Validation;

public sealed class AnalysisRequestValidator
{
    public ValidationResult Validate(AnalysisRequest request)
    {
        var result = new ValidationResult();
        if (request is null)
        {
            result.Add("Request is null.");
            return result;
        }

        if (request.Nodes.Count == 0) result.Add("Structure must contain at least one node.");
        if (request.Elements.Count == 0) result.Add("Structure must contain at least one element.");
        if (request.LoadCases.Count == 0) result.Add("At least one load case is required.");

        var nodeLabels = ValidateUniqueLabels(request.Nodes, n => n.Label, "node", result);
        var materialLabels = ValidateUniqueLabels(request.Materials, m => m.Label, "material", result);
        var sectionLabels = ValidateUniqueLabels(request.Sections, s => s.Label, "section", result);
        var elementLabels = ValidateUniqueLabels(request.Elements, e => e.Label, "element", result);
        var loadCaseLabels = ValidateUniqueLabels(request.LoadCases, lc => lc.Label, "loadCase", result);
        ValidateUniqueLabels(request.LoadCombinations, c => c.Label, "loadCombination", result);

        ValidateAnySupport(request, result);
        ValidateMaterials(request, result);
        ValidateSections(request, materialLabels, result);
        var elementLengths = ValidateElements(request, nodeLabels, sectionLabels, result);
        ValidateLoadCases(request, nodeLabels, elementLabels, elementLengths, result);
        ValidateLoadCombinations(request, loadCaseLabels, result);

        return result;
    }

    private static HashSet<string> ValidateUniqueLabels<T>(IEnumerable<T> items,
        Func<T, string> selector, string kind, ValidationResult result)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var label = selector(item);
            if (string.IsNullOrWhiteSpace(label))
            {
                result.Add($"{kind}: empty label is not allowed.");
                continue;
            }
            if (!seen.Add(label))
                result.Add($"{kind}: duplicate label '{label}'.");
        }
        return seen;
    }

    private static void ValidateAnySupport(AnalysisRequest request, ValidationResult result)
    {
        foreach (var node in request.Nodes)
            if (node.Support is not null)
                return;

        if (request.Nodes.Count > 0)
            result.Add("Structure has no supports — at least one node must be restrained.");
    }

    private static void ValidateMaterials(AnalysisRequest request, ValidationResult result)
    {
        foreach (var m in request.Materials)
        {
            if (m.E <= 0) result.Add($"Material '{m.Label}': E must be greater than 0.");
            if (m.U < 0 || m.U >= 0.5) result.Add($"Material '{m.Label}': U must be in [0, 0.5).");
        }
    }

    private static void ValidateSections(AnalysisRequest request,
        HashSet<string> materialLabels, ValidationResult result)
    {
        foreach (var s in request.Sections)
        {
            if (!materialLabels.Contains(s.MaterialLabel))
                result.Add($"Section '{s.Label}': materialLabel '{s.MaterialLabel}' not found.");

            switch (s)
            {
                case GenericSectionDto g:
                    if (g.A <= 0) result.Add($"Section '{g.Label}': A must be greater than 0.");
                    if (g.Iz <= 0) result.Add($"Section '{g.Label}': Iz must be greater than 0.");
                    break;
                case RectangularSectionDto r:
                    if (r.B <= 0 || r.T <= 0) result.Add($"Section '{r.Label}': b and t must be greater than 0.");
                    break;
                case CircularSectionDto c:
                    if (c.D <= 0) result.Add($"Section '{c.Label}': d must be greater than 0.");
                    break;
                case IPESectionDto ipe:
                    if (ipe.B <= 0 || ipe.H <= 0 || ipe.Tf <= 0 || ipe.Tw <= 0)
                        result.Add($"Section '{ipe.Label}': b, h, tf, tw must be greater than 0.");
                    break;
                case HollowTubeSectionDto h:
                    if (h.D <= 0 || h.Thickness <= 0)
                        result.Add($"Section '{h.Label}': d and thickness must be greater than 0.");
                    break;
            }
        }
    }

    private static Dictionary<string, double> ValidateElements(AnalysisRequest request,
        HashSet<string> nodeLabels, HashSet<string> sectionLabels, ValidationResult result)
    {
        var lengths = new Dictionary<string, double>();
        var nodeCoords = new Dictionary<string, (double X, double Y)>();
        foreach (var n in request.Nodes)
            nodeCoords[n.Label] = (n.X, n.Y);

        foreach (var e in request.Elements)
        {
            if (!nodeLabels.Contains(e.StartNodeLabel))
                result.Add($"Element '{e.Label}': startNodeLabel '{e.StartNodeLabel}' not found.");
            if (!nodeLabels.Contains(e.EndNodeLabel))
                result.Add($"Element '{e.Label}': endNodeLabel '{e.EndNodeLabel}' not found.");

            if (e is FrameElementDto f && !sectionLabels.Contains(f.SectionLabel))
                result.Add($"Element '{e.Label}': sectionLabel '{f.SectionLabel}' not found.");

            if (e is SpringElementDto s)
            {
                if (s.K < 0) result.Add($"SpringElement '{e.Label}': K must be >= 0.");
                if (s.R < 0) result.Add($"SpringElement '{e.Label}': R must be >= 0.");
            }

            if (nodeCoords.TryGetValue(e.StartNodeLabel, out var p1) &&
                nodeCoords.TryGetValue(e.EndNodeLabel, out var p2))
            {
                var dx = p2.X - p1.X;
                var dy = p2.Y - p1.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length <= 0)
                    result.Add($"Element '{e.Label}': start and end nodes coincide (zero length).");
                lengths[e.Label] = length;
            }
        }
        return lengths;
    }

    private static void ValidateLoadCases(AnalysisRequest request,
        HashSet<string> nodeLabels, HashSet<string> elementLabels,
        Dictionary<string, double> elementLengths, ValidationResult result)
    {
        foreach (var lc in request.LoadCases)
        {
            foreach (var nl in lc.NodalLoads)
                if (!nodeLabels.Contains(nl.NodeLabel))
                    result.Add($"LoadCase '{lc.Label}': nodal load references unknown node '{nl.NodeLabel}'.");

            foreach (var sd in lc.SupportDisplacements)
                if (!nodeLabels.Contains(sd.NodeLabel))
                    result.Add($"LoadCase '{lc.Label}': support displacement references unknown node '{sd.NodeLabel}'.");

            foreach (var el in lc.ElementLoads)
            {
                if (!elementLabels.Contains(el.ElementLabel))
                {
                    result.Add($"LoadCase '{lc.Label}': element load references unknown element '{el.ElementLabel}'.");
                    continue;
                }
                if (!elementLengths.TryGetValue(el.ElementLabel, out var len)) continue;

                switch (el)
                {
                    case PointLoadDto p:
                        if (p.L1 < 0 || p.L1 > len)
                            result.Add($"LoadCase '{lc.Label}': point load on '{el.ElementLabel}' L1={p.L1} out of [0, {len}].");
                        break;
                    case UniformLoadDto u:
                        if (u.L1 < 0 || u.L2 < 0 || u.L1 + u.L2 > len)
                            result.Add($"LoadCase '{lc.Label}': uniform load on '{el.ElementLabel}' L1+L2={u.L1 + u.L2} exceeds length {len}.");
                        break;
                    case TrapezoidalLoadDto t:
                        if (t.L1 < 0 || t.L2 < 0 || t.L1 + t.L2 > len)
                            result.Add($"LoadCase '{lc.Label}': trapezoidal load on '{el.ElementLabel}' L1+L2={t.L1 + t.L2} exceeds length {len}.");
                        break;
                }
            }
        }
    }

    private static void ValidateLoadCombinations(AnalysisRequest request,
        HashSet<string> loadCaseLabels, ValidationResult result)
    {
        foreach (var c in request.LoadCombinations)
        {
            if (c.Factors.Count == 0)
                result.Add($"LoadCombination '{c.Label}': no factors defined.");

            foreach (var key in c.Factors.Keys)
                if (!loadCaseLabels.Contains(key))
                    result.Add($"LoadCombination '{c.Label}': factor key '{key}' is not a known loadCase label.");
        }
    }
}
