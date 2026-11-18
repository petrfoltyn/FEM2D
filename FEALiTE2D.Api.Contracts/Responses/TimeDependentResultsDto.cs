using System.Collections.Generic;
using System.Text.Json.Serialization;
using FEALiTE2D.Api.Contracts.Requests;

namespace FEALiTE2D.Api.Contracts.Responses;

/// <summary>
/// Results of the time-dependent solver (TDA) for <see cref="AnalysisRequest.TimeDependent"/>. Every state and effect
/// is a <see cref="CaseResultDto"/> with the same labels, axes, units and sign convention as the FEALiTE2D load case
/// results, so the same diagrams, tables and combinations work on both. FEALiTE2D never fills it.
/// </summary>
public sealed class TimeDependentResultsDto
{
    public RheologyModelDto RheologyModel { get; set; }

    /// <summary>Full states of the structure in time order: before / after every stage, output times, end.</summary>
    public List<TimeStateDto> States { get; set; } = new();

    /// <summary>
    /// The states split into causes: the instantaneous effect of every stage and the effect of creep and shrinkage
    /// between consecutive reported times. The sum of the effects up to a time equals the state at that time.
    /// </summary>
    public List<EffectDto> Effects { get; set; } = new();

    /// <summary>Values at every time step of the solver (logarithmic time axis) for time graphs.</summary>
    public TimeHistoriesDto Histories { get; set; } = new();

    /// <summary>Warnings of the conversion or the solver (text for the report).</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>State of the structure at one time (cumulative from the start of the analysis).</summary>
public sealed class TimeStateDto
{
    /// <summary>Time [days].</summary>
    public double Time { get; set; }

    /// <summary>
    /// Construction stage: for <see cref="TimeStateKindDto.BeforeStage"/> / <see cref="TimeStateKindDto.AfterStage"/>
    /// the stage at <see cref="Time"/> the state precedes / follows, otherwise the stage valid at <see cref="Time"/>.
    /// </summary>
    public string Stage { get; set; } = string.Empty;

    public TimeStateKindDto Kind { get; set; }

    /// <summary>Elements (of the request and additional ones) in the structure at <see cref="Time"/> (before the
    /// changes of the stage for <see cref="TimeStateKindDto.BeforeStage"/>).</summary>
    public List<string> ActiveElements { get; set; } = new();

    public CaseResultDto Total { get; set; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimeStateKindDto
{
    /// <summary>Just before the changes of a stage (after creep up to its time).</summary>
    BeforeStage,
    /// <summary>Just after the changes of a stage.</summary>
    AfterStage,
    /// <summary>A requested output time.</summary>
    Output,
    /// <summary>End of the analysis.</summary>
    End
}

/// <summary>An increment of the state with one cause.</summary>
public sealed class EffectDto
{
    /// <summary>Stage label or "CS from–to d".</summary>
    public string Label { get; set; } = string.Empty;

    public EffectKindDto Kind { get; set; }

    /// <summary>Stage label for <see cref="EffectKindDto.Stage"/>.</summary>
    public string? Stage { get; set; }

    /// <summary>Start of the interval [days] (equal to <see cref="TimeTo"/> for a stage).</summary>
    public double TimeFrom { get; set; }

    /// <summary>End of the interval [days].</summary>
    public double TimeTo { get; set; }

    /// <summary>The increment (not cumulative).</summary>
    public CaseResultDto Result { get; set; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EffectKindDto
{
    /// <summary>Instantaneous effect of a stage: loads, supports, prestress, (de)activated elements.</summary>
    Stage,
    /// <summary>Effect of creep and shrinkage (redistribution, losses of prestress) in an interval.</summary>
    CreepShrinkage
}

/// <summary>Time histories; every list has the length of <see cref="Times"/>.</summary>
public sealed class TimeHistoriesDto
{
    /// <summary>Time steps of the solver [days].</summary>
    public List<double> Times { get; set; } = new();

    /// <summary>Node displacements (and reactions of nodes that are supported at some time); key = node label.</summary>
    public Dictionary<string, NodeHistoryDto> Nodes { get; set; } = new();

    /// <summary>Forces at the element ends; key = label of a request element or an additional element.</summary>
    public Dictionary<string, ElementHistoryDto> Elements { get; set; } = new();
}

public sealed class NodeHistoryDto
{
    public List<double> Ux { get; set; } = new();
    public List<double> Uy { get; set; } = new();
    public List<double> Rz { get; set; } = new();

    /// <summary>Support reactions (0 while the dof is free); null for a node never supported.</summary>
    public List<double>? Fx { get; set; }
    public List<double>? Fy { get; set; }
    public List<double>? Mz { get; set; }
}

/// <summary>
/// Element end forces in engineering convention, in the element's own axis: N tension positive, M positive when the
/// local −y side is in tension (sagging for an element running in +X), V = shear force at the start (dM/dx). For a
/// request element with additional parallel elements these are the forces of the element itself (not the resultant).
/// </summary>
public sealed class ElementHistoryDto
{
    public List<double> NStart { get; set; } = new();
    public List<double> NEnd { get; set; } = new();
    public List<double> MStart { get; set; } = new();
    public List<double> MEnd { get; set; } = new();
    public List<double> V { get; set; } = new();
}
