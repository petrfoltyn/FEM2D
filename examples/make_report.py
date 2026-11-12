#!/usr/bin/env python3
"""Visual check of the fem2d examples: report.html with, for every example,
  - the structure with supports, releases, springs and loads (per load case),
  - the results of every load case / combination: deformed shape + reactions, M, V (and N when non-zero),
  - the table of the expected.json checks (pass / fail with the computed value).

  python make_report.py --url http://localhost:9301      # calls the service
  python make_report.py --offline                        # uses saved <example>/response.json (run_examples.py --save)

Drawing conventions: diagrams are drawn on the left normal of the element (start -> end) with +value, i.e. for a
horizontal beam drawn left to right sagging M (negative mz of FEALiTE) is below the axis = tension side.
Standard library only.
"""
import argparse, html, json, math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import run_examples  # noqa: E402  (resolve, check, post)

W, H, PAD = 590, 290, 46
INK, MUTED, GRID = "#2c2a26", "#9e9890", "#e3dfd8"
LOAD, REACT, DEF = "#c0392b", "#1b7f5a", "#2a78d6"
COLOR = {"M": "#2a78d6", "V": "#eb6834", "N": "#7a5af5"}


def g(v):
    if abs(v) < 1e-12:
        return "0"
    s = f"{v:.4g}"
    return s.replace("e+0", "e").replace("e-0", "e-")


def fnum(v):
    """API number; NaN / Infinity come as strings (spring elements) -> nan."""
    try:
        return float(v)
    except (TypeError, ValueError):
        return float("nan")


def esc(s):
    return html.escape(str(s))


class Model:
    def __init__(self, req):
        self.nodes = {n["label"]: n for n in req["nodes"]}
        self.elements = req["elements"]
        self.cases = req.get("loadCases", [])
        self.combos = req.get("loadCombinations", [])
        self.settings = req.get("settings") or {}
        xs = [n["x"] for n in req["nodes"]]
        ys = [n["y"] for n in req["nodes"]]
        self.x0, self.x1, self.y0, self.y1 = min(xs), max(xs), min(ys), max(ys)
        self.span = max(self.x1 - self.x0, self.y1 - self.y0, 1e-9)

    def geom(self, e):
        a, b = self.nodes[e["startNodeLabel"]], self.nodes[e["endNodeLabel"]]
        dx, dy = b["x"] - a["x"], b["y"] - a["y"]
        L = math.hypot(dx, dy) or 1e-12
        return (a["x"], a["y"]), (dx / L, dy / L), L


class View:
    """Model -> screen transform for a set of model-space points (y up)."""
    def __init__(self, pts, w=W, h=H, pad=PAD):
        xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
        self.minx, self.maxx, self.miny, self.maxy = min(xs), max(xs), min(ys), max(ys)
        sx = (w - 2 * pad) / max(self.maxx - self.minx, 1e-12)
        sy = (h - 2 * pad) / max(self.maxy - self.miny, 1e-12)
        self.s = min(sx, sy)
        self.ox = (w - (self.maxx - self.minx) * self.s) / 2
        self.oy = (h - (self.maxy - self.miny) * self.s) / 2
        self.h = h

    def p(self, x, y):
        return self.ox + (x - self.minx) * self.s, self.h - (self.oy + (y - self.miny) * self.s)


def arrow(x1, y1, x2, y2, color, width=1.4, head=6):
    ang = math.atan2(y2 - y1, x2 - x1)
    hx1, hy1 = x2 - head * math.cos(ang - 0.4), y2 - head * math.sin(ang - 0.4)
    hx2, hy2 = x2 - head * math.cos(ang + 0.4), y2 - head * math.sin(ang + 0.4)
    return (f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{color}" stroke-width="{width}"/>'
            f'<path d="M{x2:.1f},{y2:.1f} L{hx1:.1f},{hy1:.1f} L{hx2:.1f},{hy2:.1f} Z" fill="{color}"/>')


