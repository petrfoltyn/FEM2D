using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FEALiTE2D.Api.Contracts;
using FEALiTE2D.Api.Contracts.Enums;
using FEALiTE2D.Api.Contracts.Requests;
using FEALiTE2D.Api.Contracts.Responses;
using FEALiTE2D.Tda;
using FEALiTE2D.Tests.Api;
using NUnit.Framework;

namespace FEALiTE2D.Tests.Tda;

/// <summary>
/// TDA over the FEALiTE2D contract. Needs the TDA command line program: env TDA_EXE, or d:\Repos\TDA next to this
/// repository (build-win/Release/tda.exe on Windows, build/tda elsewhere); without it the tests are ignored.
/// </summary>
public class TdaAnalysisTest
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FEALiTE2D.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("FEALiTE2D.sln not found above the test directory.");
    }

    private static TdaAnalysisService Service()
    {
        string? exe = Environment.GetEnvironmentVariable("TDA_EXE");
        if (string.IsNullOrEmpty(exe))
        {
            string tda = Path.Combine(Path.GetDirectoryName(RepoRoot)!, "TDA");
            exe = OperatingSystem.IsWindows()
                ? Path.Combine(tda, "build-win", "Release", "tda.exe")
                : Path.Combine(tda, "build", "tda");
        }
        if (!File.Exists(exe)) Assert.Ignore($"TDA executable not found ({exe}); set TDA_EXE.");
        return new TdaAnalysisService(new TdaCliSolver(exe));
    }

    // ===================================================================== elastic: FEALiTE2D examples

    /// <summary>Examples of FEM2D/examples that TDA can compute (spring supports and elements excluded).</summary>
    private static readonly string[] Examples =
    {
        "beam-start-release", "classic-frame", "continuous-beam", "fixed-end-beam", "frame-load-combination",
        "hinged-beam-internal-forces", "pp-trapezoidal-full", "pp-trapezoidal-partial", "pp-uniform-full",
        "pp-uniform-partial-center", "pp-uniform-partial-edge", "steel-frame-kip-inch", "steel-frame-kip-inch-settlement"
    };

    /// <summary>
    /// Every check of expected.json, tolerance max(tol, 1e-4 · largest |expected| of the same quantity) because TDA
    /// keeps its results in single precision.
    /// </summary>
    [TestCaseSource(nameof(Examples))]
    public void Elastic_example_matches_fealite(string name)
    {
        var service = Service();
        string dir = Path.Combine(RepoRoot, "examples", name);
        var request = JsonSerializer.Deserialize<AnalysisRequest>(File.ReadAllText(Path.Combine(dir, "request.json")), ApiJsonOptions.Default)!;
        var response = service.Analyze(request);
        Assert.That(response.Status, Is.EqualTo(AnalysisStatusDto.Successful), string.Join("\n", response.Errors));

        var doc = JsonSerializer.SerializeToNode(response, ApiJsonOptions.Default)!;
        var checks = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "expected.json")))!["checks"]!.AsArray();
        var scale = new Dictionary<string, double>();
        foreach (var c in checks.Where(c => c!["expected"] != null))
        {
            string f = Family(c!["path"]!.GetValue<string>());
            scale[f] = Math.Max(scale.GetValueOrDefault(f), Math.Abs(c["expected"]!.GetValue<double>()));
        }
        var failures = new List<string>();
        foreach (var c in checks)
        {
            string path = c!["path"]!.GetValue<string>();
            var value = Resolve(doc, path);
            if (c["absent"] != null)
            {
                if (value != null) failures.Add($"{path}: present");
                continue;
            }
            if (value == null) { failures.Add($"{path}: missing"); continue; }
            if (c["count"] != null)
            {
                if (value.AsArray().Count != c["count"]!.GetValue<int>()) failures.Add($"{path}: count {value.AsArray().Count}");
                continue;
            }
            double expected = c["expected"]!.GetValue<double>(), actual = value.GetValue<double>();
            double tol = Math.Max(c["tol"]?.GetValue<double>() ?? 1e-6, 1e-4 * scale[Family(path)]);
            if (Math.Abs(actual - expected) > tol) failures.Add($"{path}: {actual} (expected {expected} ± {tol})");
        }
        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    private static string Family(string path)
    {
        var parts = Regex.Replace(path, @"\[[^\]]*\]", "").Split('.');
        return string.Join(".", parts.Take(3).Append(parts[^1]));
    }

    private static JsonNode? Resolve(JsonNode doc, string path)
    {
        JsonNode? cur = doc;
        foreach (Match m in Regex.Matches(path, @"([^.\[\]]+)|\[([^\]]+)\]"))
        {
            if (cur == null) return null;
            if (m.Groups[1].Success)
            {
                cur = cur is JsonObject o && o.TryGetPropertyValue(m.Groups[1].Value, out var v) ? v : null;
                continue;
            }
            if (cur is not JsonArray arr) return null;
            var sel = m.Groups[2].Value;
            var eq = Regex.Match(sel, @"^(\w+)=(.+)$");
            if (eq.Success)
            {
                double target = double.Parse(eq.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                cur = arr.FirstOrDefault(it => it?[eq.Groups[1].Value] is JsonValue jv && Math.Abs(jv.GetValue<double>() - target) < 1e-6);
            }
            else
            {
                int i = int.Parse(sel);
                if (i < 0) i += arr.Count;
                cur = i >= 0 && i < arr.Count ? arr[i] : null;
            }
        }
        return cur;
    }

    // ========================================================================== time-dependent

    private static double SumFy(CaseResultDto r) => r.SupportReactions.Values.Sum(f => f.Fy);

    private static SegmentDto At(List<SegmentDto> segs, double x) => segs.First(s => Math.Abs(s.X2 - x) < 1e-9);

    private static TimeStateDto State(TimeDependentResultsDto r, string stage, TimeStateKindDto kind) =>
        r.States.Single(s => s.Stage == stage && s.Kind == kind);

    /// <summary>The sum of the effects up to the end equals the end state (displacements, reactions, forces).</summary>
    private static void AssertEffectsSumToEnd(TimeDependentResultsDto r)
    {
        var end = r.States.Single(s => s.Kind == TimeStateKindDto.End).Total;
        foreach (var (node, d) in end.NodeDisplacements)
        {
            double sum = r.Effects.Sum(e => e.Result.NodeDisplacements[node].Rz);
            Assert.That(sum, Is.EqualTo(d.Rz).Within(1e-6 * Math.Max(1e-3, Math.Abs(d.Rz)) + 1e-9), $"rz {node}");
        }
        foreach (var (node, f) in end.SupportReactions)
        {
            double sum = r.Effects.Sum(e => e.Result.SupportReactions.GetValueOrDefault(node)?.Fy ?? 0);
            Assert.That(sum, Is.EqualTo(f.Fy).Within(1e-4 * Math.Max(1, Math.Abs(f.Fy))), $"fy {node}");
        }
        foreach (var (label, segs) in end.ElementForces)
            for (int i = 0; i < segs.Count; i++)
            {
                double sum = r.Effects.Sum(e => e.Result.ElementForces.TryGetValue(label, out var s) ? s[i].EndForce.Mz : 0);
                Assert.That(sum, Is.EqualTo(segs[i].EndForce.Mz).Within(1e-4 * Math.Max(1000, Math.Abs(segs[i].EndForce.Mz))), $"mz {label}[{i}]");
            }
    }

    [Test]
    public void Staged_continuous_beam_creep()
    {
        var response = Service().Analyze(TimeDependentContractTest.StagedBeam());
        Assert.That(response.Status, Is.EqualTo(AnalysisStatusDto.Successful), string.Join("\n", response.Errors));
        var r = response.TimeDependentResults!;

        // striking at 28 days: continuous beam 2 × 10 m under G0 = 8 kN/m, E(28) = E28 → M_B = qL²/8 (mz = −M)
        var struck = State(r, "odskružení", TimeStateKindDto.AfterStage).Total;
        Assert.That(SumFy(struck), Is.EqualTo(160000).Within(0.001 * 160000));
        Assert.That(At(struck.ElementForces["e1"], 10).EndForce.Mz, Is.EqualTo(100000).Within(0.01 * 100000));
        var before = State(r, "odskružení", TimeStateKindDto.BeforeStage).Total;
        Assert.That(Math.Abs(before.SupportReactions["C"].Fx), Is.GreaterThan(1000), "falsework restrains the shrinkage before striking");
        Assert.That(before.NodeDisplacements["A"].Rz, Is.EqualTo(0), "falsework clamps the nodes before striking");

        // end: G0 + G1 on a structure that did not change its system → moments stay ≈ elastic, deformation grows
        var end = r.States.Single(s => s.Kind == TimeStateKindDto.End).Total;
        Assert.That(SumFy(end), Is.EqualTo(260000).Within(0.001 * 260000));
        Assert.That(At(end.ElementForces["e1"], 10).EndForce.Mz, Is.EqualTo(162500).Within(0.05 * 162500));
        var finishes = State(r, "ostatní stálé", TimeStateKindDto.AfterStage).Total;
        Assert.That(Math.Abs(end.NodeDisplacements["A"].Rz), Is.GreaterThan(1.5 * Math.Abs(finishes.NodeDisplacements["A"].Rz)), "creep");

        AssertEffectsSumToEnd(r);
        Assert.That(r.Effects.Count(e => e.Kind == EffectKindDto.Stage), Is.EqualTo(3));
        Assert.That(r.Histories.Times, Is.Ordered);
        Assert.That(r.Histories.Nodes["A"].Rz.Count, Is.EqualTo(r.Histories.Times.Count));
    }

    [Test]
    public void Prestressed_composite_girder_resultant_tendon_force_and_losses()
    {
        var response = Service().Analyze(TimeDependentContractTest.PrestressedCompositeGirder());
        Assert.That(response.Status, Is.EqualTo(AnalysisStatusDto.Successful), string.Join("\n", response.Errors));
        var r = response.TimeDependentResults!;
        const double p0 = 1400e6 * 0.0014;              // σp0 · Ap

        // in the form: the clamped ends hold the tendon force → resultant = tendon tension P at e = 0.4 m below the axis
        var inForm = State(r, "vnesení předpětí", TimeStateKindDto.BeforeStage).Total;
        Assert.That(At(inForm.ElementForces["nosnik"], 10).EndForce.Mz, Is.EqualTo(-0.4 * p0).Within(0.05 * 0.4 * p0));

        // released onto the bearings: the resultant of girder + tendon about the node axis carries only G0
        // (prestress is internal); the effect of the stage = G0 + release of the form (+0.4·P)
        var afterTransfer = State(r, "vnesení předpětí", TimeStateKindDto.AfterStage).Total;
        var mid = At(afterTransfer.ElementForces["nosnik"], 10);
        Assert.That(mid.EndForce.Mz, Is.EqualTo(-11250.0 * 400 / 8).Within(0.005 * 562500));
        Assert.That(mid.EndForce.Fx, Is.EqualTo(0).Within(2e-3 * p0));
        var transfer = r.Effects.Single(e => e.Stage == "vnesení předpětí").Result;
        Assert.That(At(transfer.ElementForces["nosnik"], 10).EndForce.Mz,
                    Is.EqualTo(mid.EndForce.Mz - At(inForm.ElementForces["nosnik"], 10).EndForce.Mz).Within(100));

        // tendon force: elastic loss at transfer, creep and shrinkage losses later
        var end = r.States.Single(s => s.Kind == TimeStateKindDto.End).Total;
        double nTransfer = -At(afterTransfer.PartForces!["nosnik"]["lano"], 10).EndForce.Fx;
        double nEnd = -At(end.PartForces!["nosnik"]["lano"], 10).EndForce.Fx;
        Assert.That(nTransfer, Is.InRange(0.85 * p0, 0.99 * p0), "elastic loss");
        Assert.That(nEnd, Is.InRange(0.6 * p0, 0.97 * nTransfer), "time-dependent losses");
        Assert.That(r.Histories.Elements["lano"].NStart[^1], Is.EqualTo(nEnd).Within(1e-3 * p0), "history: tension +");

        // statically determinate: creep, shrinkage and prestress leave the resultant = G0 + slab
        Assert.That(At(end.ElementForces["nosnik"], 10).EndForce.Mz, Is.EqualTo(-21250.0 * 400 / 8).Within(0.005 * 1062500));
        Assert.That(At(end.ElementForces["nosnik"], 10).EndForce.Fx, Is.EqualTo(0).Within(2e-3 * p0));
        Assert.That(SumFy(end), Is.EqualTo(425000).Within(0.001 * 425000));
        Assert.That(end.PartForces["nosnik"].Keys, Is.EquivalentTo(new[] { "nosnik", "lano", "deska" }));

        AssertEffectsSumToEnd(r);
    }
}
