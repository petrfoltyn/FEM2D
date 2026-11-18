using System.Linq;
using System.Text.Json;
using FEALiTE2D.Api.Contracts;
using FEALiTE2D.Api.Contracts.Enums;
using FEALiTE2D.Api.Contracts.Mapping;
using FEALiTE2D.Api.Contracts.Requests;
using FEALiTE2D.Api.Contracts.Validation;
using NUnit.Framework;

namespace FEALiTE2D.Tests.Api;

/// <summary>
/// Time-dependent extension of the request (stages, creep, shrinkage): the same structure drives FEALiTE2D
/// (instantaneous response at 28 days, extension ignored) and a time-dependent solver (TDA).
/// </summary>
public class TimeDependentContractTest
{
    /// <summary>Two-span continuous concrete beam 2 × 10 m cast on falsework, struck at 28 days, finishes at 60 days.</summary>
    internal static AnalysisRequest StagedBeam() => new()
    {
        Nodes =
        {
            new NodeDto { Label = "A", X = 0, Y = 0, Support = new RigidSupportDto { Ux = true, Uy = true } },
            new NodeDto { Label = "B", X = 10, Y = 0, Support = new RigidSupportDto { Uy = true } },
            new NodeDto { Label = "C", X = 20, Y = 0, Support = new RigidSupportDto { Uy = true } }
        },
        Materials =
        {
            new MaterialDto { Label = "C30/37", MaterialType = MaterialTypeDto.Concrete, E = 33e9, U = 0.2, Alpha = 1e-5, Gama = 25000 }
        },
        Sections = { new RectangularSectionDto { Label = "R", MaterialLabel = "C30/37", B = 0.4, T = 0.8 } },
        Elements =
        {
            new FrameElementDto { Label = "e1", StartNodeLabel = "A", EndNodeLabel = "B", SectionLabel = "R" },
            new FrameElementDto { Label = "e2", StartNodeLabel = "B", EndNodeLabel = "C", SectionLabel = "R" }
        },
        LoadCases =
        {
            new LoadCaseDto
            {
                Label = "G0", LoadCaseType = LoadCaseTypeDto.SelfWeight,
                ElementLoads =
                {
                    new UniformLoadDto { ElementLabel = "e1", Wy = -8000 },
                    new UniformLoadDto { ElementLabel = "e2", Wy = -8000 }
                }
            },
            new LoadCaseDto
            {
                Label = "G1", LoadCaseType = LoadCaseTypeDto.Dead,
                ElementLoads =
                {
                    new UniformLoadDto { ElementLabel = "e1", Wy = -5000 },
                    new UniformLoadDto { ElementLabel = "e2", Wy = -5000 }
                }
            },
            new LoadCaseDto
            {
                Label = "Q", LoadCaseType = LoadCaseTypeDto.Live, LoadCaseDuration = LoadCaseDurationDto.ShortTerm,
                ElementLoads = { new UniformLoadDto { ElementLabel = "e1", Wy = -10000 } }
            }
        },
        TimeDependent = new TimeDependentDto
        {
            RheologyModel = RheologyModelDto.En1992_2023,
            RelativeHumidity = 0.7,
            AnalysisEnd = 36500,
            OutputTimes = { 28, 60, 36500 },
            Concretes = { new ConcreteRheologyDto { MaterialLabel = "C30/37", Fck = 30e6, CementClass = CementClassDto.N } },
            Sections = { new SectionDryingDto { SectionLabel = "R", PerimeterExposed = 2.0 } },
            Elements = { new ElementAgeDto { ElementLabel = "e1", CuringDuration = 7 } },
            Stages =
            {
                new ConstructionStageDto
                {
                    Label = "betonáž", Time = 0, ActivateElements = { "e1", "e2" },
                    AddSupports =
                    {
                        new StageSupportDto { NodeLabel = "A", Ux = true, Uy = true, Rz = true },
                        new StageSupportDto { NodeLabel = "B", Ux = true, Uy = true, Rz = true },
                        new StageSupportDto { NodeLabel = "C", Ux = true, Uy = true, Rz = true }
                    }
                },
                new ConstructionStageDto
                {
                    Label = "odskružení", Time = 28, LoadCases = { "G0" },
                    RemoveSupports =
                    {
                        new StageSupportDto { NodeLabel = "A", Rz = true },
                        new StageSupportDto { NodeLabel = "B", Ux = true, Rz = true },
                        new StageSupportDto { NodeLabel = "C", Ux = true, Rz = true }
                    }
                },
                new ConstructionStageDto { Label = "ostatní stálé", Time = 60, LoadCases = { "G1" } }
            }
        }
    };