def moment_symbol(x, y, mz, color, label):
    r, sgn = 11, 1 if mz > 0 else -1   # counter-clockwise positive (model y up -> screen flipped)
    a0, a1 = math.radians(200), math.radians(-20)
    p0 = (x + r * math.cos(a0), y - r * math.sin(a0)); p1 = (x + r * math.cos(a1), y - r * math.sin(a1))
    if sgn < 0:
        p0, p1 = p1, p0
    sweep = 1 if sgn > 0 else 0
    tip = p1
    tang = math.atan2(tip[1] - y, tip[0] - x) + (-math.pi / 2 if sgn > 0 else math.pi / 2)
    hx1, hy1 = tip[0] - 6 * math.cos(tang - 0.5), tip[1] - 6 * math.sin(tang - 0.5)
    hx2, hy2 = tip[0] - 6 * math.cos(tang + 0.5), tip[1] - 6 * math.sin(tang + 0.5)
    return (f'<path d="M{p0[0]:.1f},{p0[1]:.1f} A{r},{r} 0 1 {sweep} {p1[0]:.1f},{p1[1]:.1f}" fill="none" stroke="{color}" stroke-width="1.4"/>'
            f'<path d="M{tip[0]:.1f},{tip[1]:.1f} L{hx1:.1f},{hy1:.1f} L{hx2:.1f},{hy2:.1f} Z" fill="{color}"/>'
            f'<text x="{x + 14:.1f}" y="{y - 12:.1f}" font-size="10" fill="{color}">{esc(label)}</text>')


def zigzag(x1, y1, x2, y2, color=MUTED, n=6, amp=4):
    dx, dy = x2 - x1, y2 - y1
    L = math.hypot(dx, dy) or 1
    ux, uy, nx, ny = dx / L, dy / L, -dy / L, dx / L
    pts = [(x1, y1), (x1 + ux * L * 0.2, y1 + uy * L * 0.2)]
    for i in range(n):
        t = 0.2 + 0.6 * (i + 0.5) / n
        s = amp if i % 2 == 0 else -amp
        pts.append((x1 + ux * L * t + nx * s, y1 + uy * L * t + ny * s))
    pts += [(x1 + ux * L * 0.8, y1 + uy * L * 0.8), (x2, y2)]
    return f'<polyline points="{" ".join(f"{a:.1f},{b:.1f}" for a, b in pts)}" fill="none" stroke="{color}" stroke-width="1.3"/>'


def support_svg(x, y, sup):
    if not sup:
        return ""
    if sup.get("type") == "Spring":
        lab = ", ".join(f"{k}={g(sup.get(k, 0))}" for k in ("kx", "ky", "cz") if sup.get(k))
        return zigzag(x, y, x, y + 26, INK) + f'<line x1="{x-8}" y1="{y+26}" x2="{x+8}" y2="{y+26}" stroke="{INK}" stroke-width="1.5"/>' \
            + f'<text x="{x+8:.1f}" y="{y+40:.1f}" font-size="9" fill="{MUTED}">{esc(lab)}</text>'
    ux, uy, rz = sup.get("ux"), sup.get("uy"), sup.get("rz")
    out = []
    if ux and uy and rz:
        out.append(f'<line x1="{x-10}" y1="{y}" x2="{x+10}" y2="{y}" stroke="{INK}" stroke-width="3"/>')
        for i in range(5):
            xx = x - 10 + i * 5
            out.append(f'<line x1="{xx}" y1="{y}" x2="{xx-5}" y2="{y+7}" stroke="{INK}" stroke-width="1"/>')
    elif ux and uy:
        out.append(f'<path d="M{x},{y} L{x-8},{y+12} L{x+8},{y+12} Z" fill="none" stroke="{INK}" stroke-width="1.4"/>'
                   f'<line x1="{x-11}" y1="{y+12}" x2="{x+11}" y2="{y+12}" stroke="{INK}" stroke-width="1.4"/>')
    elif uy and not ux and not rz:
        out.append(f'<path d="M{x},{y} L{x-8},{y+11} L{x+8},{y+11} Z" fill="none" stroke="{INK}" stroke-width="1.4"/>'
                   f'<circle cx="{x-4}" cy="{y+14}" r="2.5" fill="none" stroke="{INK}"/><circle cx="{x+4}" cy="{y+14}" r="2.5" fill="none" stroke="{INK}"/>'
                   f'<line x1="{x-11}" y1="{y+17}" x2="{x+11}" y2="{y+17}" stroke="{INK}" stroke-width="1.2"/>')
    else:
        fixed = "+".join(k for k in ("ux", "uy", "rz") if sup.get(k))
        out.append(f'<rect x="{x-6}" y="{y-6}" width="12" height="12" fill="none" stroke="{INK}" stroke-width="1.4"/>'
                   f'<text x="{x+9}" y="{y+16}" font-size="9" fill="{MUTED}">{esc(fixed)}</text>')
    return "".join(out)


