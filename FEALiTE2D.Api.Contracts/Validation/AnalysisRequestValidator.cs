using System;
using System.Collections.Generic;
using System.Linq;
using FEALiTE2D.Api.Contracts.Enums;
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
        if (request.TimeDependent is not null)
            ValidateTimeDependent(request, nodeLabels, materialLabels, sectionLabels, elementLabels, loadCaseLabels, result);

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

    private static void ValidateTimeDependent(AnalysisRequest request, HashSet<string> nodeLabels,
        HashSet<string> materialLabels, HashSet<string> sectionLabels, HashSet<string> elementLabels,
        HashSet<string> loadCaseLabels, ValidationResult result)
    {
        var td = request.TimeDependent!;
        const string p = "TimeDependent";

        if (td.RelativeHumidity <= 0 || td.RelativeHumidity > 1)
            result.Add($"{p}: relativeHumidity must be in (0, 1].");
        if (td.AnalysisEnd <= 0) result.Add($"{p}: analysisEnd must be greater than 0.");
        if (td.StepsPerDecade < 1) result.Add($"{p}: stepsPerDecade must be at least 1.");
        foreach (var t in td.OutputTimes)
            if (t < 0 || t > td.AnalysisEnd)
                result.Add($"{p}: output time {t} is outside [0, analysisEnd].");

        // features a time-dependent solver over this request does not support
        foreach (var n in request.Nodes)
        {
            if (n.Support is SpringSupportDto) result.Add($"{p}: node '{n.Label}' has a spring support (not supported).");
            if (n.RotationAngle != 0) result.Add($"{p}: node '{n.Label}' has a rotated support (not supported).");
        }
        foreach (var e in request.Elements)
            if (e is SpringElementDto) result.Add($"{p}: element '{e.Label}' is a spring element (not supported).");

        // concrete materials and their creep properties
        var concreteMaterials = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in request.Materials)
            if (m.MaterialType == MaterialTypeDto.Concrete) concreteMaterials.Add(m.Label);
        var concretes = ValidateUniqueLabels(td.Concretes, c => c.MaterialLabel, $"{p}.concretes", result);
        foreach (var c in td.Concretes)
        {
            if (!materialLabels.Contains(c.MaterialLabel))
                result.Add($"{p}: concrete '{c.MaterialLabel}' is not a known material label.");
            else if (!concreteMaterials.Contains(c.MaterialLabel))
                result.Add($"{p}: material '{c.MaterialLabel}' is not of type Concrete.");
            if (c.Fck <= 0) result.Add($"{p}: concrete '{c.MaterialLabel}': fck must be greater than 0.");
        }
        foreach (var m in concreteMaterials)
            if (!concretes.Contains(m)) result.Add($"{p}: concrete material '{m}' has no creep properties (concretes).");

        // drying of concrete sections
        var drying = ValidateUniqueLabels(td.Sections, s => s.SectionLabel, $"{p}.sections", result);
        foreach (var s in td.Sections)
        {
            if (!sectionLabels.Contains(s.SectionLabel))
                result.Add($"{p}: section '{s.SectionLabel}' is not a known section label.");
            var h0 = s.NotionalSize ?? 0;
            var u = s.PerimeterExposed ?? 0;
            if (h0 <= 0 && u <= 0)
                result.Add($"{p}: section '{s.SectionLabel}': notionalSize or perimeterExposed must be greater than 0.");
        }
        foreach (var s in request.Sections)
            if (concreteMaterials.Contains(s.MaterialLabel) && !drying.Contains(s.Label))
                result.Add($"{p}: concrete section '{s.Label}' has no drying data (sections).");

        // elements only the time-dependent solver models (tendons, slab): parallel to the request elements
        var allElements = new HashSet<string>(elementLabels, StringComparer.Ordinal);
        var sectionOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in request.Elements)
            if (e is FrameElementDto f) sectionOf[f.Label] = f.SectionLabel;
        foreach (var e in td.AdditionalElements)
        {
            var s = $"{p}: additional element '{e.Label}'";
            if (string.IsNullOrWhiteSpace(e.Label)) { result.Add($"{p}: additional element with an empty label."); continue; }
            if (!allElements.Add(e.Label)) result.Add($"{s}: duplicate element label.");
            if (!nodeLabels.Contains(e.StartNodeLabel)) result.Add($"{s}: startNodeLabel '{e.StartNodeLabel}' not found.");
            if (!nodeLabels.Contains(e.EndNodeLabel)) result.Add($"{s}: endNodeLabel '{e.EndNodeLabel}' not found.");
            if (e.StartNodeLabel == e.EndNodeLabel) result.Add($"{s}: start and end node are the same.");
            if (!sectionLabels.Contains(e.SectionLabel)) result.Add($"{s}: sectionLabel '{e.SectionLabel}' not found.");
            sectionOf[e.Label] = e.SectionLabel;
        }
        var materialOfSection = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var s in request.Sections) materialOfSection[s.Label] = s.MaterialLabel;
        var steelMaterials = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in request.Materials)
            if (m.MaterialType == MaterialTypeDto.Steel) steelMaterials.Add(m.Label);

        // element ages
        var ages = new Dictionary<string, ElementAgeDto>(StringComparer.Ordinal);
        ValidateUniqueLabels(td.Elements, e => e.ElementLabel, $"{p}.elements", result);
        foreach (var a in td.Elements)
        {
            if (!allElements.Contains(a.ElementLabel))
                result.Add($"{p}: element '{a.ElementLabel}' is not a known element label.");
            if (a.CuringDuration < 0) result.Add($"{p}: element '{a.ElementLabel}': curingDuration must not be negative.");
            if (a.CastingTime is < 0) result.Add($"{p}: element '{a.ElementLabel}': castingTime must not be negative.");
            ages[a.ElementLabel] = a;
        }

        // stages: time order, references, element and load case life cycle
        if (td.Stages.Count == 0) result.Add($"{p}: at least one stage is required.");
        ValidateUniqueLabels(td.Stages, s => s.Label, $"{p}.stages", result);
        var loadCases = new Dictionary<string, LoadCaseDto>(StringComparer.Ordinal);
        foreach (var lc in request.LoadCases) loadCases[lc.Label] = lc;
        var active = new HashSet<string>(StringComparer.Ordinal);
        var everActive = new HashSet<string>(StringComparer.Ordinal);
        var applied = new HashSet<string>(StringComparer.Ordinal);
        double previous = double.NegativeInfinity;
        foreach (var st in td.Stages)
        {
            var s = $"{p}: stage '{st.Label}'";
            if (st.Time < previous) result.Add($"{s}: stages must be in time order.");
            if (st.Time < 0 || st.Time >= td.AnalysisEnd) result.Add($"{s}: time must be in [0, analysisEnd).");
            previous = st.Time;

            foreach (var e in st.ActivateElements)
            {
                if (!allElements.Contains(e)) { result.Add($"{s}: unknown element '{e}'."); continue; }
                if (!everActive.Add(e)) result.Add($"{s}: element '{e}' is activated more than once.");
                active.Add(e);
                if (ages.TryGetValue(e, out var a) && a.CastingTime > st.Time)
                    result.Add($"{s}: element '{e}' is activated before its casting time.");
            }
            foreach (var e in st.DeactivateElements)
                if (!active.Remove(e)) result.Add($"{s}: element '{e}' is not active.");

            foreach (var sup in st.AddSupports.Concat(st.RemoveSupports))
            {
                if (!nodeLabels.Contains(sup.NodeLabel)) result.Add($"{s}: unknown node '{sup.NodeLabel}'.");
                if (!sup.Ux && !sup.Uy && !sup.Rz) result.Add($"{s}: support at '{sup.NodeLabel}' restrains nothing.");
            }

            foreach (var lcLabel in st.LoadCases)
            {
                if (!loadCaseLabels.Contains(lcLabel)) { result.Add($"{s}: unknown load case '{lcLabel}'."); continue; }
                if (!applied.Add(lcLabel)) result.Add($"{s}: load case '{lcLabel}' is applied more than once.");
                foreach (var l in loadCases[lcLabel].ElementLoads)
                    if (!active.Contains(l.ElementLabel))
                        result.Add($"{s}: load case '{lcLabel}' loads element '{l.ElementLabel}', which is not active.");
            }

            foreach (var ps in st.Prestress)
            {
                if (!allElements.Contains(ps.ElementLabel)) { result.Add($"{s}: prestress of unknown element '{ps.ElementLabel}'."); continue; }
                if (!active.Contains(ps.ElementLabel))
                    result.Add($"{s}: prestressed element '{ps.ElementLabel}' is not active.");
                if (!sectionOf.TryGetValue(ps.ElementLabel, out var sec) || !materialOfSection.TryGetValue(sec, out var mat)
                    || !steelMaterials.Contains(mat))
                    result.Add($"{s}: prestressed element '{ps.ElementLabel}' must have a Steel section.");
                if (ps.StressStart <= 0 || ps.StressEnd <= 0)
                    result.Add($"{s}: prestress of '{ps.ElementLabel}' must be greater than 0.");
            }
        }
        foreach (var e in allElements)
            if (!everActive.Contains(e)) result.Add($"{p}: element '{e}' is never activated.");
    }
}