    /// <summary>
    /// Pretensioned girder 20 m with a composite slab: FEALiTE2D sees one girder element on two bearings, TDA in
    /// addition a tendon 0.4 m below and a slab 0.6 m above the node axis (girder centroid). Cast at day 0 in a
    /// form that holds both ends (pretensioned strands), released onto the bearings at day 3, slab cast at day 60.
    /// </summary>
    internal static AnalysisRequest PrestressedCompositeGirder() => new()
    {
        Nodes =
        {
            new NodeDto { Label = "A", X = 0, Y = 0, Support = new RigidSupportDto { Ux = true, Uy = true } },
            new NodeDto { Label = "B", X = 20, Y = 0, Support = new RigidSupportDto { Uy = true } }
        },
        Materials =
        {
            new MaterialDto { Label = "C50/60", MaterialType = MaterialTypeDto.Concrete, E = 37e9, U = 0.2, Alpha = 1e-5, Gama = 25000 },
            new MaterialDto { Label = "C30/37", MaterialType = MaterialTypeDto.Concrete, E = 33e9, U = 0.2, Alpha = 1e-5, Gama = 25000 },
            new MaterialDto { Label = "Y1860", MaterialType = MaterialTypeDto.Steel, E = 195e9, U = 0.3, Alpha = 1.2e-5 }
        },
        Sections =
        {
            new GenericSectionDto { Label = "I", MaterialLabel = "C50/60", A = 0.45, Iz = 0.09, Az = 0.2, Ay = 0.2, Iy = 0.01, J = 0.01, MaxHeight = 1.2, MaxWidth = 0.6 },
            new RectangularSectionDto { Label = "deska", MaterialLabel = "C30/37", B = 2.0, T = 0.2 },
            new GenericSectionDto { Label = "lana", MaterialLabel = "Y1860", A = 0.0014, Iz = 1e-9, Az = 0.0014, Ay = 0.0014, Iy = 1e-9, J = 1e-9, MaxHeight = 0.05, MaxWidth = 0.05 }
        },
        Elements = { new FrameElementDto { Label = "nosnik", StartNodeLabel = "A", EndNodeLabel = "B", SectionLabel = "I" } },
        LoadCases =
        {
            new LoadCaseDto { Label = "G0", LoadCaseType = LoadCaseTypeDto.SelfWeight,
                              ElementLoads = { new UniformLoadDto { ElementLabel = "nosnik", Wy = -11250 } } },
            new LoadCaseDto { Label = "Gdeska", LoadCaseType = LoadCaseTypeDto.Dead,
                              ElementLoads = { new UniformLoadDto { ElementLabel = "nosnik", Wy = -10000 } } }
        },
        TimeDependent = new TimeDependentDto
        {
            Concretes =
            {
                new ConcreteRheologyDto { MaterialLabel = "C50/60", Fck = 50e6, CementClass = CementClassDto.R },
                new ConcreteRheologyDto { MaterialLabel = "C30/37", Fck = 30e6 }
            },
            Sections =
            {
                new SectionDryingDto { SectionLabel = "I", PerimeterExposed = 3.6 },
                new SectionDryingDto { SectionLabel = "deska", NotionalSize = 0.2 }
            },
            AdditionalElements =
            {
                new FrameElementDto { Label = "lano", StartNodeLabel = "A", EndNodeLabel = "B", SectionLabel = "lana" },
                new FrameElementDto { Label = "deska", StartNodeLabel = "A", EndNodeLabel = "B", SectionLabel = "deska" }
            },
            Elements =
            {
                new ElementAgeDto { ElementLabel = "nosnik", CuringDuration = 1 },
                new ElementAgeDto { ElementLabel = "lano", OffsetStart = -0.4, OffsetEnd = -0.4 },
                new ElementAgeDto { ElementLabel = "deska", CastingTime = 60, CuringDuration = 7, OffsetStart = 0.6, OffsetEnd = 0.6 }
            },
            Stages =
            {
                new ConstructionStageDto
                {
                    // betonáž do formy s napnutými lany: forma drží oba konce, síla lan jde do opěr formy
                    Label = "výroba", Time = 0, ActivateElements = { "nosnik", "lano" },
                    Prestress = { new PrestressDto { ElementLabel = "lano", StressStart = 1400e6, StressEnd = 1400e6 } },
                    AddSupports =
                    {
                        new StageSupportDto { NodeLabel = "A", Ux = true, Uy = true, Rz = true },
                        new StageSupportDto { NodeLabel = "B", Ux = true, Uy = true, Rz = true }
                    }
                },
                new ConstructionStageDto
                {
                    // uvolnění z formy na ložiska: síla lan do betonu (pružná ztráta) + vlastní tíha
                    Label = "vnesení předpětí", Time = 3, LoadCases = { "G0" },
                    RemoveSupports =
                    {
                        new StageSupportDto { NodeLabel = "A", Rz = true },
                        new StageSupportDto { NodeLabel = "B", Ux = true, Rz = true }
                    }
                },
                new ConstructionStageDto { Label = "betonáž desky", Time = 60, ActivateElements = { "deska" }, LoadCases = { "Gdeska" } }
            }
        }
    };