def element_load_shapes(m, e, load):
    """Returns (kind, data) in model coords: distributed polygon / point / moment."""
    (ax, ay), (cx, cy), L = m.geom(e)
    nx, ny = -cy, cx
    local = load.get("direction") == "Local"
    t = load.get("type")
    l1 = load.get("l1", 0) or 0
    if t in ("Uniform", "Trapezoidal"):
        l2 = load.get("l2", 0) or 0
        x_from, x_to = l1, L - l2
        if t == "Uniform":
            wy1 = wy2 = load.get("wy", 0) or 0
            wx1 = wx2 = load.get("wx", 0) or 0
        else:
            wy1, wy2 = load.get("wy1", 0) or 0, load.get("wy2", 0) or 0
            wx1, wx2 = load.get("wx1", 0) or 0, load.get("wx2", 0) or 0
        # transverse direction of the load vector
        dirx, diry = (nx, ny) if local else (0.0, 1.0)
        axx, axy = (cx, cy) if local else (1.0, 0.0)
        return {"kind": "dist", "a": (ax + cx * x_from, ay + cy * x_from), "b": (ax + cx * x_to, ay + cy * x_to),
                "w": (wy1, wy2), "wx": (wx1, wx2), "dir": (dirx, diry), "axdir": (axx, axy), "local": local}
    if t == "Point":
        px, py = ax + cx * l1, ay + cy * l1
        fx, fy = load.get("fx", 0) or 0, load.get("fy", 0) or 0
        if local:
            fx, fy = fx * cx + fy * nx, fx * cy + fy * ny
        return {"kind": "point", "p": (px, py), "f": (fx, fy), "mz": load.get("mz", 0) or 0}
    return None


