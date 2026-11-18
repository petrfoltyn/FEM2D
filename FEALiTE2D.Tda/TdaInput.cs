using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FEALiTE2D.Tda;

/// <summary>TDA degrees of freedom of a node: U (X), W (Z down), FI.</summary>
internal enum TdaDof { U = 0, W = 1, FI = 2 }

/// <summary>Physical type FK(1..40) of TDA.</summary>
internal sealed record TdaPhysicalType(double[] Fk)
{
    public bool Equals(TdaPhysicalType? other) => other is not null && Fk.SequenceEqual(other.Fk);
    public override int GetHashCode() => Fk.Aggregate(17, (h, v) => h * 31 + v.GetHashCode());
}

internal sealed record TdaGeometryType(double DX, double DZ, double Ez1, double Ez2);

/// <summary>What changes at one time node (batch 6 block).</summary>
internal sealed class TdaEvent
{
    public List<int> Remove { get; } = new();
    public List<int> Install { get; } = new();
    public List<(int Element, double E0, double EL)> AxialStrain { get; } = new();
    public List<int>[] RemoveSupports { get; } = { new(), new(), new() };
    public List<(int Node, double Value)>[] InstallSupports { get; } = { new(), new(), new() };
    public Dictionary<int, double>[] NodalLoads { get; } = { new(), new(), new() };

    public void AddLoad(int node, TdaDof dof, double value)
    {
        var d = NodalLoads[(int)dof];
        d[node] = d.GetValueOrDefault(node) + value;
    }
}

/// <summary>Native TDA input (batches 1–8) in TDA units (kN, m, kPa, days); 1-based numbering.</summary>
internal sealed class TdaInput
{
    public int Ifi { get; set; } = 12;
    public int NodeCount { get; set; }
    public List<TdaGeometryType> GeometryTypes { get; } = new();
    public List<TdaPhysicalType> PhysicalTypes { get; } = new();
    /// <summary>Elements: start, end node (1-based), geometry and physical type (1-based).</summary>
    public List<(int Start, int End, int Geometry, int Physical)> Elements { get; } = new();
    /// <summary>User time nodes [days], first 0.</summary>
    public List<double> Times { get; } = new();
    public List<TdaEvent> Events { get; } = new();
    public double Humidity { get; set; } = 0.7;

    public int AddGeometry(TdaGeometryType g)
    {
        int i = GeometryTypes.IndexOf(g);
        if (i < 0) { GeometryTypes.Add(g); i = GeometryTypes.Count - 1; }
        return i + 1;
    }

    public int AddPhysical(double[] fk)
    {
        var t = new TdaPhysicalType(fk);
        int i = PhysicalTypes.IndexOf(t);
        if (i < 0) { PhysicalTypes.Add(t); i = PhysicalTypes.Count - 1; }
        return i + 1;
    }

    /// <summary>
    /// Text of the .@za input. Rules (TDA docs/vstupni-data.md): a record ends with ';' and starts on a new line,
    /// only the first 80 characters of a line are read, exponent with upper-case E, empty record "0 ;", a blank
    /// line between the batch-6 blocks; without time steps batch 7 and 8 are a single "0 ;".
    /// </summary>
    public string ToZa()
    {
        int np = Elements.Count, nzi = Times.Count - 1;
        if (Events.Count != Times.Count) throw new InvalidOperationException("One event block per time node.");
        var lines = new List<string>
        {
            Record(new double[] { np, NodeCount, GeometryTypes.Count, PhysicalTypes.Count, np, Ifi, nzi, nzi }),
            Record(GeometryTypes.SelectMany(g => new[] { g.DX, g.DZ, g.Ez1, g.Ez2 })
                .Concat(Elements.Select(e => (double)e.Geometry))),
            Record(PhysicalTypes.SelectMany(t => t.Fk).Concat(Elements.Select(e => (double)e.Physical))),
            Record(Elements.SelectMany(e => new double[] { 1, 1, e.Start, e.End })),
            Record(Times.Concat(Enumerable.Repeat(1.0, nzi)))
        };
        foreach (var ev in Events)
        {
            lines.Add("");
            lines.Add(Record(ev.Remove.Select(x => (double)x)));
            lines.Add(Record(ev.Install.Select(x => (double)x)));
            lines.Add(Record(ev.AxialStrain.SelectMany(s => new[] { s.Element, s.E0, s.EL })));
            lines.Add(Record(Array.Empty<double>()));
            lines.Add(Record(Array.Empty<double>()));
            for (int d = 0; d < 3; d++)
            {
                lines.Add(Record(ev.RemoveSupports[d].Select(x => (double)x)));
                lines.Add(Record(ev.InstallSupports[d].SelectMany(s => new[] { s.Node, s.Value })));
                lines.Add(Record(ev.NodalLoads[d].Where(kv => kv.Value != 0).OrderBy(kv => kv.Key)
                    .SelectMany(kv => new[] { kv.Key, kv.Value })));
            }
        }
        lines.Add("");
        if (nzi > 0)
        {
            lines.Add(Record(new double[] { -273, 1 }));
            lines.Add(Record(Enumerable.Repeat(Humidity, nzi)));
        }
        else
        {
            lines.Add(Record(new double[] { 0 }));
            lines.Add(Record(new double[] { 0 }));
        }
        lines.Add("");
        return string.Join("\n", lines) + "\n";
    }

