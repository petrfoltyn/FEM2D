# Testovací příklady fem2d (FEALiTE2D.Api)

Příklady pro službu **fem2d** (kontejner s `FEALiTE2D.Api`, `POST /api/v1/analyze`). Každá složka:

- `request.json` — tělo požadavku (kontrakt `AnalysisRequest`, JSON camelCase),
- `expected.json` — očekávané výsledky jako kontroly cest v odpovědi s tolerancí.

| Příklad | Co ověřuje | Zdroj očekávaných hodnot |
|---|---|---|
| continuous-beam | spojitý nosník 3 pole, vetknutí + klouby, posuny a reakce | StructureTest.TestContinousBeam (ověřeno v Robotu) |
| fixed-end-beam | oboustranně vetknutý nosník, q: R = qL/2, M = qL²/12, qL²/24 | analyticky (StructureTest.TestFixedEndBeam nemá asserty) |
| hinged-beam-internal-forces | průběh V a M po segmentech, částečné lichoběžníkové zatížení | StructureTest.TestInternalForces |
| beam-start-release | kloub na začátku prvku (uvolnění konce) | StructureTest.TestBeamWihEndRelease |
| beam-release-springs-as-supports, beam-nodal-spring-supports | podpory jako tuhé pružiny / pružné podpory | StructureTest.TestBeamWihEndRelease… |
| pp-uniform-full, pp-uniform-partial-edge, pp-uniform-partial-center | rovnoměrné zatížení celé / části prvku, síly na hranách segmentů | PostProcessorTest |
| pp-trapezoidal-full | trojúhelníkové zatížení přes celý prvek | PostProcessorTest |
| pp-trapezoidal-partial | trojúhelník na části prvku (`l1`, `l2` od začátku / konce) | analyticky (test nemá asserty) |
| classic-frame, steel-frame-kip-inch(-settlement) | rámy, pokles podpory | StructureTest.TestStructure, TestStructure2… |
| frame-load-combination | dva zatěžovací stavy + kombinace (`loadCombinationResults`) | StructureTest.TestStructureWithLoadCombination |
| springs-planar, springs-series, truss-spring-support | pružinové prvky, příhradovina | StructureTest.TestSprings… |

Konvence ověřené příklady: kladný ohyb (tah dole) = **záporné `mz`** koncové síly segmentu, moment nad vetknutím
kladný; `l2` u lichoběžníkového / rovnoměrného zatížení je vzdálenost **od konce** prvku.

## Spuštění

```bash
docker run -d --name fem2d-ex -p 9301:8080 fem2d:0.1.0
python run_examples.py --url http://localhost:9301          # všechny, -v vypíše každou kontrolu
python run_examples.py --url http://localhost:9301 continuous-beam fixed-end-beam
python run_examples.py --save                                # uloží response.json ke každému příkladu
python run_examples.py --offline                             # vyhodnotí uložené response.json (bez HTTP)
```

Kontejner na NASu poslouchá jen na `127.0.0.1:9300`: požadavky poslat na NASu přes `curl` do `response.json`,
stáhnout a vyhodnotit `--offline`.

## Grafická kontrola — `report.html`

```bash
python run_examples.py --url http://localhost:9301 --save    # uloží odpovědi
python make_report.py --offline                              # → report.html (nebo --url … bez --save)
```

Pro každý příklad: **schéma konstrukce** (pruty, podpory, klouby / uvolnění konců, pružiny) se **zatížením** po
zatěžovacích stavech (spojitá, lichoběžníková, osamělé síly a momenty, uzlová zatížení, pokles podpory), pro každý
stav a kombinaci **deformace + reakce** a průběhy **M, V, N** s hodnotami na koncích prvků a v extrémech, pod tím
tabulka kontrol z `expected.json` (✓ / ✗ s vypočtenou hodnotou). Průběhy jsou vždy na tažené straně; znaménko
hodnoty závisí na orientaci prvku. `response.json` a `report.html` jsou generované (v `.gitignore`).

Zjištění: pružinové prvky (`Spring`) vracejí posuny po segmentech jako `"NaN"` (text) — report je přeskočí.

## Formát kontroly

```jsonc
{ "path": "loadCaseResults.live.supportReactions.n1.fy", "expected": 146.33, "tol": 1e-6 }
{ "path": "loadCaseResults.live.supportReactions.n2", "absent": true }      // klíč nesmí existovat
{ "path": "loadCaseResults.live.elementForces.e1", "count": 11 }             // počet segmentů
// [0], [-1] = index; [x2=0.5] = segment končící v x = 0,5
```

16 příkladů je převedeno z `FEALiTE2D.Tests/Api/JsonWorkflowTest.cs` skriptem `make_examples.py`
(`python make_examples.py ..` je přegeneruje po změně testů); `fixed-end-beam` a `pp-trapezoidal-partial` jsou ruční.