    private static ValidationResult Validate(AnalysisRequest r) => new AnalysisRequestValidator().Validate(r);

    [Test]
    public void Prestressed_composite_girder_passes_validation_and_round_trips()
    {
        var r = PrestressedCompositeGirder();
        var result = Validate(r);
        Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors));

        var json = JsonSerializer.Serialize(r, ApiJsonOptions.Default);
        var back = JsonSerializer.Deserialize<AnalysisRequest>(json, ApiJsonOptions.Default)!;
        var td = back.TimeDependent!;
        Assert.Multiple(() =>
        {
            Assert.That(td.AdditionalElements.Select(e => e.Label), Is.EqualTo(new[] { "lano", "deska" }));
            Assert.That(td.Elements.Single(e => e.ElementLabel == "lano").OffsetStart, Is.EqualTo(-0.4));
            Assert.That(td.Elements.Single(e => e.ElementLabel == "deska").OffsetEnd, Is.EqualTo(0.6));
            Assert.That(td.Stages[0].Prestress.Single().StressStart, Is.EqualTo(1400e6));
            Assert.That(JsonSerializer.Serialize(back, ApiJsonOptions.Default), Is.EqualTo(json));
        });
    }

    [Test]
    public void Fealite_computes_only_the_simple_girder()
    {
        var with = new AnalysisService().Analyze(PrestressedCompositeGirder());
        Assert.That(with.Status, Is.EqualTo(AnalysisStatusDto.Successful), string.Join("\n", with.Errors));
        Assert.That(with.LoadCaseResults["G0"].ElementForces.Keys, Is.EqualTo(new[] { "nosnik" }));
    }

    [Test]
    public void Reports_prestress_of_a_concrete_or_inactive_element()
    {
        var r = PrestressedCompositeGirder();
        r.TimeDependent!.Stages[0].Prestress.Add(new PrestressDto { ElementLabel = "deska", StressStart = 1e6, StressEnd = 1e6 });
        var errors = Validate(r).Errors;
        Assert.That(errors, Has.Some.Contains("prestressed element 'deska' is not active"));
        Assert.That(errors, Has.Some.Contains("prestressed element 'deska' must have a Steel section"));
    }

    [Test]
    public void Reports_additional_element_clashing_or_never_activated()
    {
        var r = PrestressedCompositeGirder();
        r.TimeDependent!.AdditionalElements.Add(new FrameElementDto { Label = "nosnik", StartNodeLabel = "A", EndNodeLabel = "B", SectionLabel = "I" });
        r.TimeDependent.AdditionalElements.Add(new FrameElementDto { Label = "lano2", StartNodeLabel = "A", EndNodeLabel = "X", SectionLabel = "lana" });
        var errors = Validate(r).Errors;
        Assert.That(errors, Has.Some.Contains("additional element 'nosnik': duplicate element label"));
        Assert.That(errors, Has.Some.Contains("additional element 'lano2': endNodeLabel 'X' not found"));
        Assert.That(errors, Has.Some.Contains("element 'lano2' is never activated"));
    }

    [Test]
    public void Staged_beam_passes_validation()
    {
        var result = Validate(StagedBeam());
        Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors));
    }

    [Test]
    public void Json_round_trip_preserves_time_dependent_data()
    {
        var json = JsonSerializer.Serialize(StagedBeam(), ApiJsonOptions.Default);
        Assert.That(json, Does.Contain("\"timeDependent\""));
        Assert.That(json, Does.Contain("\"rheologyModel\":\"En1992_2023\""));
        Assert.That(json, Does.Contain("\"cementClass\":\"N\""));

        var back = JsonSerializer.Deserialize<AnalysisRequest>(json, ApiJsonOptions.Default)!;
        var td = back.TimeDependent!;
        Assert.Multiple(() =>
        {
            Assert.That(td.OutputTimes, Is.EqualTo(new[] { 28.0, 60.0, 36500.0 }));
            Assert.That(td.Concretes.Single().Fck, Is.EqualTo(30e6));
            Assert.That(td.Sections.Single().PerimeterExposed, Is.EqualTo(2.0));
            Assert.That(td.Sections.Single().NotionalSize, Is.Null);
            Assert.That(td.Elements.Single().CastingTime, Is.Null);
            Assert.That(td.Stages.Select(s => s.Label), Is.EqualTo(new[] { "betonáž", "odskružení", "ostatní stálé" }));
            Assert.That(td.Stages[1].RemoveSupports[1].Ux, Is.True);
            Assert.That(JsonSerializer.Serialize(back, ApiJsonOptions.Default), Is.EqualTo(json));
        });
    }

    [Test]
    public void Request_without_time_dependent_data_serializes_as_before()
    {
        var r = StagedBeam();
        r.TimeDependent = null;
        Assert.That(JsonSerializer.Serialize(r, ApiJsonOptions.Default), Does.Not.Contain("timeDependent"));
    }

    [Test]
    public void Fealite_ignores_time_dependent_data()
    {
        var with = new AnalysisService().Analyze(StagedBeam());
        var plain = StagedBeam();
        plain.TimeDependent = null;
        var without = new AnalysisService().Analyze(plain);

        Assert.That(with.Status, Is.EqualTo(AnalysisStatusDto.Successful), string.Join("\n", with.Errors));
        Assert.That(JsonSerializer.Serialize(with, ApiJsonOptions.Default),
            Is.EqualTo(JsonSerializer.Serialize(without, ApiJsonOptions.Default)));
    }

    [Test]
    public void Reports_concrete_without_creep_properties_and_section_without_drying()
    {
        var r = StagedBeam();
        r.TimeDependent!.Concretes.Clear();
        r.TimeDependent.Sections.Clear();
        var errors = Validate(r).Errors;
        Assert.That(errors, Has.Some.Contains("concrete material 'C30/37' has no creep properties"));
        Assert.That(errors, Has.Some.Contains("concrete section 'R' has no drying data"));
    }

    [Test]
    public void Reports_unknown_references_in_stages()
    {
        var r = StagedBeam();
        r.TimeDependent!.Stages[0].ActivateElements.Add("e9");
        r.TimeDependent.Stages[1].LoadCases.Add("G9");
        r.TimeDependent.Stages[1].RemoveSupports.Add(new StageSupportDto { NodeLabel = "X", Uy = true });
        var errors = Validate(r).Errors;
        Assert.That(errors, Has.Some.Contains("unknown element 'e9'"));
        Assert.That(errors, Has.Some.Contains("unknown load case 'G9'"));
        Assert.That(errors, Has.Some.Contains("unknown node 'X'"));
    }

    [Test]
    public void Reports_stages_out_of_time_order()
    {
        var r = StagedBeam();
        r.TimeDependent!.Stages[2].Time = 10;
        Assert.That(Validate(r).Errors, Has.Some.Contains("stages must be in time order"));
    }

    [Test]
    public void Reports_load_on_element_that_is_not_active()
    {
        var r = StagedBeam();
        var s0 = r.TimeDependent!.Stages[0];
        s0.ActivateElements.Remove("e2");
        r.TimeDependent.Stages[2].ActivateElements.Add("e2");
        var errors = Validate(r).Errors;
        Assert.That(errors, Has.Some.Contains("load case 'G0' loads element 'e2', which is not active"));
    }

    [Test]
    public void Reports_element_that_is_never_activated()
    {
        var r = StagedBeam();
        r.TimeDependent!.Stages[0].ActivateElements.Remove("e2");
        Assert.That(Validate(r).Errors, Has.Some.Contains("element 'e2' is never activated"));
    }

    [Test]
    public void Reports_features_a_time_dependent_solver_does_not_support()
    {
        var r = StagedBeam();
        r.Nodes[1].Support = new SpringSupportDto { Ky = 1e8 };
        r.Nodes[2].RotationAngle = 0.1;
        var errors = Validate(r).Errors;
        Assert.That(errors, Has.Some.Contains("node 'B' has a spring support"));
        Assert.That(errors, Has.Some.Contains("node 'C' has a rotated support"));
    }

    [Test]
    public void Reports_invalid_global_values()
    {
        var r = StagedBeam();
        r.TimeDependent!.RelativeHumidity = 70;
        r.TimeDependent.StepsPerDecade = 0;
        r.TimeDependent.OutputTimes.Add(50000);
        var errors = Validate(r).Errors;
        Assert.That(errors, Has.Some.Contains("relativeHumidity must be in (0, 1]"));
        Assert.That(errors, Has.Some.Contains("stepsPerDecade must be at least 1"));
        Assert.That(errors, Has.Some.Contains("output time 50000 is outside"));
    }
}
