using System.Text.Json;
using FEALiTE2D.Api.Contracts;
using FEALiTE2D.Api.Contracts.Enums;
using FEALiTE2D.Api.Contracts.Mapping;
using FEALiTE2D.Api.Contracts.Requests;
using FEALiTE2D.Api.Contracts.Responses;
using NUnit.Framework;

namespace FEALiTE2D.Tests.Api;

/// <summary>
/// Re-implements <see cref="Structure.StructureTest.TestStructure"/> using the
/// API DTO surface — proves the DTO contract is rich enough to drive the
/// existing solver and that JSON round-tripping preserves every input.
/// </summary>
public class AnalysisRoundTripTest
{
    private static AnalysisRequest BuildClassicFrameRequest()
    {
        return new AnalysisRequest
        {
            Nodes =
            {
                new NodeDto { Label = "n1", X = 0, Y = 0,
                              Support = new RigidSupportDto { Ux = true, Uy = true, Rz = true } },
                new NodeDto { Label = "n2", X = 9, Y = 0,
                              Support = new RigidSupportDto { Ux = true, Uy = true, Rz = true } },
                new NodeDto { Label = "n3", X = 0, Y = 6 },
                new NodeDto { Label = "n4", X = 9, Y = 6 },
                new NodeDto { Label = "n5", X = 0, Y = 12 }
            },
            Materials =
            {
                new MaterialDto { Label = "Steel", MaterialType = MaterialTypeDto.Steel,
                                  E = 30E6, U = 0.2, Alpha = 0.000012, Gama = 39885 }
            },
            Sections =
            {
                new GenericSectionDto
                {
                    Label = "S1", MaterialLabel = "Steel",
                    A = 0.075, Az = 0.075, Ay = 0.075,
                    Iz = 0.000480, Iy = 0.000480, J = 0.000480 * 2,
                    MaxHeight = 0.1, MaxWidth = 0.1
                }
            },
            Elements =
            {
                new FrameElementDto { Label = "e1", StartNodeLabel = "n1", EndNodeLabel = "n3", SectionLabel = "S1" },
                new FrameElementDto { Label = "e2", StartNodeLabel = "n2", EndNodeLabel = "n4", SectionLabel = "S1" },
                new FrameElementDto { Label = "e3", StartNodeLabel = "n3", EndNodeLabel = "n5", SectionLabel = "S1" },
                new FrameElementDto { Label = "e4", StartNodeLabel = "n3", EndNodeLabel = "n4", SectionLabel = "S1" },
                new FrameElementDto { Label = "e5", StartNodeLabel = "n4", EndNodeLabel = "n5", SectionLabel = "S1" }
            },
            LoadCases =
            {
                new LoadCaseDto
                {
                    Label = "live",
                    LoadCaseType = LoadCaseTypeDto.Live,
                    NodalLoads =
                    {
                        new NodalLoadDto { NodeLabel = "n1", Fx = 40 },
                        new NodalLoadDto { NodeLabel = "n3", Fx = 80 },
                        new NodalLoadDto { NodeLabel = "n5", Fx = 40 }
                    },
                    SupportDisplacements =
                    {
                        new SupportDisplacementDto
                        {
                            NodeLabel = "n2",
                            Ux = 10E-3, Uy = -5E-3,
                            Rz = -2.5 * System.Math.PI / 180
                        }
                    },
                    ElementLoads =
                    {
                        new PointLoadDto { ElementLabel = "e3", Mz = 7.5, L1 = 3.0 },
                        new TrapezoidalLoadDto { ElementLabel = "e4", Wy1 = -15, Wy2 = -7, L1 = 0.9, L2 = 2.7 },
                        new UniformLoadDto { ElementLabel = "e5", Wy = -12, Direction = LoadDirectionDto.Local }
                    }
                }
            },
            Settings = new SettingsDto { MeshSegments = 35 }
        };
    }

    [Test]
    public void AnalysisService_matches_StructureTest_displacements_and_reactions()
    {
        var request = BuildClassicFrameRequest();
        var response = new AnalysisService().Analyze(request);

        Assert.That(response.Status, Is.EqualTo(AnalysisStatusDto.Successful));
        Assert.That(response.Errors, Is.Empty);

        var lc = response.LoadCaseResults["live"];

        // Reference values copied from Structure.StructureTest.TestStructure
        AssertDisplacement(lc.NodeDisplacements["n1"], 0, 0, 0);
        AssertDisplacement(lc.NodeDisplacements["n2"], 0.01, -0.005, -0.043633231299858237);
        AssertDisplacement(lc.NodeDisplacements["n3"], 0.26589188069505026, 0.00034194310937903029, -0.029312603676573);
        AssertDisplacement(lc.NodeDisplacements["n4"], 0.26621001441150061, -0.0052123431093790149, -0.021088130059077989);
        AssertDisplacement(lc.NodeDisplacements["n5"], 0.27076612292955626, 0.00065149930914949782, 0.019752367356605849);

        AssertForce(lc.SupportReactions["n1"], -182.36325573226503, -128.22866601713636, 497.44001602057028);
        AssertForce(lc.SupportReactions["n2"], -49.636744267753542, 79.628666017130627, 94.801989825388063);

        Assert.That(lc.ElementForces, Has.Count.EqualTo(5));
        Assert.That(lc.ElementForces["e1"], Is.Not.Empty);
    }

