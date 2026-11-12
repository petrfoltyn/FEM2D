#!/usr/bin/env python3
"""Runs the fem2d test examples against a running FEALiTE2D.Api (container fem2d).

  python run_examples.py                          # http://localhost:9300, all examples
  python run_examples.py --url http://localhost:9301 continuous-beam fixed-end-beam
  python run_examples.py --save                   # also writes <example>/response.json
  python run_examples.py --offline                # evaluates saved <example>/response.json (no HTTP)

Every example is a folder with request.json (POST /api/v1/analyze body) and expected.json:
  { "name", "description", "source", "checks": [ check, ... ] }
check:
  { "path": "loadCaseResults.live.supportReactions.n1.fy", "expected": 146.33, "tol": 1e-6 }
  { "path": "loadCaseResults.live.supportReactions.n2", "absent": true }
  { "path": "loadCaseResults.live.elementForces.e1", "count": 11 }
path: dotted keys (JSON camelCase), list index [0] / [-1], segment by its end [x2=0.5].
Standard library only. Exit code 0 = all checks passed.
"""
import argparse, json, os, re, sys, urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
MISSING = object()


def resolve(doc, path):
    cur = doc
    for key, sel in re.findall(r"([^.\[\]]+)|\[([^\]]+)\]", path):
        if key:
            if not isinstance(cur, dict) or key not in cur:
                return MISSING
            cur = cur[key]
        else:
            if not isinstance(cur, list):
                return MISSING
            m = re.fullmatch(r"(\w+)=(.+)", sel)
            if m:  # [x2=0.5] - first item whose field equals the value
                field, value = m.group(1), float(m.group(2))
                hit = [it for it in cur if isinstance(it, dict) and abs(it.get(field, float("nan")) - value) < 1e-6]
                if not hit:
                    return MISSING
                cur = hit[0]
            else:
                i = int(sel)
                if not -len(cur) <= i < len(cur):
                    return MISSING
                cur = cur[i]
    return cur


def check(response, c):
    value = resolve(response, c["path"])
    if c.get("absent"):
        return value is MISSING, "absent" if value is MISSING else "present"
    if value is MISSING:
        return False, "missing"
    if "count" in c:
        return len(value) == c["count"], f"count {len(value)} (expected {c['count']})"
    ok = abs(value - c["expected"]) <= c.get("tol", 1e-6)
    return ok, f"{value!r} (expected {c['expected']!r} ± {c.get('tol', 1e-6)})"


def post(url, body):
    req = urllib.request.Request(url.rstrip("/") + "/api/v1/analyze", data=body.encode("utf-8"),
                                 headers={"Content-Type": "application/json"}, method="POST")
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode("utf-8"))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--url", default=os.environ.get("FEM_HOST_URL", "http://localhost:9300"))
    ap.add_argument("--save", action="store_true", help="write response.json next to each example")
    ap.add_argument("--offline", action="store_true", help="evaluate saved response.json instead of calling the service")
    ap.add_argument("--verbose", "-v", action="store_true", help="print every check")
    ap.add_argument("names", nargs="*")
    a = ap.parse_args()

    names = a.names or sorted(d for d in os.listdir(HERE) if os.path.isfile(os.path.join(HERE, d, "expected.json")))
    total_fail = 0
    for name in names:
        d = os.path.join(HERE, name)
        expected = json.load(open(os.path.join(d, "expected.json"), encoding="utf-8"))
        try:
            response = (json.load(open(os.path.join(d, "response.json"), encoding="utf-8")) if a.offline
                        else post(a.url, open(os.path.join(d, "request.json"), encoding="utf-8").read()))
        except Exception as e:  # connection, HTTP error
            print(f"FAIL {name}: request failed: {e}")
            total_fail += 1
            continue
        if a.save:
            json.dump(response, open(os.path.join(d, "response.json"), "w", encoding="utf-8"), indent=2)
        if response.get("status") != "Successful":
            print(f"FAIL {name}: status {response.get('status')} {response.get('errors')}")
            total_fail += 1
            continue
        fails = []
        for c in expected["checks"]:
            ok, msg = check(response, c)
            if a.verbose or not ok:
                print(f"  {'ok  ' if ok else 'FAIL'} {c['path']}: {msg}")
            if not ok:
                fails.append(c)
        total_fail += bool(fails)
        n = len(expected["checks"])
        print(f"{'FAIL' if fails else 'ok  '} {name}: {n - len(fails)}/{n} checks")
    print(f"\n{len(names) - total_fail}/{len(names)} examples passed ({'saved responses' if a.offline else a.url})")
    return 1 if total_fail else 0


if __name__ == "__main__":
    sys.exit(main())