def scheme_svg(m, case, title):
    pts = [(n["x"], n["y"]) for n in m.nodes.values()]
    v = View(pts, pad=60)
    out = [f'<svg viewBox="0 0 {W} {H}" width="{W}" height="{H}" class="fig"><text x="6" y="14" font-size="11" fill="{MUTED}">{esc(title)}</text>']
    for e in m.elements:
        a, b = m.nodes[e["startNodeLabel"]], m.nodes[e["endNodeLabel"]]
        (x1, y1), (x2, y2) = v.p(a["x"], a["y"]), v.p(b["x"], b["y"])
        if e["type"] == "Spring":
            out.append(zigzag(x1, y1, x2, y2, INK))
            out.append(f'<text x="{(x1+x2)/2+6:.1f}" y="{(y1+y2)/2-6:.1f}" font-size="9" fill="{MUTED}">{esc(e["label"])} K={g(e.get("K", 0))}</text>')
            continue
        out.append(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{INK}" stroke-width="2.4"/>')
        out.append(f'<text x="{(x1+x2)/2+4:.1f}" y="{(y1+y2)/2+13:.1f}" font-size="9" fill="{MUTED}">{esc(e["label"])}</text>')
        rel = e.get("endRelease", "NoRelease")
        L = math.hypot(x2 - x1, y2 - y1) or 1
        for end, (px, py), sgn in (("Start", (x1, y1), 1), ("End", (x2, y2), -1)):
            if rel == "FullRelease" or rel == f"{end}Release":
                cx, cy = px + sgn * (x2 - x1) / L * 7, py + sgn * (y2 - y1) / L * 7
                out.append(f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="3.5" fill="#fff" stroke="{INK}" stroke-width="1.3"/>')
    for n in m.nodes.values():
        x, y = v.p(n["x"], n["y"])
        out.append(support_svg(x, y, n.get("support")))
        out.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="2.2" fill="{INK}"/><text x="{x-14:.1f}" y="{y-6:.1f}" font-size="9" fill="{INK}">{esc(n["label"])}</text>')
    if case is not None:
        dist = []
        for ld in case.get("elementLoads", []):
            e = next((x for x in m.elements if x["label"] == ld["elementLabel"]), None)
            s = element_load_shapes(m, e, ld) if e else None
            if s:
                dist.append(s)
        wmax = max([abs(w) for s in dist if s["kind"] == "dist" for w in s["w"]] + [1e-12])
        for s in dist:
            if s["kind"] == "dist":
                (ax, ay), (bx, by) = v.p(*s["a"]), v.p(*s["b"])
                dx, dy = s["dir"][0], -s["dir"][1]  # screen
                if any(abs(w) > 0 for w in s["w"]):
                    h1, h2 = (-s["w"][0] / wmax * 30, -s["w"][1] / wmax * 30)
                    ta, tb = (ax + dx * h1, ay + dy * h1), (bx + dx * h2, by + dy * h2)
                    out.append(f'<path d="M{ax:.1f},{ay:.1f} L{ta[0]:.1f},{ta[1]:.1f} L{tb[0]:.1f},{tb[1]:.1f} L{bx:.1f},{by:.1f} Z" fill="{LOAD}" fill-opacity="0.08" stroke="{LOAD}" stroke-width="0.8"/>')
                    k = max(2, int(math.hypot(bx - ax, by - ay) / 22))
                    for i in range(k + 1):
                        t = i / k
                        hx, hy = ax + (bx - ax) * t, ay + (by - ay) * t
                        hh = h1 + (h2 - h1) * t
                        if abs(hh) > 2:
                            out.append(arrow(hx + dx * hh, hy + dy * hh, hx, hy, LOAD, 1, 4))
                    lab = g(s["w"][0]) if s["w"][0] == s["w"][1] else f"{g(s['w'][0])} → {g(s['w'][1])}"
                    out.append(f'<text x="{(ta[0]+tb[0])/2:.1f}" y="{min(ta[1], tb[1]) - 4:.1f}" font-size="10" fill="{LOAD}" text-anchor="middle">w{"y" if s["local"] else "Y"} = {esc(lab)}</text>')
                if any(abs(w) > 0 for w in s["wx"]):
                    mx, my = (ax + bx) / 2, (ay + by) / 2
                    ux, uy = s["axdir"][0], -s["axdir"][1]
                    sg = 1 if (s["wx"][0] + s["wx"][1]) > 0 else -1
                    out.append(arrow(mx - ux * 14 * sg, my - uy * 14 * sg + 8, mx + ux * 14 * sg, my + uy * 14 * sg + 8, LOAD, 1, 4))
                    out.append(f'<text x="{mx:.1f}" y="{my + 22:.1f}" font-size="9" fill="{LOAD}" text-anchor="middle">wx = {g(s["wx"][0])} → {g(s["wx"][1])}</text>')
            elif s["kind"] == "point":
                x, y = v.p(*s["p"])
                fx, fy = s["f"]
                F = math.hypot(fx, fy)
                if F > 0:
                    out.append(arrow(x - fx / F * 34, y + fy / F * 34, x, y, LOAD, 1.6))
                    out.append(f'<text x="{x - fx / F * 34 + 4:.1f}" y="{y + fy / F * 34 - 4:.1f}" font-size="10" fill="{LOAD}">F = ({g(fx)}, {g(fy)})</text>')
                if s["mz"]:
                    out.append(moment_symbol(x, y, s["mz"], LOAD, f"M = {g(s['mz'])}"))
        for nl in case.get("nodalLoads", []):
            n = m.nodes.get(nl["nodeLabel"])
            if not n:
                continue
            x, y = v.p(n["x"], n["y"])
            fx, fy = nl.get("fx", 0) or 0, nl.get("fy", 0) or 0
            F = math.hypot(fx, fy)
            if F > 0:
                out.append(arrow(x - fx / F * 34, y + fy / F * 34, x, y, LOAD, 1.6))
                out.append(f'<text x="{x - fx / F * 34 + 4:.1f}" y="{y + fy / F * 34 - 4:.1f}" font-size="10" fill="{LOAD}">({g(fx)}, {g(fy)})</text>')
            if nl.get("mz"):
                out.append(moment_symbol(x, y, nl["mz"], LOAD, f"M = {g(nl['mz'])}"))
        for sd in case.get("supportDisplacements", []):
            n = m.nodes.get(sd["nodeLabel"])
            if n:
                x, y = v.p(n["x"], n["y"])
                lab = ", ".join(f"{k}={g(sd.get(k, 0))}" for k in ("ux", "uy", "rz") if sd.get(k))
                out.append(f'<text x="{x+10:.1f}" y="{y+30:.1f}" font-size="10" fill="{LOAD}">Δ {esc(lab)}</text>')
    out.append("</svg>")
    return "".join(out)


def segment_points(m, e, segs, comp):
    """Diagram outline in model coords for component comp (fx/fy/mz) with scale applied later."""
    (ax, ay), (cx, cy), L = m.geom(e)
    vals = []
    for s in segs:
        vals.append((s["x1"], s["startForce"][comp]))
        vals.append((s["x2"], s["endForce"][comp]))
    return (ax, ay), (cx, cy), vals


def diagram_svg(m, res, comp, title):
    frames = [e for e in m.elements if e["type"] == "Frame" and e["label"] in res.get("elementForces", {})]
    allv = [abs(v) for e in frames for s in res["elementForces"][e["label"]] for v in (s["startForce"][comp], s["endForce"][comp])]
    vmax = max(allv + [1e-12])
    scale = 0.16 * m.span / vmax
    pts = [(n["x"], n["y"]) for n in m.nodes.values()]
    polys = []
    for e in frames:
        (ax, ay), (cx, cy), vals = segment_points(m, e, res["elementForces"][e["label"]], comp)
        nx, ny = -cy, cx
        base = [(ax + cx * x, ay + cy * x) for x, _ in vals]
        off = [(bx + nx * val * scale, by + ny * val * scale) for (bx, by), (_, val) in zip(base, vals)]
        pts += off
        polys.append((e, base, off, vals))
    v = View(pts)
    out = [f'<svg viewBox="0 0 {W} {H}" width="{W}" height="{H}" class="fig"><text x="6" y="14" font-size="11" fill="{MUTED}">{esc(title)} — max |{comp}| = {g(vmax)}</text>']
    col = COLOR["M" if comp == "mz" else "V" if comp == "fy" else "N"]
    for e, base, off, vals in polys:
        b = [v.p(*q) for q in base]; o = [v.p(*q) for q in off]
        d = "M" + " L".join(f"{x:.1f},{y:.1f}" for x, y in b) + " L" + " L".join(f"{x:.1f},{y:.1f}" for x, y in reversed(o)) + " Z"
        out.append(f'<path d="{d}" fill="{col}" fill-opacity="0.14" stroke="none"/>')
        out.append(f'<polyline points="{" ".join(f"{x:.1f},{y:.1f}" for x, y in o)}" fill="none" stroke="{col}" stroke-width="1.6"/>')
        out.append(f'<line x1="{b[0][0]:.1f}" y1="{b[0][1]:.1f}" x2="{b[-1][0]:.1f}" y2="{b[-1][1]:.1f}" stroke="{INK}" stroke-width="1.6"/>')
        # labels: element ends + extreme
        idx = {0, len(vals) - 1, max(range(len(vals)), key=lambda i: abs(vals[i][1]))}
        placed = []
        for i in sorted(idx):
            val = vals[i][1]
            if abs(val) < vmax * 1e-6 and i not in (0, len(vals) - 1):
                continue
            x, y = o[i]
            if any(abs(x - px) < 30 and abs(y - py) < 10 for px, py in placed):
                continue
            placed.append((x, y))
            right = x > W - 70
            out.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="2" fill="{col}"/><text x="{x + (-3 if right else 3):.1f}" y="{y-4:.1f}" font-size="9.5" fill="{INK}" text-anchor="{"end" if right else "start"}">{esc(g(val))}</text>')
    for n in m.nodes.values():
        x, y = v.p(n["x"], n["y"])
        out.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="1.8" fill="{INK}"/>')
    out.append("</svg>")
    return "".join(out), vmax


def deformed_svg(m, res, title):
    disp = res.get("nodeDisplacements", {})
    # scale from nodes AND element interiors (a beam on supports has zero node translations)
    seg_d = [h for segs in res.get("elementForces", {}).values() for s in segs
             for dd in (s["startDisplacement"], s["endDisplacement"])
             for h in [math.hypot(fnum(dd["ux"]), fnum(dd["uy"]))] if math.isfinite(h)]
    dmax = max([math.hypot(d["ux"], d["uy"]) for d in disp.values()] + seg_d + [1e-15])
    scale = 0.1 * m.span / dmax
    pts = [(n["x"], n["y"]) for n in m.nodes.values()]
    defo = {k: (m.nodes[k]["x"] + d["ux"] * scale, m.nodes[k]["y"] + d["uy"] * scale) for k, d in disp.items() if k in m.nodes}
    # interior from segments: try local (ux along, uy normal) interpretation, verified on the element end
    curves = []
    for e in m.elements:
        segs = res.get("elementForces", {}).get(e["label"])
        a, b = e["startNodeLabel"], e["endNodeLabel"]
        finite = segs and all(math.isfinite(fnum(dd[k])) for s in segs for dd in (s["startDisplacement"], s["endDisplacement"]) for k in ("ux", "uy"))
        if e["type"] != "Frame" or not finite or a not in disp or b not in disp:
            if a in defo and b in defo:
                curves.append([defo[a], defo[b]])
            continue
        (ax, ay), (cx, cy), L = m.geom(e)
        nx, ny = -cy, cx
        endd = segs[-1]["endDisplacement"]
        gx, gy = disp[b]["ux"], disp[b]["uy"]
        local_ok = abs(endd["ux"] * cx + endd["uy"] * nx - gx) <= 1e-6 * max(dmax, 1e-12) + 1e-12 and \
                   abs(endd["ux"] * cy + endd["uy"] * ny - gy) <= 1e-6 * max(dmax, 1e-12) + 1e-12
        global_ok = abs(endd["ux"] - gx) <= 1e-6 * max(dmax, 1e-12) + 1e-12 and abs(endd["uy"] - gy) <= 1e-6 * max(dmax, 1e-12) + 1e-12
        if not (local_ok or global_ok):
            curves.append([defo[a], defo[b]]); continue
        c = []
        for s in segs:
            for x, dd in ((s["x1"], s["startDisplacement"]), (s["x2"], s["endDisplacement"])):
                ux, uy = (dd["ux"] * cx + dd["uy"] * nx, dd["ux"] * cy + dd["uy"] * ny) if local_ok else (dd["ux"], dd["uy"])
                c.append((ax + cx * x + ux * scale, ay + cy * x + uy * scale))
        curves.append(c)
    for c in curves:
        pts += c
    v = View(pts, pad=56)
    out = [f'<svg viewBox="0 0 {W} {H}" width="{W}" height="{H}" class="fig"><text x="6" y="14" font-size="11" fill="{MUTED}">{esc(title)} — deformace (max {g(dmax)}, zvětšeno) a reakce</text>']
    for e in m.elements:
        a, b = m.nodes[e["startNodeLabel"]], m.nodes[e["endNodeLabel"]]
        (x1, y1), (x2, y2) = v.p(a["x"], a["y"]), v.p(b["x"], b["y"])
        out.append(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{GRID}" stroke-width="2.4"/>')
    for c in curves:
        out.append(f'<polyline points="{" ".join("%.1f,%.1f" % v.p(*q) for q in c)}" fill="none" stroke="{DEF}" stroke-width="1.6" stroke-dasharray="5 3"/>')
    lines = []
    for k, r in res.get("supportReactions", {}).items():
        n = m.nodes.get(k)
        if not n:
            continue
        x, y = v.p(n["x"], n["y"])
        out.append(support_svg(x, y, n.get("support")))
        parts = []
        if abs(r["fx"]) > 1e-9:
            sg = 1 if r["fx"] > 0 else -1
            out.append(arrow(x - sg * 30, y + 22, x - sg * 6, y + 22, REACT, 1.4)); parts.append(f"Rx={g(r['fx'])}")
        if abs(r["fy"]) > 1e-9:
            sg = 1 if r["fy"] > 0 else -1
            out.append(arrow(x + 14, y + sg * 34, x + 14, y + sg * 8, REACT, 1.4)); parts.append(f"Ry={g(r['fy'])}")
        if abs(r["mz"]) > 1e-9:
            parts.append(f"Mz={g(r['mz'])}")
        out.append(f'<text x="{x + 6:.1f}" y="{y - 8:.1f}" font-size="9" fill="{REACT}">{esc(k)}</text>')
        lines.append(f"{k}: {', '.join(parts) or '0'}")
    for i, t in enumerate(lines):   # reactions listed in the corner, no overlaps at the nodes
        out.append(f'<text x="8" y="{H - 8 - 13 * (len(lines) - 1 - i):.1f}" font-size="10" fill="{REACT}">{esc(t)}</text>')
    out.append("</svg>")
    return "".join(out)


def checks_table(resp, exp):
    rows = []
    npass = 0
    for c in exp["checks"]:
        ok, msg = run_examples.check(resp, c)
        npass += ok
        rows.append(f'<tr class="{"ok" if ok else "fail"}"><td>{"✓" if ok else "✗"}</td><td><code>{esc(c["path"])}</code></td><td>{esc(msg)}</td></tr>')
    return npass, ('<table class="checks"><thead><tr><th></th><th>cesta</th><th>vypočteno (očekáváno ± tol)</th></tr></thead><tbody>'
                   + "".join(rows) + "</tbody></table>")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--url", default=os.environ.get("FEM_HOST_URL", "http://localhost:9300"))
    ap.add_argument("--offline", action="store_true")
    ap.add_argument("--out", default=os.path.join(HERE, "report.html"))
    ap.add_argument("names", nargs="*")
    a = ap.parse_args()
    names = a.names or sorted(d for d in os.listdir(HERE) if os.path.isfile(os.path.join(HERE, d, "expected.json")))

    toc, body, total_ok = [], [], 0
    for name in names:
        d = os.path.join(HERE, name)
        req = json.load(open(os.path.join(d, "request.json"), encoding="utf-8"))
        exp = json.load(open(os.path.join(d, "expected.json"), encoding="utf-8"))
        resp = (json.load(open(os.path.join(d, "response.json"), encoding="utf-8")) if a.offline
                else run_examples.post(a.url, json.dumps(req)))
        m = Model(req)
        npass, table = checks_table(resp, exp)
        ok = npass == len(exp["checks"]) and resp.get("status") == "Successful"
        total_ok += ok
        toc.append(f'<li class="{"ok" if ok else "fail"}"><a href="#{esc(name)}">{esc(name)}</a> {npass}/{len(exp["checks"])}</li>')
        figs = []
        for case in m.cases or [None]:
            figs.append(scheme_svg(m, case, f"Konstrukce a zatížení — {case['label']}" if case else "Konstrukce"))
        results = [("ZS " + k, r) for k, r in resp.get("loadCaseResults", {}).items()] + \
                  [("Kombinace " + k, r) for k, r in resp.get("loadCombinationResults", {}).items()]
        for title, r in results:
            figs.append(deformed_svg(m, r, title))
            for comp, lab in (("mz", "M (mz)"), ("fy", "V (fy)"), ("fx", "N (fx)")):
                svg, vmax = diagram_svg(m, r, comp, f"{title} — {lab}")
                if comp == "fx" and vmax < 1e-9:
                    continue
                if vmax < 1e-12 and comp != "mz":
                    continue
                figs.append(svg)
        combos = "".join(f"<li>{esc(c['label'])}: " + " + ".join(f"{g(f)}·{esc(k)}" for k, f in c.get("factors", {}).items()) + "</li>"
                         for c in m.combos)
        body.append(f'<section id="{esc(name)}"><h2>{esc(name)} <span class="badge {"ok" if ok else "fail"}">{npass}/{len(exp["checks"])} kontrol</span></h2>'
                    f'<p class="desc">{esc(exp.get("description", ""))}</p><p class="src">{esc(exp.get("source", ""))}'
                    f'{" · síť " + str(m.settings.get("meshSegments")) + " segmentů" if m.settings.get("meshSegments") else ""}</p>'
                    + (f"<ul class='combos'>{combos}</ul>" if combos else "")
                    + f'<div class="figs">{"".join(figs)}</div>{table}</section>')

    page = f"""<!doctype html><html lang="cs"><head><meta charset="utf-8"><title>fem2d – kontrola příkladů</title>
<style>
body{{font-family:"DM Sans",Segoe UI,sans-serif;color:{INK};background:#f6f4f0;margin:0;padding:20px 28px}}
h1{{font-size:20px;margin:0 0 6px}} h2{{font-size:16px;margin:0 0 4px}}
section{{background:#fff;border:1px solid {GRID};border-radius:8px;padding:14px 16px;margin:16px 0}}
.figs{{display:flex;flex-wrap:wrap;gap:8px}} .fig{{border:1px solid {GRID};border-radius:6px;background:#fff;max-width:100%;height:auto}}
.desc{{margin:2px 0;font-size:13px}} .src{{margin:0 0 8px;font-size:11px;color:{MUTED}}}
.badge{{font-size:11px;padding:2px 8px;border-radius:10px;margin-left:8px;vertical-align:middle}}
.badge.ok,li.ok a{{color:#1b7f5a}} .badge.ok{{background:#e6f4ee}} .badge.fail,li.fail a{{color:#b3261e}} .badge.fail{{background:#fbe9e7}}
.checks{{border-collapse:collapse;font-size:11.5px;margin-top:10px;width:100%}}
.checks td,.checks th{{border-bottom:1px solid {GRID};padding:3px 6px;text-align:left}} .checks code{{font-family:"JetBrains Mono",Consolas,monospace;font-size:11px}}
tr.ok td:first-child{{color:#1b7f5a}} tr.fail td{{background:#fbe9e7}}
.toc{{columns:3;font-size:13px;margin:8px 0 0}} .legend{{font-size:12px;color:{MUTED}}}
</style></head><body>
<h1>fem2d (FEALiTE2D.Api) — kontrola testovacích příkladů</h1>
<div class="legend">{total_ok}/{len(names)} příkladů v pořádku · {'uložené odpovědi' if a.offline else esc(a.url)} ·
zatížení <span style="color:{LOAD}">červeně</span>, reakce <span style="color:{REACT}">zeleně</span>, deformace <span style="color:{DEF}">modře čárkovaně</span> (zvětšená);
průběhy na levé normále prvku (od začátku ke konci) s hodnotou +v → ohyb je vždy na tažené straně, znaménko hodnoty ale závisí na orientaci prvku (u prvku zleva doprava je kladný ohyb mz &lt; 0);
jednotky jsou jednotky vstupu příkladu.</div>
<ul class="toc">{"".join(toc)}</ul>
{"".join(body)}
</body></html>"""
    open(a.out, "w", encoding="utf-8").write(page)
    print(f"{a.out}: {total_ok}/{len(names)} examples ok")
    return 0 if total_ok == len(names) else 1


if __name__ == "__main__":
    sys.exit(main())
