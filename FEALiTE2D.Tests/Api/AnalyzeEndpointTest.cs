using System;
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

public class AnalyzeEndpointTest
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

    [Test]
    public async Task Health_returns_ok()
    {
        var response = await _client.GetAsync("/health");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Analyze_simply_supported_beam_returns_expected_midspan_moment()
    {
        // 6 m simply supported beam, uniform 10 kN/m → M_max = wL²/8 = 45 kNm
        var request = new AnalysisRequest
        {
            Nodes =
            {
                new NodeDto { Label = "n1", X = 0, Y = 0,
                              Support = new RigidSupportDto { Ux = true, Uy = true, Rz = false } },
                new NodeDto { Label = "n2", X = 6, Y = 0,
                              Support = new RigidSupportDto { Ux = false, Uy = true, Rz = false } }
            },
            Materials =
            {
                new MaterialDto { Label = "M", MaterialType = MaterialTypeDto.Steel, E = 210e9, U = 0.3 }
            },
            Sections =
            {
                new RectangularSectionDto { Label = "S", MaterialLabel = "M", B = 0.2, T = 0.4 }
            },
            Elements =
            {
                new FrameElementDto { Label = "e", StartNodeLabel = "n1", EndNodeLabel = "n2", SectionLabel = "S" }
            },
            LoadCases =
            {
                new LoadCaseDto
                {
                    Label = "LC",
                    LoadCaseType = LoadCaseTypeDto.Live,
                    ElementLoads = { new UniformLoadDto { ElementLabel = "e", Wy = -10, Direction = LoadDirectionDto.Global } }
                }
            },
            Settings = new SettingsDto { MeshSegments = 20 }
        };

        var http = await _client.PostAsJsonAsync("/api/v1/analyze", request, ApiJsonOptions.Default);
        Assert.That(http.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await http.Content.ReadFromJsonAsync<AnalysisResponse>(ApiJsonOptions.Default);
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Status, Is.EqualTo(AnalysisStatusDto.Successful));

        var lc = body.LoadCaseResults["LC"];

        // Reactions: each support carries half the load → 30 kN upward.
        Assert.That(lc.SupportReactions["n1"].Fy, Is.EqualTo(30).Within(1e-6));
        Assert.That(lc.SupportReactions["n2"].Fy, Is.EqualTo(30).Within(1e-6));

        // M_max somewhere in the segment list close to wL²/8 = 45 kNm.
        double mMax = 0;
        foreach (var seg in lc.ElementForces["e"])
        {
            mMax = Math.Max(mMax, Math.Abs(seg.StartForce.Mz));
            mMax = Math.Max(mMax, Math.Abs(seg.EndForce.Mz));
        }
        Assert.That(mMax, Is.EqualTo(45).Within(0.1));
    }

    [Test]
    public async Task Analyze_invalid_request_returns_422_with_errors()
    {
        var request = new AnalysisRequest
        {
            Nodes = { new NodeDto { Label = "n1" } }, // no support, no element, no LC
        };

        var http = await _client.PostAsJsonAsync("/api/v1/analyze", request, ApiJsonOptions.Default);
        Assert.That(http.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));

        var body = await http.Content.ReadFromJsonAsync<AnalysisResponse>(ApiJsonOptions.Default);
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Status, Is.EqualTo(AnalysisStatusDto.Failure));
        Assert.That(body.Errors, Is.Not.Empty);
    }
}
