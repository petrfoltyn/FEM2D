using System;
using System.Collections.Generic;
using System.Linq;
using FEALiTE2D.Api.Contracts.Enums;
using FEALiTE2D.Api.Contracts.Requests;
using FEALiTE2D.Api.Contracts.Responses;

namespace FEALiTE2D.Tda;

/// <summary>
/// A frame element with the FEALiTE2D semantics of element loads reproduced exactly: consistent nodal loads
/// (FrameElement2D.GlobalEndForcesForLoadCase incl. the release condensation), the mesh (LinearMesher) and the
/// segment forces (PostProcessor.GetElementInternalForces). Local forces f = (fx, fy, mz) at a section are the
/// resultant of the part left of it: fx = −N, fy = V, mz = −M (M positive with tension on the local −y side).
/// </summary>
internal sealed class FrameMember
{
    private static readonly (double X, double W)[] Gauss3 =
        { (-Math.Sqrt(0.6), 5.0 / 9.0), (0.0, 8.0 / 9.0), (Math.Sqrt(0.6), 5.0 / 9.0) };

    public FrameMember(string label, int index, int startNode, int endNode, NodeDto start, NodeDto end,
                       double area, double iz, MaterialDto material, EndReleaseDto release, bool additional)
    {
        Label = label;
        Index = index;
        StartNode = startNode;
        EndNode = endNode;
        Dx = end.X - start.X;
        Dy = end.Y - start.Y;
        L = Math.Sqrt(Dx * Dx + Dy * Dy);
        C = Dx / L;
        S = Dy / L;
        A = area;
        Iz = iz;
        Material = material;
        Release = release;
        IsAdditional = additional;
    }

    public string Label { get; }
    public int Index { get; }
    public int StartNode { get; }
    public int EndNode { get; }
    public double Dx { get; }
    public double Dy { get; }
    public double L { get; }
    public double C { get; }
    public double S { get; }
    public double A { get; }
    public double Iz { get; }
    public MaterialDto Material { get; }
    public EndReleaseDto Release { get; }
    public bool IsAdditional { get; }

    /// <summary>Eccentricity (local y, + = left of start→end) at the start and end [m].</summary>
    public double OffsetStart { get; set; }
    public double OffsetEnd { get; set; }

    public double OffsetAt(double x) => OffsetStart + (OffsetEnd - OffsetStart) * x / L;

    public string Section { get; init; } = string.Empty;

    /// <summary>Request element this additional element is a part of (same start and end node), or null.</summary>
    public FrameMember? Host { get; set; }

    /// <summary>Additional elements that are parts of this request element (composite member).</summary>
    public List<FrameMember> Parts { get; } = new();

    public (double X, double Y) ToLocal(double gx, double gy) => (C * gx + S * gy, -S * gx + C * gy);

    public (double X, double Y) ToGlobal(double lx, double ly) => (C * lx - S * ly, S * lx + C * ly);

    private (double X, double Y) LocalLoad(ElementLoadDto load, double wx, double wy) =>
        load.Direction == LoadDirectionDto.Global ? ToLocal(wx, wy) : (wx, wy);

    // ------------------------------------------------------------------------------- fixed-end forces

    private (double N1, double N2, double N3, double N4, double N5, double N6) Shape(double x)
    {
        double xi = x / L;
        return (1 - xi, xi, 1 - 3 * xi * xi + 2 * xi * xi * xi, L * (xi - 2 * xi * xi + xi * xi * xi),
                3 * xi * xi - 2 * xi * xi * xi, L * (-xi * xi + xi * xi * xi));
    }

    private double[] Distributed(double a, double b, Func<double, double> wx, Func<double, double> wy)
    {
        var fem = new double[6];
        if (b <= a) return fem;
        foreach (var (g, w) in Gauss3)
        {
            double x = 0.5 * (a + b) + 0.5 * (b - a) * g, wt = 0.5 * (b - a) * w;
            var n = Shape(x);
            double qx = wx(x), qy = wy(x);
            fem[0] += n.N1 * qx * wt;
            fem[1] += n.N3 * qy * wt;
            fem[2] += n.N4 * qy * wt;
            fem[3] += n.N2 * qx * wt;
            fem[4] += n.N5 * qy * wt;
            fem[5] += n.N6 * qy * wt;
        }
        return fem;
    }

    /// <summary>Consistent nodal loads in local axes before the release condensation (ILoad.GetGlobalFixedEndForces).</summary>
    private double[] LocalFem(ElementLoadDto load)
    {
        switch (load)
        {
            case PointLoadDto p:
            {
                var (fx, fy) = LocalLoad(p, p.Fx, p.Fy);
                double mz = p.Mz, l1 = p.L1, a = l1 / L, bb = (L - l1) / L;
                return new[]
                {
                    fx * bb,
                    fy * bb * bb * (3 * a + bb) - 6.0 * mz * a * bb / L,
                    fy * l1 * bb * bb + mz * bb * (bb - 2.0 * a),
                    fx * a,
                    fy * a * a * (a + 3 * bb) + 6.0 * mz * a * bb / L,
                    -fy * a * a * (L - l1) + mz * a * (a - 2.0 * bb)
                };
            }
            case UniformLoadDto u:
            {
                var (wx, wy) = LocalLoad(u, u.Wx, u.Wy);
                return Distributed(u.L1, L - u.L2, _ => wx, _ => wy);
            }
            case TrapezoidalLoadDto t:
            {
                var (wx1, wy1) = LocalLoad(t, t.Wx1, t.Wy1);
                var (wx2, wy2) = LocalLoad(t, t.Wx2, t.Wy2);
                double a = t.L1, b = L - t.L2;
                Func<double, double> Lin(double v1, double v2) => x => b > a ? v1 + (v2 - v1) * (x - a) / (b - a) : 0.0;
                return Distributed(a, b, Lin(wx1, wx2), Lin(wy1, wy2));
            }
            default:
                throw new NotSupportedException($"Element load {load.GetType().Name} is not supported.");
        }
    }