    /// <summary>Number for .@za: integers as is, otherwise 10 significant digits, upper-case E.</summary>
    public static string Fmt(double v)
    {
        if (!double.IsFinite(v)) throw new InvalidOperationException($"Invalid number {v} in the TDA input.");
        if (v == Math.Floor(v) && Math.Abs(v) < 1e15) return ((long)v).ToString(CultureInfo.InvariantCulture);
        double r = double.Parse(v.ToString("G10", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        return r.ToString("R", CultureInfo.InvariantCulture).ToUpperInvariant();
    }

    private static string Record(IEnumerable<double> values)
    {
        var toks = values.Select(Fmt).ToList();
        if (toks.Count == 0) toks.Add("0");
        var sb = new StringBuilder();
        var line = new StringBuilder();
        foreach (var t in toks)
        {
            if (line.Length > 0 && line.Length + 1 + t.Length > 72)
            {
                sb.Append(line).Append('\n');
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(t);
        }
        sb.Append(line).Append(" ;");
        return sb.ToString();
    }
}

/// <summary>
/// TDA concrete constants Dt(4..33) for IFI 12 (EN 1992-1-1:2004) and 15 (EN 1992-1-1:2023). Same formulas as
/// CSSC DCE.Engine/Tda/ConcreteTdaParameters and the checks in TDA cpp/src/rheology/rheology.cpp — keep in sync.
/// </summary>
internal static class ConcreteParameters
{
    private static readonly Dictionary<string, double> AggregateFactor = new()
    {
        ["Quartzite"] = 1.0, ["Limestone"] = 0.9, ["Sandstone"] = 0.7, ["Basalt"] = 1.2,
    };

    public static double ScEn2023(double fck, string cement)
    {
        var (s, n, r) = fck <= 35 ? (0.6, 0.5, 0.3) : fck < 60 ? (0.5, 0.4, 0.2) : (0.4, 0.3, 0.1);
        return cement switch { "S" => s, "R" => r, _ => n };
    }

    /// <summary>
    /// Dt(index) → value; fck in MPa, fcm = fck + 8. EN 2023: TDA checks E28 against (5.1) Ecm = kE·fcm^(1/3)
    /// (error 78 above 20 %); the E of the request is kept (same response at 28 days as FEALiTE2D) and the aggregate
    /// modulus Ea is calibrated so that (5.1) gives exactly <paramref name="eMpa"/>. <paramref name="formulaE"/> =
    /// Ecm of (5.1) for the given aggregate type (for a warning when the request E deviates).
    /// </summary>
    public static SortedDictionary<int, double> Dt(double fck, string cement, string aggregate, bool en2023, double eMpa,
                                                   out double formulaE)
    {
        double fcm = fck + 8, agg = AggregateFactor[aggregate];
        var dt = new SortedDictionary<int, double> { [20] = 1e30 };
        formulaE = eMpa;
        if (en2023)
        {
            double ac = Math.Max(1.0, 1 / (0.8 + 0.2 * fcm / 88));
            double kE60 = 21500 * Math.Pow(10, -1.0 / 3) / ac;          // kE for Ea = 60 000 MPa
            formulaE = kE60 * agg * Math.Cbrt(fcm);
            dt[4] = ScEn2023(fck, cement);
            dt[5] = Round(60000 * eMpa / (kE60 * Math.Cbrt(fcm)), 4);
            dt[7] = Round(ac, 6);
            dt[10] = fcm;
            dt[11] = 28;
            dt[14] = 1;
        }
        else
        {
            dt[4] = cement switch { "R" => 0.2, "S" => 0.38, _ => 0.25 };
            dt[5] = agg;
            dt[10] = fcm;
            dt[11] = fck;
            dt[12] = 1;
        }
        return dt;
    }

    private static double Round(double v, int digits)
    {
        double f = Math.Pow(10, digits);
        return Math.Floor(v * f + 0.5) / f;
    }
}
