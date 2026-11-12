"""Usage: python make_examples.py <repo root>  (regenerates the 16 examples mirrored from JsonWorkflowTest;
fixed-end-beam and pp-trapezoidal-partial are hand-made with analytical values and are not touched).

Converts FEALiTE2D.Tests/Api/JsonWorkflowTest.cs (fixtures + NUnit asserts) into test examples:
examples/<name>/request.json + expected.json (path checks with tolerance)."""
import json, os, re, shutil, sys

repo = sys.argv[1]
src = os.path.join(repo, "FEALiTE2D.Tests", "Api", "JsonWorkflowTest.cs")
fixtures = os.path.join(repo, "FEALiTE2D.Tests", "Api", "Fixtures")
out = os.path.join(repo, "examples")
code = open(src, encoding="utf-8").read()

def camel(name):
    return name[0].lower() + name[1:]

def num(expr, nums):
    e = expr.strip()
    for k, v in nums.items():
        e = re.sub(r"\b%s\b" % re.escape(k), repr(v), e)
    if not re.fullmatch(r"[0-9eE.+\-*/() ]+", e):
        raise ValueError("not numeric: " + expr)
    return float(eval(e))

def split_args(s):
    args, depth, cur = [], 0, ""
    for ch in s:
        if ch in "([": depth += 1
        if ch in ")]": depth -= 1
        if ch == "," and depth == 0:
            args.append(cur); cur = ""
        else:
            cur += ch
    if cur.strip(): args.append(cur)
    return [a.strip() for a in args]

def translate(expr, alias):
    """C# access path -> JSON path (camelCase, [i], [-1], [x2=..])."""
    e = expr.strip()
    m = re.match(r"(\w+)(.*)", e)
    head, rest = m.group(1), m.group(2)
    if head not in alias:
        raise ValueError("unknown variable " + head + " in " + expr)
    path = alias[head]
    rest = rest.replace("[^1]", "[-1]")
    rest = re.sub(r'\["([^"]+)"\]', r".\1", rest)
    rest = re.sub(r"\.([A-Z]\w*)", lambda mm: "." + camel(mm.group(1)), rest)
    return path + rest

tests = re.findall(r"public async Task (\w+)\(\)\s*\{(.*?)\n    \}", code, re.S)
examples = []
for name, body in tests:
    alias, nums, checks = {}, {}, []
    fixture = None
    for line in body.split("\n"):
        s = line.strip()
        if s.startswith("//") or not s:
            continue
        m = re.match(r'var (\w+) = await PostExpectSuccess\("([^"]+)"(?:, "([^"]+)")?\);', s)
        if m:
            fixture = m.group(2)
            alias[m.group(1)] = f"loadCaseResults.{m.group(3)}" if m.group(3) else ""
            continue
        m = re.match(r'var (\w+) *= *(\w+)\.(LoadCaseResults|LoadCombinationResults)\["([^"]+)"\];', s)
        if m:
            alias[m.group(1)] = f"{camel(m.group(3))}.{m.group(4)}"
            continue
        m = re.match(r'var (\w+) *= *FindSegmentEndingAt\((\w+), ([0-9.]+)\)(\.EndForce)?;', s)
        if m:
            alias[m.group(1)] = f"{alias[m.group(2)]}[x2={m.group(3)}]" + (".endForce" if m.group(4) else "")
            continue
        m = re.match(r'var (\w+) *= *(\w+)(\[.*\]|\.\w+\[.*\]);', s)
        if m and m.group(2) in alias:
            alias[m.group(1)] = translate(m.group(2) + m.group(3), alias)
            continue
        m = re.match(r'var (\w+) *= *([0-9.+\-*/ ]+);', s)
        if m:
            nums[m.group(1)] = num(m.group(2), nums)
            continue
        m = re.match(r'AssertDisplacement\((.*)\);', s)
        if m:
            a = split_args(m.group(1)); tol = num(a[4], nums) if len(a) > 4 else 1e-9
            for comp, v in zip(("ux", "uy", "rz"), a[1:4]):
                checks.append({"path": translate(a[0], alias) + "." + comp, "expected": num(v, nums), "tol": tol})
            continue
        m = re.match(r'AssertForce\((.*)\);?', s)
        if m or s.startswith("AssertForce("):
            # may span several lines - collect
            pass
        m = re.match(r'Assert\.That\((.*?)\.ContainsKey\("([^"]+)"\), Is\.False\);', s)
        if m:
            checks.append({"path": translate(m.group(1), alias) + "." + m.group(2), "absent": True})
            continue
        m = re.match(r'Assert\.That\((\w+), Has\.Count\.EqualTo\((\d+)\)\);', s)
        if m:
            checks.append({"path": alias[m.group(1)], "count": int(m.group(2))})
            continue
        m = re.match(r'Assert\.That\(([^,]+),\s*Is\.EqualTo\(\s*(.+?)\)\.Within\(([^)]+)\)', s)
        if m:
            checks.append({"path": translate(m.group(1), alias), "expected": num(m.group(2), nums), "tol": num(m.group(3), nums)})
            continue
    # AssertForce calls (possibly multi-line)
    for call in re.findall(r"AssertForce\((.*?)\);", body, re.S):
        a = split_args(" ".join(call.split()))
        tol = num(a[4], nums) if len(a) > 4 else 1e-6
        for comp, v in zip(("fx", "fy", "mz"), a[1:4]):
            checks.append({"path": translate(a[0], alias) + "." + comp, "expected": num(v, nums), "tol": tol})
    if fixture is None:
        raise SystemExit("no fixture in " + name)
    examples.append((name, fixture, checks))

os.makedirs(out, exist_ok=True)
index = []
for name, fixture, checks in examples:
    stem = fixture[:-5]
    d = os.path.join(out, stem)
    os.makedirs(d, exist_ok=True)
    shutil.copyfile(os.path.join(fixtures, fixture), os.path.join(d, "request.json"))
    comment = json.load(open(os.path.join(fixtures, fixture), encoding="utf-8")).get("_comment", "")
    exp = {"name": stem, "description": comment,
           "source": f"FEALiTE2D.Tests/Api/JsonWorkflowTest.{name} (fixture Api/Fixtures/{fixture})",
           "checks": checks}
    json.dump(exp, open(os.path.join(d, "expected.json"), "w", encoding="utf-8"), indent=2, ensure_ascii=False)
    index.append((stem, len(checks)))
for stem, n in index:
    print(f"{stem:42s} {n:3d} checks")