    /// <summary>
    /// Fixed-end forces f (global, f = −Σ consistent loads, condensed for the release on the global components as
    /// FEALiTE2D does). The consistent nodal load is −f; the true end forces are k·d + f.
    /// </summary>
    public double[] FixedEndForces(IEnumerable<ElementLoadDto> loads)
    {
        var f = new double[6];
        foreach (var load in loads)
        {
            var fl = LocalFem(load);
            foreach (int k in new[] { 0, 3 })
            {
                var (gx, gy) = ToGlobal(fl[k], fl[k + 1]);
                f[k] -= gx;
                f[k + 1] -= gy;
                f[k + 2] -= fl[k + 2];
            }
        }
        switch (Release)
        {
            case EndReleaseDto.StartRelease:
                f[1] -= 1.5 * f[2] / L; f[4] += 1.5 * f[2] / L; f[5] -= 0.5 * f[2]; f[2] = 0; break;
            case EndReleaseDto.EndRelease:
                f[1] -= 1.5 * f[5] / L; f[2] -= 0.5 * f[5]; f[4] += 1.5 * f[5] / L; f[5] = 0; break;
            case EndReleaseDto.FullRelease:
                f[1] -= (f[2] + f[5]) / L; f[4] += (f[2] + f[5]) / L; f[2] = 0; f[5] = 0; break;
        }
        return f;
    }

    // ----------------------------------------------------------------------------------- segments

    /// <summary>LinearMesher.SetupMeshSegments: load start/end points + n equal divisions.</summary>
    public List<double> MeshPoints(IEnumerable<ElementLoadDto> loads, int segments)
    {
        var pts = new SortedSet<double> { 0.0, L };
        foreach (var load in loads)
        {
            switch (load)
            {
                case PointLoadDto p: pts.Add(p.L1); break;
                case UniformLoadDto u: pts.Add(u.L1); pts.Add(L - u.L2); break;
                case TrapezoidalLoadDto t: pts.Add(t.L1); pts.Add(L - t.L2); break;
            }
        }
        double dx = L / segments;
        for (int i = 0; i < segments; i++) pts.Add(i * dx);
        return pts.ToList();
    }

    /// <summary>PostProcessor.GetElementInternalForces: segment end forces from the local start forces.</summary>
    public List<SegmentDto> Segments(double[] start, IReadOnlyList<ElementLoadDto> loads, IReadOnlyList<double> points)
    {
        var result = new List<SegmentDto>(points.Count - 1);
        for (int i = 0; i + 1 < points.Count; i++)
        {
            double x = points[i], xEnd = points[i + 1];
            double fx = start[0], fy = start[1], mz = start[2] - start[1] * x;
            double wx1 = 0, wx2 = 0, wy1 = 0, wy2 = 0;
            foreach (var load in loads)
            {
                switch (load)
                {
                    case PointLoadDto p when p.L1 <= x:
                    {
                        var (px, py) = LocalLoad(p, p.Fx, p.Fy);
                        fx += px; fy += py; mz += p.Mz - py * (x - p.L1);
                        break;
                    }
                    case UniformLoadDto u when u.L1 <= x:
                    {
                        var (wx, wy) = LocalLoad(u, u.Wx, u.Wy);
                        double a = u.L1, b = L - u.L2;
                        if (b > x) { wx1 += wx; wx2 += wx; wy1 += wy; wy2 += wy; b = x; }
                        fx += wx * (b - a); fy += wy * (b - a); mz -= wy * (b - a) * (x - 0.5 * b - 0.5 * a);
                        break;
                    }
                    case TrapezoidalLoadDto t when t.L1 <= x:
                    {
                        var (qx1, qy1) = LocalLoad(t, t.Wx1, t.Wy1);
                        var (qx2, qy2) = LocalLoad(t, t.Wx2, t.Wy2);
                        double a = t.L1, b = L - t.L2;
                        if (b > x)
                        {
                            wx1 += (qx2 - qx1) / (b - a) * (x - a) + qx1;
                            wx2 += (qx2 - qx1) / (b - a) * (xEnd - a) + qx1;
                            wy1 += (qy2 - qy1) / (b - a) * (x - a) + qy1;
                            wy2 += (qy2 - qy1) / (b - a) * (xEnd - a) + qy1;
                            qx2 = (qx2 - qx1) / (b - a) * (x - a) + qx1;
                            qy2 = (qy2 - qy1) / (b - a) * (x - a) + qy1;
                            b = x;
                        }
                        fx += 0.5 * (qx1 + qx2) * (b - a);
                        fy += 0.5 * (qy1 + qy2) * (b - a);
                        mz += (2 * qy1 * a - 3 * qy1 * x + qy1 * b + qy2 * a - 3 * qy2 * x + 2 * qy2 * b) * (b - a) / 6;
                        break;
                    }
                }
            }
            double s = xEnd - x;
            result.Add(new SegmentDto
            {
                X1 = x,
                X2 = xEnd,
                StartForce = new ForceDto { Fx = fx, Fy = fy, Mz = mz },
                EndForce = new ForceDto
                {
                    Fx = fx + wx1 * s + s * (wx2 - wx1) / 2,
                    Fy = fy + wy1 * s + 0.5 * s * (wy2 - wy1),
                    Mz = mz - fy * s - 0.5 * wy1 * s * s - s * s * (wy2 - wy1) / 6
                }
            });
        }
        return result;
    }
}
