using System;
using System.Net.Http;
using System.Threading.Tasks;
using FEALiTE2D.Api.Contracts.Enums;
using FEALiTE2D.Api.Contracts.Requests;
using FEALiTE2D.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FEALiTE2D.Tests.Api;

public class FealiteApiClientTest
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _http = null!;
    private FealiteApiClient _client = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>();
        _http = _factory.CreateClient();
        _client = new FealiteApiClient(_http);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private static AnalysisRequest SimpleBeam() => new()
    {
        Nodes =
        {
            new NodeDto { Label = "n1", X = 0, Y = 0,
                          Support = new RigidSupportDto { Ux = true, Uy = true, Rz = false } },
            new NodeDto { Label = "n2", X = 6, Y = 0,
                          Support = new RigidSupportDto { Ux = false, Uy = true, Rz = false } }
        },
        Materials = { new MaterialDto { Label = "M", MaterialType = MaterialTypeDto.Steel, E = 210e9, U = 0.3 } },
        Sections = { new RectangularSectionDto { Label = "S", MaterialLabel = "M", B = 0.2, T = 0.4 } },
        Elements = { new FrameElementDto { Label = "e", StartNodeLabel = "n1", EndNodeLabel = "n2", SectionLabel = "S" } },
        LoadCases =
        {
            new LoadCaseDto
            {
                Label = "LC", LoadCaseType = LoadCaseTypeDto.Live,
                ElementLoads = { new UniformLoadDto { ElementLabel = "e", Wy = -10 } }
            }
        }
    };

    [Test]
    public async Task HealthAsync_returns_true_when_server_running()
    {
        Assert.That(await _client.HealthAsync(), Is.True);
    }

    [Test]
    public async Task AnalyzeAsync_returns_successful_response_for_valid_model()
    {
        var response = await _client.AnalyzeAsync(SimpleBeam());

        Assert.That(response.Status, Is.EqualTo(AnalysisStatusDto.Successful));
        Assert.That(response.Errors, Is.Empty);
        Assert.That(response.LoadCaseResults["LC"].SupportReactions["n1"].Fy, Is.EqualTo(30).Within(1e-6));
    }

    [Test]
    public async Task AnalyzeAsync_returns_failure_response_for_invalid_model()
    {
        var bad = new AnalysisRequest { Nodes = { new NodeDto { Label = "n1" } } };

        var response = await _client.AnalyzeAsync(bad);

        Assert.That(response.Status, Is.EqualTo(AnalysisStatusDto.Failure));
        Assert.That(response.Errors, Is.Not.Empty);
    }

    [Test]
    public void Service_collection_extension_resolves_typed_client()
    {
        var services = new ServiceCollection();
        services.AddFealiteApiClient(o => o.BaseAddress = new Uri("http://localhost:5180/"));
        using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<FealiteApiClient>();
        Assert.That(client, Is.Not.Null);
    }
}
