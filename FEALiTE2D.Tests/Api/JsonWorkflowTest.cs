using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FEALiTE2D.Api.Contracts;
using FEALiTE2D.Api.Contracts.Enums;
using FEALiTE2D.Api.Contracts.Requests;
using FEALiTE2D.Api.Contracts.Responses;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace FEALiTE2D.Tests.Api;

/// <summary>
/// End-to-end: JSON fixture on disk → HTTP POST /api/v1/analyze → parsed
/// AnalysisResponse → asserts against reference values taken from the classic
/// domain-level test suites (<see cref="Structure.StructureTest"/> and
/// <see cref="Structure.PostProcessorTest"/>).
/// </summary>
public class JsonWorkflowTest
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // --------------------------------------------------------------------
    // StructureTest scenarios
    // --------------------------------------------------------------------

    [Test]
    public async Task TestStructure_classic_frame()
    {
        var lc = await PostExpectSuccess("classic-frame.json", "live");

        AssertDisplacement(lc.NodeDisplacements["n1"], 0, 0, 0);
        AssertDisplacement(lc.NodeDisplacements["n2"], 0.01, -0.005, -0.043633231299858237);
        AssertDisplacement(lc.NodeDisplacements["n3"], 0.26589188069505026, 0.00034194310937903029, -0.029312603676573);
        AssertDisplacement(lc.NodeDisplacements["n4"], 0.26621001441150061, -0.0052123431093790149, -0.021088130059077989);
        AssertDisplacement(lc.NodeDisplacements["n5"], 0.27076612292955626, 0.00065149930914949782, 0.019752367356605849);

        AssertForce(lc.SupportReactions["n1"], -182.36325573226503, -128.22866601713636, 497.44001602057028);
        AssertForce(lc.SupportReactions["n2"], -49.636744267753542,  79.628666017130627,  94.801989825388063);
    }

    [Test]
    public async Task TestStructure2_steel_frame_kip_inch()
    {
        var lc = await PostExpectSuccess("steel-frame-kip-inch.json", "live");

        AssertDisplacement(lc.NodeDisplacements["n1"], 0, 0, 0);
        AssertDisplacement(lc.NodeDisplacements["n3"], 0, 0, 0);
        AssertDisplacement(lc.NodeDisplacements["n2"], 0.021301404056102882, -0.06732180013884803, -0.0025498997289483097);

        AssertForce(lc.SupportReactions["n1"],  30.37225194999335, 102.08675797670341,  1215.9664523968904);
        AssertForce(lc.SupportReactions["n3"], -30.37225194999336,  17.913242023296597, -854.0740487820697);
        Assert.That(lc.SupportReactions.ContainsKey("n2"), Is.False);
    }

    [Test]
    public async Task TestStructure2_with_settlement()
    {
        var lc = await PostExpectSuccess("steel-frame-kip-inch-settlement.json", "live");

        AssertDisplacement(lc.NodeDisplacements["n1"], 0, -1, 0);
        AssertDisplacement(lc.NodeDisplacements["n3"], 0,  0, 0);
        AssertDisplacement(lc.NodeDisplacements["n2"], 0.017760708393401745, -1.059915467868475, 0.00074191638715062017);
    }

    [Test]
    public async Task TestContinousBeam()
    {
        var lc = await PostExpectSuccess("continuous-beam.json", "live");

        AssertDisplacement(lc.NodeDisplacements["n1"], 0, 0, 0);
        AssertDisplacement(lc.NodeDisplacements["n2"], 0, -0.0044728627208516182, 0.00056143270452086369);
        AssertDisplacement(lc.NodeDisplacements["n3"], 0, 0, -0.0006841653841759686);
        AssertDisplacement(lc.NodeDisplacements["n4"], 0, 0,  0.0032284743177037486);

        AssertForce(lc.SupportReactions["n1"], 0, 146.32690995907231, 281.18656207366985);
        Assert.That(lc.SupportReactions.ContainsKey("n2"), Is.False);
        AssertForce(lc.SupportReactions["n3"], 0, 243.46483628922235, 0);
        AssertForce(lc.SupportReactions["n4"], 0,  50.208253751705314, 0);
    }

    [Test]
    public async Task TestInternalForces_hinged_beam()
    {
        var lc = await PostExpectSuccess("hinged-beam-internal-forces.json", "live");

        // Mesh inserts a breakpoint at the trapezoidal-load edge (x = 1.35),
        // so the base 10-segment mesh expands to 11. Indices still match the
        // original StructureTest.TestInternalForces assertions.
        var e1 = lc.ElementForces["e1"];
        Assert.That(e1, Has.Count.EqualTo(11));

        Assert.That(e1[0].StartForce.Fy, Is.EqualTo(24.553854166666675).Within(1e-6));
        Assert.That(e1[0].EndForce.Fy,   Is.EqualTo(24.553854166666675).Within(1e-6));
        Assert.That(e1[3].EndForce.Fy,   Is.EqualTo(22.553511700913251).Within(1e-6));
        Assert.That(e1[4].StartForce.Fy, Is.EqualTo(22.553511700913251).Within(1e-6));
        Assert.That(e1[4].EndForce.Fy,   Is.EqualTo(16.241867865296811).Within(1e-6));
        Assert.That(e1[6].EndForce.Fy,   Is.EqualTo( 5.2624158105022882).Within(1e-6));

        Assert.That(e1[0].StartForce.Mz, Is.EqualTo(0).Within(1e-8));
        Assert.That(e1[0].EndForce.Mz,   Is.EqualTo(-12.276927083333323).Within(1e-6));
        Assert.That(e1[5].EndForce.Mz,   Is.EqualTo(-53.013331192922386).Within(1e-6));
        Assert.That(e1[6].EndForce.Mz,   Is.EqualTo(-56.925646404109592).Within(1e-6));
    }

    [Test]
    public async Task TestSprings_planar()
    {
        var lc = await PostExpectSuccess("springs-planar.json", "live");

        AssertForce(lc.SupportReactions["n1"],   0,  0, 0);
        AssertForce(lc.SupportReactions["n2"],  50, 50, 0);
        AssertForce(lc.SupportReactions["n3"], -50, 50, 0);
        AssertForce(lc.SupportReactions["n4"],   0,  0, 0);

        AssertDisplacement(lc.NodeDisplacements["n1"], 0, 0, 0);
        AssertDisplacement(lc.NodeDisplacements["n2"], 0, 0, 0);
        AssertDisplacement(lc.NodeDisplacements["n3"], 0, 0, 0);
        AssertDisplacement(lc.NodeDisplacements["n4"], 0, -0.10, 0);
    }

    [Test]
    public async Task TestSprings_series()
    {
        var lc = await PostExpectSuccess("springs-series.json", "live");

        AssertForce(lc.SupportReactions["n1"], -2.5, 0, 0);
        AssertForce(lc.SupportReactions["n5"], -2.5, 0, 0);

        AssertDisplacement(lc.NodeDisplacements["n2"], 0.125, 0, 0);
        AssertDisplacement(lc.NodeDisplacements["n3"], 0.25,  0, 0);
        AssertDisplacement(lc.NodeDisplacements["n4"], 0.125, 0, 0);
    }

    [Test]
    public async Task TestBeamWithEndRelease()
    {
        var lc = await PostExpectSuccess("beam-start-release.json", "live");

        AssertForce(lc.SupportReactions["n1"], 0, 5.25, 0);
        AssertForce(lc.SupportReactions["n2"], 0, 8.25, 0);
        AssertForce(lc.SupportReactions["n3"], 0, 3.00, 0);

        Assert.That(lc.NodeDisplacements["n1"].Rz, Is.EqualTo(-1.49333333333e-5).Within(1e-5));
        Assert.That(lc.NodeDisplacements["n2"].Rz, Is.EqualTo( 1.49333333333e-5).Within(1e-5));
        Assert.That(lc.NodeDisplacements["n3"].Rz, Is.EqualTo( 2.27555555557e-5).Within(1e-5));
    }

    [Test]
    public async Task TestTrussWithSpringElementAsSupport()
    {
        var lc = await PostExpectSuccess("truss-spring-support.json", "ll");

        AssertForce(lc.SupportReactions["n2"], -36.20689655172414,  36.20689655172414, 0);
        AssertForce(lc.SupportReactions["n3"],  36.206896551724135, 0,                 0);
        AssertForce(lc.SupportReactions["n4"],   0,                 13.793103448275861, 0);

        Assert.That(lc.NodeDisplacements["n1"].Ux, Is.EqualTo(-0.00344827586206897).Within(1e-5));
        Assert.That(lc.NodeDisplacements["n1"].Uy, Is.EqualTo(-0.00689655172413793).Within(1e-5));
    }

    [Test]
    public async Task TestBeamWithEndReleaseAndSupportsAsSprings()
    {
        var lc = await PostExpectSuccess("beam-release-springs-as-supports.json", "live");

        AssertForce(lc.SupportReactions["n4"], 0, 5.25, 0);
        AssertForce(lc.SupportReactions["n5"], 0, 8.25, 0);
        AssertForce(lc.SupportReactions["n6"], 0, 3.00, 0);

        Assert.That(lc.NodeDisplacements["n1"].Rz, Is.EqualTo(-1.49333333333e-5).Within(1e-5));
        Assert.That(lc.NodeDisplacements["n2"].Rz, Is.EqualTo( 1.49333333333e-5).Within(1e-5));
        Assert.That(lc.NodeDisplacements["n3"].Rz, Is.EqualTo( 2.27555555557e-5).Within(1e-5));
    }

    [Test]
    public async Task TestBeamWithEndReleaseAndNodalSpringSupports()
    {
        var lc = await PostExpectSuccess("beam-nodal-spring-supports.json", "live");

        AssertForce(lc.SupportReactions["n1"], 0, 5.25, 0);
        AssertForce(lc.SupportReactions["n2"], 0, 8.25, 0);
        AssertForce(lc.SupportReactions["n3"], 0, 3.00, 0);

        Assert.That(lc.NodeDisplacements["n1"].Rz, Is.EqualTo(-1.49333333333e-5).Within(1e-5));
        Assert.That(lc.NodeDisplacements["n2"].Rz, Is.EqualTo( 1.49333333333e-5).Within(1e-5));
        Assert.That(lc.NodeDisplacements["n3"].Rz, Is.EqualTo( 2.27555555557e-5).Within(1e-5));
    }

    [Test]
    public async Task TestStructureWithLoadCombination()
    {
        var body = await PostExpectSuccess("frame-load-combination.json");

        var live = body.LoadCaseResults["Live"];
        var dead = body.LoadCaseResults["Dead"];
        var uls  = body.LoadCombinationResults["uls"];

        AssertForce(live.SupportReactions["n1"], -1.0545205, 30,  0.67707616, 1e-5);
        AssertForce(live.SupportReactions["n4"],  1.0545205, 30, -0.6770761,  1e-5);

        AssertForce(dead.SupportReactions["n1"], -10.149307, -3.0919293, 15.5190549, 1e-5);
        AssertForce(dead.SupportReactions["n4"],  -9.8506923,  3.09192933, 15.2051570, 1e-5);

        AssertForce(uls.SupportReactions["n1"],
            -1.0545205 * 1.5 + 1.35 * -10.149307,
             30 * 1.5        + 1.35 * -3.0919293,
             0.67707616 * 1.5 + 1.35 *  15.5190549, 1e-5);
        AssertForce(uls.SupportReactions["n4"],
             1.0545205 * 1.5 + 1.35 * -9.8506923,
             30 * 1.5        + 1.35 *  3.09192933,
            -0.6770761 * 1.5 + 1.35 *  15.2051570, 1e-5);
    }

    // --------------------------------------------------------------------
    // PostProcessorTest scenarios — check segment-boundary forces
    // --------------------------------------------------------------------

    [Test]
    public async Task PostProcessor_uniform_full_span()
    {
        var lc = await PostExpectSuccess("pp-uniform-full.json", "General");
        var e1 = lc.ElementForces["e1"];

        // Default MeshSegments = 10 → seg[0].Start is at x=0, seg[last].End at x=L.
        var first = e1[0];
        var last  = e1[^1];

        Assert.That(first.StartForce.Fx, Is.EqualTo(0).Within(1e-3));
        Assert.That(first.StartForce.Fy, Is.EqualTo(0.5).Within(1e-3));
        Assert.That(first.StartForce.Mz, Is.EqualTo(0).Within(1e-3));

        Assert.That(last.EndForce.Fx, Is.EqualTo(0).Within(1e-3));
        Assert.That(last.EndForce.Fy, Is.EqualTo(-0.5).Within(1e-3));
        Assert.That(last.EndForce.Mz, Is.EqualTo(0).Within(1e-3));
    }

    [Test]
    public async Task PostProcessor_uniform_partial_edge_segment()
    {
        var lc = await PostExpectSuccess("pp-uniform-partial-edge.json", "General");
        var e1 = lc.ElementForces["e1"];

        // LinearMesher inserts extra breakpoints at the partial-load edges,
        // so the base 4-segment mesh grows to 6. Last segment always ends at L.
        var last = e1[^1];
        Assert.That(last.X2, Is.EqualTo(1.0).Within(1e-9), "last segment must end at L=1");

        Assert.That(last.EndForce.Fx, Is.EqualTo(0).Within(1e-3));
        Assert.That(last.EndForce.Fy, Is.EqualTo(0.0737).Within(1e-3));
        Assert.That(last.EndForce.Mz, Is.EqualTo(0).Within(1e-3));
    }

    [Test]
    public async Task PostProcessor_uniform_partial_center()
    {
        var lc = await PostExpectSuccess("pp-uniform-partial-center.json", "General");
        var e1 = lc.ElementForces["e1"];

        // Symmetric partial load → V(x=0.5) = 0, M(x=0.5) = -Mmax = -0.09375.
        // Index of midspan varies with how many breakpoints the mesher inserts,
        // so locate the segment ending exactly at x = 0.5.
        var midEnd = FindSegmentEndingAt(e1, 0.5);

        Assert.That(midEnd.EndForce.Fy, Is.EqualTo(0).Within(1e-3));
        Assert.That(midEnd.EndForce.Mz, Is.EqualTo(-0.09375).Within(1e-3));
    }

    [Test]
    public async Task PostProcessor_trapezoidal_full_span()
    {
        var lc = await PostExpectSuccess("pp-trapezoidal-full.json", "General");
        var e1 = lc.ElementForces["e1"];

        // Closed-form (triangular Wy2=-1 over L=1):
        //   W=|w|*L/2=0.5;  Vx=W/3 - W*x²/L² = 1/6 - 1/8 = 1/24 ≈ 0.041666…
        //   Mx=W*x/(3L²) * (L²-x²) = (0.25/3) * 0.75 = 0.0625
        var v = 1.0 / 24.0;
        var m = 0.0625;
        var midEnd = FindSegmentEndingAt(e1, 0.5).EndForce;

        Assert.That(midEnd.Fx, Is.EqualTo(0).Within(1e-3));
        Assert.That(midEnd.Fy, Is.EqualTo(v).Within(1e-3));
        Assert.That(midEnd.Mz, Is.EqualTo(-m).Within(1e-3));
    }

    // --------------------------------------------------------------------
    // Helpers
    // --------------------------------------------------------------------

    private async Task<CaseResultDto> PostExpectSuccess(string fixtureFile, string loadCaseLabel)
    {
        var body = await PostExpectSuccess(fixtureFile);
        Assert.That(body.LoadCaseResults.ContainsKey(loadCaseLabel),
            $"Load case '{loadCaseLabel}' missing from response of {fixtureFile}.");
        return body.LoadCaseResults[loadCaseLabel];
    }

    private async Task<AnalysisResponse> PostExpectSuccess(string fixtureFile)
    {
        var request = LoadFixture(fixtureFile);

        using var http = await _client.PostAsJsonAsync("/api/v1/analyze", request, ApiJsonOptions.Default);
        var raw = await http.Content.ReadAsStringAsync();
        Assert.That(http.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"{fixtureFile} → {(int)http.StatusCode} {http.StatusCode}: {raw}");

        var body = System.Text.Json.JsonSerializer.Deserialize<AnalysisResponse>(raw, ApiJsonOptions.Default);
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Status, Is.EqualTo(AnalysisStatusDto.Successful),
            $"{fixtureFile} returned status {body.Status}: {string.Join("; ", body.Errors)}");
        Assert.That(body.Errors, Is.Empty);
        return body;
    }

    private static SegmentDto FindSegmentEndingAt(System.Collections.Generic.List<SegmentDto> segments, double x, double tol = 1e-6)
    {
        foreach (var s in segments)
            if (System.Math.Abs(s.X2 - x) < tol) return s;
        Assert.Fail($"No segment ending at x={x}. Segments: [{string.Join(", ", segments.ConvertAll(s => $"({s.X1}..{s.X2})"))}]");
        return null!;
    }

    private static AnalysisRequest LoadFixture(string fileName)
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Api", "Fixtures", fileName);
        Assert.That(File.Exists(path), $"Fixture not found: {path}");

        using var stream = File.OpenRead(path);
        var request = System.Text.Json.JsonSerializer.Deserialize<AnalysisRequest>(stream, ApiJsonOptions.Default);
        Assert.That(request, Is.Not.Null, $"Fixture {fileName} failed to deserialize.");
        return request!;
    }

    private static void AssertDisplacement(DisplacementDto d,
        double ux, double uy, double rz, double tol = 1e-9)
    {
        Assert.That(d.Ux, Is.EqualTo(ux).Within(tol), "Ux");
        Assert.That(d.Uy, Is.EqualTo(uy).Within(tol), "Uy");
        Assert.That(d.Rz, Is.EqualTo(rz).Within(tol), "Rz");
    }

    private static void AssertForce(ForceDto f,
        double fx, double fy, double mz, double tol = 1e-6)
    {
        Assert.That(f.Fx, Is.EqualTo(fx).Within(tol), "Fx");
        Assert.That(f.Fy, Is.EqualTo(fy).Within(tol), "Fy");
        Assert.That(f.Mz, Is.EqualTo(mz).Within(tol), "Mz");
    }
}
