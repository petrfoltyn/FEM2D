using FEALiTE2D.Api.Contracts.Enums;
using FEALiTE2D.Api.Contracts.Requests;
using FEALiTE2D.Api.Contracts.Validation;
using NUnit.Framework;

namespace FEALiTE2D.Tests.Api;

public class AnalysisRequestValidatorTest
{
    private static AnalysisRequest MinimalValidRequest() => new()
    {
        Nodes =
        {
            new NodeDto { Label = "n1", X = 0, Y = 0,
                          Support = new RigidSupportDto { Ux = true, Uy = true, Rz = true } },
            new NodeDto { Label = "n2", X = 5, Y = 0 }
        },
        Materials =
        {
            new MaterialDto { Label = "M1", MaterialType = MaterialTypeDto.Steel, E = 210e9, U = 0.3 }
        },
        Sections =
        {
            new RectangularSectionDto { Label = "S1", MaterialLabel = "M1", B = 0.2, T = 0.4 }
        },
        Elements =
        {
            new FrameElementDto { Label = "e1", StartNodeLabel = "n1", EndNodeLabel = "n2", SectionLabel = "S1" }
        },
        LoadCases =
        {
            new LoadCaseDto { Label = "LC1", LoadCaseType = LoadCaseTypeDto.Live }
        }
    };

    [Test]
    public void Minimal_request_passes_validation()
    {
        var result = new AnalysisRequestValidator().Validate(MinimalValidRequest());
        Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors));
    }

    [Test]
    public void Reports_missing_supports()
    {
        var req = MinimalValidRequest();
        req.Nodes[0].Support = null;

        var result = new AnalysisRequestValidator().Validate(req);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contains("no supports"));
    }

    [Test]
    public void Reports_unknown_section_reference()
    {
        var req = MinimalValidRequest();
        ((FrameElementDto)req.Elements[0]).SectionLabel = "DoesNotExist";

        var result = new AnalysisRequestValidator().Validate(req);
        Assert.That(result.Errors, Has.Some.Contains("'DoesNotExist'"));
    }

    [Test]
    public void Reports_duplicate_node_labels()
    {
        var req = MinimalValidRequest();
        req.Nodes.Add(new NodeDto { Label = "n1", X = 1, Y = 1 });

        var result = new AnalysisRequestValidator().Validate(req);
        Assert.That(result.Errors, Has.Some.Contains("duplicate label 'n1'"));
    }

    [Test]
    public void Reports_load_position_outside_element()
    {
        var req = MinimalValidRequest();
        req.LoadCases[0].ElementLoads.Add(
            new PointLoadDto { ElementLabel = "e1", Fy = -10, L1 = 999 });

        var result = new AnalysisRequestValidator().Validate(req);
        Assert.That(result.Errors, Has.Some.Contains("out of"));
    }

    [Test]
    public void Reports_unknown_loadCase_in_combination()
    {
        var req = MinimalValidRequest();
        req.LoadCombinations.Add(new LoadCombinationDto
        {
            Label = "ULS",
            Factors = { ["LC1"] = 1.35, ["NoSuchCase"] = 1.5 }
        });

        var result = new AnalysisRequestValidator().Validate(req);
        Assert.That(result.Errors, Has.Some.Contains("'NoSuchCase'"));
    }

    [Test]
    public void Reports_invalid_material_E_and_U()
    {
        var req = MinimalValidRequest();
        req.Materials[0].E = 0;
        req.Materials[0].U = 0.6;

        var result = new AnalysisRequestValidator().Validate(req);
        Assert.That(result.Errors, Has.Some.Contains("E must be greater than 0"));
        Assert.That(result.Errors, Has.Some.Contains("U must be in [0, 0.5)"));
    }
}
