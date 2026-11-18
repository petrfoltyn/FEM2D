using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FEALiTE2D.Api.Contracts.Requests;

/// <summary>
/// Time-dependent extension of <see cref="AnalysisRequest"/>: construction stages, creep and shrinkage of concrete,
/// the time axis. The structure itself (nodes, materials, sections, elements, load cases) stays in the request and is
/// referenced by label. The FEALiTE2D solver ignores this part: its result is the instantaneous response at the
/// reference time of 28 days (E = E28, no creep), one point of the time axis a time-dependent solver (TDA) covers.
/// Units: SI as the rest of the request, times in days.
/// </summary>
public sealed class TimeDependentDto
{
    /// <summary>Creep and shrinkage model, one for the whole structure.</summary>
    public RheologyModelDto RheologyModel { get; set; } = RheologyModelDto.En1992_2023;

    /// <summary>Relative humidity of the environment, 0..1.</summary>
    public double RelativeHumidity { get; set; } = 0.7;

    /// <summary>End of the analysis [days].</summary>
    public double AnalysisEnd { get; set; } = 36500;

    /// <summary>Number of time steps per decade of time after each stage.</summary>
    public int StepsPerDecade { get; set; } = 4;

    /// <summary>Times with results [days]; empty = the stages and the end of the analysis.</summary>
    public List<double> OutputTimes { get; set; } = new();

    /// <summary>Creep properties of the concrete materials (every Concrete material of the request).</summary>
    public List<ConcreteRheologyDto> Concretes { get; set; } = new();

    /// <summary>Drying of the sections (notional size); a concrete section without an entry is an error.</summary>
    public List<SectionDryingDto> Sections { get; set; } = new();

    /// <summary>
    /// Time-dependent data of the elements (age, eccentricity) — for elements of the request and for
    /// <see cref="AdditionalElements"/>. An element without an entry is cast at the stage that activates it and lies
    /// on the node axis.
    /// </summary>
    public List<ElementAgeDto> Elements { get; set; } = new();

    /// <summary>
    /// Elements only the time-dependent solver models: tendons, a composite slab, parts of a section cast at
    /// different times. They run parallel to the request elements between the same nodes, offset by their
    /// eccentricity (<see cref="ElementAgeDto.OffsetStart"/>). FEALiTE2D keeps the simple model of the request.
    /// Labels must differ from the request elements.
    /// </summary>
    public List<FrameElementDto> AdditionalElements { get; set; } = new();

    /// <summary>
    /// Construction stages in time order; the first one activates the initial structure. A node whose supports no
    /// stage adds or removes gets its <see cref="NodeDto.Support"/> of the request in the first stage.
    /// </summary>
    public List<ConstructionStageDto> Stages { get; set; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RheologyModelDto
{
    /// <summary>EN 1992-1-1:2004 (TDA IFI 12).</summary>
    En1992_2004,
    /// <summary>EN 1992-1-1:2023 (TDA IFI 15).</summary>
    En1992_2023
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CementClassDto
{
    S,
    N,
    R
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AggregateTypeDto
{
    Quartzite,
    Limestone,
    Sandstone,
    Basalt
}

/// <summary>Creep and shrinkage properties of one concrete material of the request.</summary>
public sealed class ConcreteRheologyDto
{
    public string MaterialLabel { get; set; } = string.Empty;

    /// <summary>Characteristic cylinder strength fck [Pa].</summary>
    public double Fck { get; set; }

    public CementClassDto CementClass { get; set; } = CementClassDto.N;

    public AggregateTypeDto AggregateType { get; set; } = AggregateTypeDto.Quartzite;
}

/// <summary>Drying of a section: notional size h0 = 2·A/u, given directly or by the perimeter exposed to drying.</summary>
public sealed class SectionDryingDto
{
    public string SectionLabel { get; set; } = string.Empty;

    /// <summary>Perimeter exposed to drying u [m].</summary>
    public double? PerimeterExposed { get; set; }

    /// <summary>Notional size h0 [m]; takes precedence over <see cref="PerimeterExposed"/>.</summary>
    public double? NotionalSize { get; set; }
}

/// <summary>Time-dependent data of one element: when it is cast, how long it is cured, where its axis lies.</summary>
public sealed class ElementAgeDto
{
    public string ElementLabel { get; set; } = string.Empty;

    /// <summary>Casting time [days]; null = the time of the stage that activates the element.</summary>
    public double? CastingTime { get; set; }

    /// <summary>Duration of curing [days]; shrinkage starts at casting + curing.</summary>
    public double CuringDuration { get; set; } = 3;

    /// <summary>
    /// Eccentricity of the element axis from the node axis at the start node [m], in the element local y
    /// (positive = to the left of start→end, i.e. up for an element running in +X). Girder centroid, slab
    /// centroid above it, tendon below it.
    /// </summary>
    public double OffsetStart { get; set; }

    /// <summary>Eccentricity at the end node [m]; see <see cref="OffsetStart"/>.</summary>
    public double OffsetEnd { get; set; }
}

/// <summary>One construction stage: what changes on the structure at <see cref="Time"/>.</summary>
public sealed class ConstructionStageDto
{
    public string Label { get; set; } = string.Empty;

    /// <summary>Time of the stage [days].</summary>
    public double Time { get; set; }

    public List<string> ActivateElements { get; set; } = new();

    public List<string> DeactivateElements { get; set; } = new();

    /// <summary>Supports added at this stage (restraints are added to the ones the node already has).</summary>
    public List<StageSupportDto> AddSupports { get; set; } = new();

    /// <summary>Supports removed at this stage.</summary>
    public List<StageSupportDto> RemoveSupports { get; set; } = new();

    /// <summary>Load cases applied from this stage on (permanent loads; variable loads stay instantaneous).</summary>
    public List<string> LoadCases { get; set; } = new();

    /// <summary>Prestress applied at this stage (pretensioned tendons released, post-tensioned tendons stressed).</summary>
    public List<PrestressDto> Prestress { get; set; } = new();
}

/// <summary>
/// Prestress of a tendon element (steel, usually one of <see cref="TimeDependentDto.AdditionalElements"/>): the
/// stress in the tendon when it is anchored, before the elastic and time-dependent losses.
/// </summary>
public sealed class PrestressDto
{
    public string ElementLabel { get; set; } = string.Empty;

    /// <summary>Initial tendon stress σp0 [Pa] (tension positive) at the start of the element.</summary>
    public double StressStart { get; set; }

    /// <summary>Initial tendon stress σp0 [Pa] at the end of the element (friction losses along it).</summary>
    public double StressEnd { get; set; }
}

/// <summary>Restrained degrees of freedom of a node in a stage.</summary>
public sealed class StageSupportDto
{
    public string NodeLabel { get; set; } = string.Empty;
    public bool Ux { get; set; }
    public bool Uy { get; set; }
    public bool Rz { get; set; }
}
