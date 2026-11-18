using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FEALiTE2D.Tda;

/// <summary>Values of one TDA result block in TDA units and axes, 0-based by node / element number.</summary>
internal sealed class TdaSnapshot
{
    public required double[][] Displacements { get; init; }   // U W FI
    public required double[][] Forces { get; init; }          // N0 NL M0 ML T
    public required double[][] Reactions { get; init; }       // U W FI

    public static TdaSnapshot Zero(TdaAnalysisService.Model m) => new()
    {
        Displacements = New(m.Nodes.Count, 3),
        Forces = New(m.Members.Count, 5),
        Reactions = New(m.Nodes.Count, 3)
    };

    public TdaSnapshot Add(TdaSnapshot o) => new()
    {
        Displacements = Sum(Displacements, o.Displacements),
        Forces = Sum(Forces, o.Forces),
        Reactions = Sum(Reactions, o.Reactions)
    };

    private static double[][] New(int n, int k) => Enumerable.Range(0, n).Select(_ => new double[k]).ToArray();

    private static double[][] Sum(double[][] a, double[][] b) =>
        a.Select((row, i) => row.Select((v, k) => v + b[i][k]).ToArray()).ToArray();
}

internal sealed record TdaStep(string Kind, int TimeNode, double Time, TdaSnapshot Increments, TdaSnapshot Totals);

/// <summary>Reader of the TDA JSON result (<c>tda --json</c> → <c>.tda.json</c>, cpp/src/io/results_json.cpp).</summary>
internal sealed class TdaJson
{
    public List<double> DetailTimes { get; } = new();
    public List<TdaStep> Steps { get; } = new();

    public static TdaJson Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = new TdaJson();
        foreach (var t in doc.RootElement.GetProperty("detail_times").EnumerateArray()) r.DetailTimes.Add(t.GetDouble());
        foreach (var s in doc.RootElement.GetProperty("steps").EnumerateArray())
        {
            r.Steps.Add(new TdaStep(
                s.GetProperty("kind").GetString() ?? "",
                s.GetProperty("time_node").GetInt32(),
                s.GetProperty("time").GetDouble(),
                Snapshot(s.GetProperty("increments")),
                Snapshot(s.GetProperty("totals"))));
        }
        return r;
    }

    private static TdaSnapshot Snapshot(JsonElement e) => new()
    {
        Displacements = Rows(e.GetProperty("displacements"), "U", "W", "FI"),
        Forces = Rows(e.GetProperty("internal_forces"), "N0", "NL", "M0", "ML", "T"),
        Reactions = Rows(e.GetProperty("reactions"), "U", "W", "FI")
    };

    private static double[][] Rows(JsonElement arr, params string[] names) =>
        arr.EnumerateArray().OrderBy(x => x.GetProperty("id").GetInt32())
           .Select(x => names.Select(n => x.GetProperty(n).GetDouble()).ToArray()).ToArray();
}