    [Test]
    public void AnalysisRequest_round_trips_through_json_with_polymorphic_DTOs()
    {
        var original = BuildClassicFrameRequest();

        var json = JsonSerializer.Serialize(original, ApiJsonOptions.Default);
        var parsed = JsonSerializer.Deserialize<AnalysisRequest>(json, ApiJsonOptions.Default);

        Assert.That(parsed, Is.Not.Null);

        // Polymorphism survived the trip.
        Assert.That(parsed!.Nodes[0].Support, Is.TypeOf<RigidSupportDto>());
        Assert.That(parsed.Sections[0], Is.TypeOf<GenericSectionDto>());
        Assert.That(parsed.Elements[0], Is.TypeOf<FrameElementDto>());

        var lc = parsed.LoadCases[0];
        Assert.That(lc.ElementLoads[0], Is.TypeOf<PointLoadDto>());
        Assert.That(lc.ElementLoads[1], Is.TypeOf<TrapezoidalLoadDto>());
        Assert.That(lc.ElementLoads[2], Is.TypeOf<UniformLoadDto>());

        // Solving the deserialized request produces the same support reaction at n1.
        var response = new AnalysisService().Analyze(parsed);
        Assert.That(response.Status, Is.EqualTo(AnalysisStatusDto.Successful));
        AssertForce(response.LoadCaseResults["live"].SupportReactions["n1"],
            -182.36325573226503, -128.22866601713636, 497.44001602057028);
    }

    [Test]
    public void Polymorphic_discriminator_can_appear_anywhere_in_the_object()
    {
        // Hand-crafted JSON where "type" is NOT the first property — must succeed
        // thanks to AllowOutOfOrderMetadataProperties (.NET 9+).
        const string json = """
        {
          "nodes": [
            { "label": "n1", "x": 0, "y": 0,
              "support": { "ux": true, "uy": true, "rz": true, "type": "Rigid" } },
            { "label": "n2", "x": 5, "y": 0 }
          ],
          "materials": [
            { "label": "M", "materialType": "Steel", "E": 210000000000, "U": 0.3 }
          ],
          "sections": [
            { "label": "S", "materialLabel": "M", "b": 0.2, "t": 0.4, "type": "Rectangular" }
          ],
          "elements": [
            { "label": "e", "startNodeLabel": "n1", "endNodeLabel": "n2",
              "sectionLabel": "S", "type": "Frame" }
          ],
          "loadCases": [
            {
              "label": "LC", "loadCaseType": "Live",
              "elementLoads": [
                { "elementLabel": "e", "wy": -10, "direction": "Global", "type": "Uniform" }
              ]
            }
          ]
        }
        """;

        var request = JsonSerializer.Deserialize<AnalysisRequest>(json, ApiJsonOptions.Default);
        Assert.That(request, Is.Not.Null);
        Assert.That(request!.Sections[0], Is.TypeOf<RectangularSectionDto>());
        Assert.That(request.Elements[0], Is.TypeOf<FrameElementDto>());
        Assert.That(request.LoadCases[0].ElementLoads[0], Is.TypeOf<UniformLoadDto>());

        var response = new AnalysisService().Analyze(request);
        Assert.That(response.Status, Is.EqualTo(AnalysisStatusDto.Successful));
    }

    [Test]
    public void Json_payload_uses_camelCase_with_uppercase_physical_quantities()
    {
        var json = JsonSerializer.Serialize(BuildClassicFrameRequest(), ApiJsonOptions.Default);

        // camelCase wins by default.
        Assert.That(json, Does.Contain("\"label\":\"n1\""));
        Assert.That(json, Does.Contain("\"startNodeLabel\":\"n1\""));
        // ...but physical-quantity property names stay uppercase.
        Assert.That(json, Does.Contain("\"E\":30000000"));
        Assert.That(json, Does.Contain("\"Iz\":0.00048"));
        // Discriminator written in PascalCase.
        Assert.That(json, Does.Contain("\"type\":\"Rigid\""));
        Assert.That(json, Does.Contain("\"type\":\"Frame\""));
        Assert.That(json, Does.Contain("\"type\":\"Generic\""));
        Assert.That(json, Does.Contain("\"type\":\"Uniform\""));
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
