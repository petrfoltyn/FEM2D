# FEALiTE2D — REST API Design

Návrh stateless cloudové služby nad knihovnou FEALiTE2D.

> **Stav implementace** (.NET 10 LTS, 44/44 testů zelených)
> - ✅ `FEALiTE2D.Api.Contracts` — DTOs, polymorfismus, mapování, validátor, `AnalysisService` orchestrátor
> - ✅ `FEALiTE2D.Api` — ASP.NET Core minimal API, `POST /api/v1/analyze`, `GET /health`, Swagger UI
> - ✅ `FEALiTE2D.Client` — typed HttpClient + DI extension
> - ⏳ Docker / nasazení — plánováno

---

## 1. Architektura

### 1.1 Dvě varianty přístupu

| Vlastnost | **Stateless (implementováno)** | **Stateful / Session** |
|---|---|---|
| Jeden request = celý model + výsledky | ano | ne |
| Stav na serveru | žádný | session/job ID |
| Škálovatelnost | triviální (any pod) | sticky session nebo Redis |
| Vhodné pro | malé-střední modely | velmi velké modely, interaktivní UI |
| Složitost API | nízká | vysoká |

První verze používá **stateless** přístup. Klient pošle celý model v jednom POST, server vrátí všechny výsledky. Stateful variantu lze přidat později bez změny stateless endpointu.

### 1.2 Technologický stack (skutečný)

```
Klient (DCE / curl / FEALiTE2D.Client)
  │  POST /api/v1/analyze  (JSON)
  ▼
[ASP.NET Core 10 minimal API]  (obal nad FEALiTE2D.dll)
  │
  ├── System.Text.Json deserializace (ApiJsonOptions.Default)
  ├── AnalysisService.Analyze(request):
  │     ├── AnalysisRequestValidator.Validate
  │     ├── StructureBuilder.Build  (DTO → FEALiTE2D doménové objekty)
  │     ├── Structure.Solve()
  │     └── ResultMapper.Map        (PostProcessor → response DTO)
  └── 200 (Successful) / 422 (Failure se strukturovaným AnalysisResponse)
```

### 1.3 Projektová struktura

```
FEALiTE2D/                         core knihovna (Solve)
FEALiTE2D.Plotting/                DXF výstup
FEALiTE2D.Api.Contracts/           DTOs + mapping + validator + AnalysisService
FEALiTE2D.Api/                     ASP.NET Core host
FEALiTE2D.Client/                  typed HttpClient pro spotřebitele
FEALiTE2D.Tests/                   NUnit (core + integrace přes WebApplicationFactory)
```

### 1.4 JSON konvence (`ApiJsonOptions.Default`)

- **PropertyNamingPolicy** = camelCase → `label`, `startNodeLabel`, `wy`, `fx`, `mz`
- **Uppercase ponechán** přes `[JsonPropertyName]` tam, kde to dává smysl: `E`, `U`, `A`, `Iy`, `Iz`, `J`, `K`, `R`
- **DictionaryKeyPolicy** = `null` — user labely (např. `"LC1"`, `"ULS1"`, klíče v `factors`) zůstanou doslovné
- **AllowOutOfOrderMetadataProperties** = `true` (.NET 10) — discriminator `"type"` může být kdekoli v objektu
- **DefaultIgnoreCondition** = `WhenWritingNull` — null pole se neserializují
- **NumberHandling** = `AllowNamedFloatingPointLiterals` — povoluje `"NaN"`, `"Infinity"`

---

## 2. Pořadí konstrukce (povinné)

Knihovna vyžaduje toto pořadí — API musí zachovat interní závislosti:

```
1. Nodes          (včetně Support podmínek)
2. Materials
3. Sections       (odkazují na Material)
4. Elements       (odkazují na StartNode, EndNode, Section)
5. LoadCases      (včetně Loads — odkazují na Node/Element)
6. LoadCombinations (odkazují na LoadCase)
7. Settings       (meshing)
── Solve ──
8. Results
```

---

## 3. Stateless API

### Endpoint

```
POST /api/v1/analyze
Content-Type: application/json
```

### 3.1 Request Schema

```json
{
  "nodes": [ ...Node[] ],
  "materials": [ ...Material[] ],
  "sections": [ ...Section[] ],
  "elements": [ ...Element[] ],
  "loadCases": [ ...LoadCase[] ],
  "loadCombinations": [ ...LoadCombination[] ],
  "settings": { ...Settings }
}
```

---

#### 3.1.1 Node

```json
{
  "label": "n1",
  "x": 0.0,
  "y": 0.0,
  "rotationAngle": 0.0,
  "support": null
}
```

Pole `support` je null (volný uzel) nebo jeden ze dvou tvarů:

**Tuhá podpora (NodalSupport):**
```json
{
  "type": "Rigid",
  "ux": true,
  "uy": true,
  "rz": false
}
```

| Pole | Typ | Popis |
|---|---|---|
| `type` | `"Rigid"` | Diskriminátor |
| `ux` | bool | Zablokování posunu X |
| `uy` | bool | Zablokování posunu Y |
| `rz` | bool | Zablokování rotace Z |

Příklady: kloub = `{ux:true,uy:true,rz:false}`, vetknutí = `{ux:true,uy:true,rz:true}`.

**Pruinová podpora (NodalSpringSupport):**
```json
{
  "type": "Spring",
  "kx": 5000.0,
  "ky": 8000.0,
  "cz": 0.0
}
```

| Pole | Typ | Popis |
|---|---|---|
| `type` | `"Spring"` | Diskriminátor |
| `kx` | double | Tuhoot pružiny v X [síla/délka] |
| `ky` | double | Tuhost pružiny v Y [síla/délka] |
| `cz` | double | Rotační tuhost v Z [síla·délka/rad] |

---

#### 3.1.2 Material

```json
{
  "label": "C25",
  "materialType": "Concrete",
  "E": 30000000000.0,
  "U": 0.2,
  "alpha": 0.000010,
  "gama": 25000.0
}
```

| Pole | Typ | Povinné | Popis |
|---|---|---|---|
| `label` | string | ano | Unikátní identifikátor |
| `materialType` | enum | ano | `Concrete`, `Steel`, `Timber`, `Aluminum`, `Userdefined` |
| `E` | double | ano | Modul pružnosti [Pa] |
| `U` | double | ano | Poissonův koeficient [-] |
| `alpha` | double | ne | Součinitel teplotní roztažnosti [1/°C] |
| `gama` | double | ne | Objemová tíha [N/m³] |

`G` (smykový modul) se počítá automaticky: `G = 0.5 * E / (1 + U)`.

---

#### 3.1.3 Section

Diskriminátor `type` určuje tvar průřezu. Všechny typy obsahují `label` a `materialLabel`.

**Generic (uživatelský průřez):**
```json
{
  "label": "sec1",
  "type": "Generic",
  "materialLabel": "C25",
  "A":   0.075,
  "Az":  0.0625,
  "Ay":  0.0625,
  "Iz":  0.000480,
  "Iy":  0.000480,
  "J":   0.000960,
  "maxHeight": 0.30,
  "maxWidth":  0.25
}
```

| Pole | Popis |
|---|---|
| `A` | Průřezová plocha [m²] |
| `Az` | Smyková plocha ve směru z [m²] |
| `Ay` | Smyková plocha ve směru y [m²] |
| `Iz` | Moment setrvačnosti k ose z [m⁴] |
| `Iy` | Moment setrvačnosti k ose y [m⁴] |
| `J` | Torzní konstanta [m⁴] |
| `maxHeight` | Maximální výška (pro plotting) |
| `maxWidth` | Maximální šířka (pro plotting) |

**Rectangular:**
```json
{
  "label": "rect300x500",
  "type": "Rectangular",
  "materialLabel": "C25",
  "b": 0.30,
  "t": 0.50
}
```

| Pole | Popis |
|---|---|
| `b` | Šířka [m] |
| `t` | Výška [m] |

**Circular:**
```json
{
  "label": "circ200",
  "type": "Circular",
  "materialLabel": "S355",
  "d": 0.20
}
```

| Pole | Popis |
|---|---|
| `d` | Průměr [m] |

**IPE (evropský válcovaný I-profil):**
```json
{
  "label": "IPE200",
  "type": "IPE",
  "materialLabel": "S355",
  "tf": 0.0085,
  "tw": 0.0056,
  "b":  0.100,
  "h":  0.1744,
  "r":  0.012
}
```

| Pole | Popis |
|---|---|
| `tf` | Tloušťka příruby [m] |
| `tw` | Tloušťka stojiny [m] |
| `b` | Šířka příruby [m] |
| `h` | Výška stojiny (bez přírub) [m] |
| `r` | Poloměr zaoblení [m] |

**HollowTube (dutý kruhový profil):**
```json
{
  "label": "CHS219x8",
  "type": "HollowTube",
  "materialLabel": "S355",
  "d": 0.219,
  "thickness": 0.008
}
```

---

#### 3.1.4 Element

Diskriminátor `type`: `"Frame"` nebo `"Spring"`.

**FrameElement2D:**
```json
{
  "label": "e1",
  "type": "Frame",
  "startNodeLabel": "n1",
  "endNodeLabel": "n3",
  "sectionLabel": "IPE200",
  "endRelease": "NoRelease"
}
```

| Pole | Typ | Povinné | Popis |
|---|---|---|---|
| `label` | string | ano | Unikátní ID |
| `type` | `"Frame"` | ano | Diskriminátor |
| `startNodeLabel` | string | ano | Odkaz na Node.label |
| `endNodeLabel` | string | ano | Odkaz na Node.label |
| `sectionLabel` | string | ano | Odkaz na Section.label |
| `endRelease` | enum | ne | Viz tabulka níže (default `NoRelease`) |

| `endRelease` | Popis |
|---|---|
| `NoRelease` | Tuhé spoje na obou koncích (výchozí) |
| `StartRelease` | Kloub na začátku (StartNode) |
| `EndRelease` | Kloub na konci (EndNode) |
| `FullRelease` | Kloub na obou koncích → příhradový prut |

**SpringElement2D:**
```json
{
  "label": "sp1",
  "type": "Spring",
  "startNodeLabel": "n1",
  "endNodeLabel": "n2",
  "K": 10000.0,
  "R": 0.0
}
```

| Pole | Popis |
|---|---|
| `K` | Axiální tuhost [síla/délka] |
| `R` | Rotační tuhost [síla·délka/rad] |

---

#### 3.1.5 LoadCase

```json
{
  "label": "LC1",
  "loadCaseType": "Live",
  "loadCaseDuration": "ShortTerm",
  "nodalLoads": [ ...NodalLoad[] ],
  "elementLoads": [ ...ElementLoad[] ],
  "supportDisplacements": [ ...SupportDisplacement[] ]
}
```

| `loadCaseType` | Popis |
|---|---|
| `SelfWeight` | Vlastní tíha |
| `Dead` | Stálé zatížení |
| `Live` | Proměnné zatížení |
| `Wind` | Vítr |
| `Seismic` | Zemětřesení |
| `Accidental` | Mimořádné |
| `Shrinkage` | Smršťování |

| `loadCaseDuration` | Popis |
|---|---|
| `Permanent` | > 10 let |
| `LongTerm` | 6 měsíců – 10 let |
| `MediumTerm` | 1 týden – 6 měsíců |
| `ShortTerm` | < 1 týden |
| `Instantaneous` | Okamžité |

**`direction`** pro všechna zatížení: `"Global"` (výchozí) nebo `"Local"` (lokální souřadnice prvku).

---

**NodalLoad:**
```json
{
  "nodeLabel": "n3",
  "fx": 80.0,
  "fy": 0.0,
  "mz": 0.0,
  "direction": "Global"
}
```

| Pole | Popis |
|---|---|
| `fx` | Síla v ose X [síla] |
| `fy` | Síla v ose Y [síla] |
| `mz` | Moment k ose Z [síla·délka] |

---

**ElementLoad — Point (FramePointLoad):**
```json
{
  "elementLabel": "e1",
  "type": "Point",
  "fx": 0.0,
  "fy": -50.0,
  "mz": 0.0,
  "l1": 4.5,
  "direction": "Global"
}
```

| Pole | Popis |
|---|---|
| `l1` | Vzdálenost od StartNode [m] |

---

**ElementLoad — Uniform (FrameUniformLoad):**
```json
{
  "elementLabel": "e2",
  "type": "Uniform",
  "wx": 0.0,
  "wy": -12.0,
  "l1": 0.0,
  "l2": 0.0,
  "direction": "Local"
}
```

| Pole | Popis |
|---|---|
| `wx` | Intenzita zatížení v X [síla/délka] |
| `wy` | Intenzita zatížení v Y [síla/délka] |
| `l1` | Vzdálenost začátku od StartNode (0 = od začátku) |
| `l2` | Vzdálenost konce od EndNode (0 = do konce) |

---

**ElementLoad — Trapezoidal (FrameTrapezoidalLoad):**
```json
{
  "elementLabel": "e3",
  "type": "Trapezoidal",
  "wx1": 0.0,
  "wx2": 0.0,
  "wy1": -5.0,
  "wy2": -15.0,
  "l1": 0.0,
  "l2": 0.0,
  "direction": "Local"
}
```

| Pole | Popis |
|---|---|
| `wx1/wy1` | Intenzita na začátku (u StartNode) |
| `wx2/wy2` | Intenzita na konci (u EndNode) |

---

**SupportDisplacement:**
```json
{
  "nodeLabel": "n2",
  "ux": 0.010,
  "uy": -0.005,
  "rz": 0.0
}
```

---

#### 3.1.6 LoadCombination

```json
{
  "label": "ULS1",
  "factors": {
    "LC1": 1.35,
    "LC2": 1.50
  }
}
```

Klíče `factors` jsou `LoadCase.label`, hodnoty jsou součinitele kombinace.

---

#### 3.1.7 Settings

```json
{
  "meshSegments": 20
}
```

| Pole | Typ | Default | Popis |
|---|---|---|---|
| `meshSegments` | int | 10 | Počet segmentů na prvek (větší = přesnější průběhy) |

---

### 3.2 Response Schema

```json
{
  "status": "Successful",
  "errors": [],
  "loadCaseResults": {
    "LC1": { ...CaseResult }
  },
  "loadCombinationResults": {
    "ULS1": { ...CaseResult }
  }
}
```

Pole `status`: `"Successful"` nebo `"Failure"`.
Pole `errors`: seznam chybových zpráv (validace vstupu, singulární matice, apod.).

---

**CaseResult:**
```json
{
  "nodeDisplacements": {
    "n1": { "ux": 0.0,     "uy": 0.0,      "rz": 0.0      },
    "n3": { "ux": 0.00123, "uy": -0.00456, "rz": 0.000089 }
  },
  "supportReactions": {
    "n1": { "fx": 25.3,  "fy": 30.1, "mz": -5.2 },
    "n2": { "fx": -25.3, "fy": 19.9, "mz":  5.2 }
  },
  "elementForces": {
    "e1": [
      {
        "x1": 0.000, "x2": 0.257,
        "startForce": { "fx": 0.0, "fy": -12.5, "mz": 0.0   },
        "endForce":   { "fx": 0.0, "fy": -12.5, "mz": -3.21 },
        "startDisplacement": { "ux": 0.0, "uy": 0.0,     "rz": 0.0      },
        "endDisplacement":   { "ux": 0.0, "uy": -0.0001, "rz": -0.00003 }
      },
      {
        "x1": 0.257, "x2": 0.514,
        "..."
      }
    ]
  }
}
```

**nodeDisplacements** — obsahuje všechny uzly (i neposunuté s nulovými hodnotami).  
**supportReactions** — pouze uzly, které mají `support != null`.  
**elementForces** — pole segmentů na každý prvek; počet segmentů = `settings.meshSegments`.

Každý segment:

| Pole | Popis |
|---|---|
| `x1`, `x2` | Začátek a konec segmentu v lokálních souřadnicích prvku [m] |
| `startForce.fx` | Normálová síla na začátku segmentu |
| `startForce.fy` | Posouvající síla na začátku segmentu |
| `startForce.mz` | Ohybový moment na začátku segmentu |
| `endForce.*` | Totéž na konci segmentu |
| `startDisplacement.*` | Posun/rotace na začátku segmentu |
| `endDisplacement.*` | Posun/rotace na konci segmentu |

---

### 3.3 Kompletní příklad request/response

**Request:**
```json
{
  "nodes": [
    { "label": "n1", "x": 0, "y": 0,
      "support": { "type": "Rigid", "ux": true, "uy": true, "rz": true } },
    { "label": "n2", "x": 9, "y": 0,
      "support": { "type": "Rigid", "ux": true, "uy": true, "rz": true } },
    { "label": "n3", "x": 0, "y": 6 }
  ],
  "materials": [
    { "label": "M1", "materialType": "Steel", "E": 30e6, "U": 0.2,
      "alpha": 1.2e-5, "gama": 39885 }
  ],
  "sections": [
    { "label": "S1", "type": "Generic", "materialLabel": "M1",
      "A": 0.075, "Az": 0.075, "Ay": 0.075,
      "Iz": 4.8e-4, "Iy": 4.8e-4, "J": 9.6e-4,
      "maxHeight": 0.1, "maxWidth": 0.1 }
  ],
  "elements": [
    { "label": "e1", "type": "Frame",
      "startNodeLabel": "n1", "endNodeLabel": "n3",
      "sectionLabel": "S1", "endRelease": "NoRelease" },
    { "label": "e2", "type": "Frame",
      "startNodeLabel": "n2", "endNodeLabel": "n3",
      "sectionLabel": "S1", "endRelease": "NoRelease" }
  ],
  "loadCases": [
    {
      "label": "LC1", "loadCaseType": "Live",
      "nodalLoads": [
        { "nodeLabel": "n3", "fx": 80, "fy": 0, "mz": 0, "direction": "Global" }
      ],
      "elementLoads": [
        { "elementLabel": "e1", "type": "Point",
          "fx": 0, "fy": -50, "mz": 0, "l1": 3.0, "direction": "Global" },
        { "elementLabel": "e2", "type": "Uniform",
          "wx": 0, "wy": -12, "l1": 0, "l2": 0, "direction": "Local" }
      ],
      "supportDisplacements": [
        { "nodeLabel": "n2", "ux": 0.01, "uy": -0.005, "rz": 0 }
      ]
    }
  ],
  "loadCombinations": [],
  "settings": { "meshSegments": 20 }
}
```

**Response (zkráceno):**
```json
{
  "status": "Successful",
  "errors": [],
  "loadCaseResults": {
    "LC1": {
      "nodeDisplacements": {
        "n1": { "ux": 0.0, "uy": 0.0, "rz": 0.0 },
        "n2": { "ux": 0.0, "uy": 0.0, "rz": 0.0 },
        "n3": { "ux": 0.00123, "uy": -0.00456, "rz": 0.000089 }
      },
      "supportReactions": {
        "n1": { "fx": 25.3, "fy": 30.1, "mz": -5.2 },
        "n2": { "fx": -25.3, "fy": 19.9, "mz": 5.2 }
      },
      "elementForces": {
        "e1": [
          { "x1": 0.0, "x2": 0.3,
            "startForce": { "fx": 0.0, "fy": -12.5, "mz": 0.0 },
            "endForce":   { "fx": 0.0, "fy": -12.5, "mz": -3.75 },
            "startDisplacement": { "ux": 0.0, "uy": 0.0, "rz": 0.0 },
            "endDisplacement":   { "ux": 0.0001, "uy": -0.0002, "rz": -0.00003 }
          }
        ]
      }
    }
  },
  "loadCombinationResults": {}
}
```

---

## 4. Stateful API (rozšíření)

Pro velké modely nebo interaktivní UI lze přidat step-by-step variantu. Každý zdroj má vlastní endpoint, stav je uložen server-side (Redis / in-memory cache s TTL).

```
POST   /api/v1/structures                          → { "id": "abc123" }

POST   /api/v1/structures/{id}/nodes               → přidá uzly
PUT    /api/v1/structures/{id}/nodes/{label}       → aktualizuje uzel
DELETE /api/v1/structures/{id}/nodes/{label}       → odebere uzel

POST   /api/v1/structures/{id}/materials
POST   /api/v1/structures/{id}/sections
POST   /api/v1/structures/{id}/elements
POST   /api/v1/structures/{id}/load-cases
POST   /api/v1/structures/{id}/load-combinations

POST   /api/v1/structures/{id}/solve               → spustí výpočet

GET    /api/v1/structures/{id}/results/displacements
GET    /api/v1/structures/{id}/results/reactions
GET    /api/v1/structures/{id}/results/elements/{elementLabel}/forces
GET    /api/v1/structures/{id}/results/elements/{elementLabel}/forces/at/{x}

DELETE /api/v1/structures/{id}                     → uvolní session
```

---

## 5. Chybové stavy (skutečné)

| HTTP kód | Situace | Tělo |
|---|---|---|
| `200 OK` | Výpočet proběhl | `AnalysisResponse` se `status: "Successful"` |
| `422 Unprocessable Entity` | Validace selhala / chyba při sestavení / singulární matice | `AnalysisResponse` se `status: "Failure"` a `errors[]` |
| `400 Bad Request` | JSON je syntakticky neparseable (např. chybí `type` discriminator) | ProblemDetails od ASP.NET Core |
| `500 Internal Server Error` | Neočekávaná výjimka knihovny | ProblemDetails |

Strukturovaná chybová odpověď (200/422):
```json
{
  "status": "Failure",
  "errors": [
    "Section 'S1': materialLabel 'M99' not found.",
    "Structure has no supports — at least one node must be restrained."
  ],
  "loadCaseResults": {},
  "loadCombinationResults": {}
}
```

> Klient `FealiteApiClient` mapuje 200 i 422 na typed `AnalysisResponse` — volající nemusí rozlišovat HTTP kódy, stačí číst `Status` a `Errors`. Ostatní non-2xx vyhodí `FealiteApiException`.

---

## 6. Validační pravidla

Tato pravidla musí API vrstvou ověřit ještě před zavoláním knihovny:

- Každý `label` musí být unikátní v rámci svého typu (nodes, materials, sections, elements, loadCases)
- `startNodeLabel` a `endNodeLabel` musí existovat v `nodes`
- `sectionLabel` musí existovat v `sections`
- `materialLabel` musí existovat v `materials`
- `nodeLabel` v loads musí existovat v `nodes`
- `elementLabel` v elementLoads musí existovat v `elements`
- `FramePointLoad.l1` musí být v intervalu `[0, délka prvku]`
- `FrameUniformLoad.l1 + l2` musí být `<= délka prvku`
- Konstrukce musí mít alespoň 1 node a 1 element
- Minimálně jeden uzel musí mít podporu (inak singulární matice)
- `loadCombination.factors` klíče musí existovat v `loadCases`
- Spring element: `K >= 0`, `R >= 0`
- Section: `A > 0`, `Iz > 0`
- Material: `E > 0`, `0 <= U < 0.5`

---

## 7. Skutečná implementace

### 7.1 `FEALiTE2D.Api.Contracts`

```
Enums/                      string-serialized enumy (LoadDirection, MaterialType, EndRelease, ...)
Requests/
  AnalysisRequest.cs        kořen
  NodeDto.cs
  SupportDto.cs             abstract + RigidSupportDto, SpringSupportDto    [JsonPolymorphic]
  MaterialDto.cs
  SectionDto.cs             abstract + Generic/Rectangular/Circular/IPE/HollowTube  [JsonPolymorphic]
  ElementDto.cs             abstract + FrameElementDto, SpringElementDto    [JsonPolymorphic]
  LoadCaseDto.cs + NodalLoadDto.cs + SupportDisplacementDto.cs
  ElementLoadDto.cs         abstract + Point/Uniform/Trapezoidal            [JsonPolymorphic]
  LoadCombinationDto.cs + SettingsDto.cs
Responses/
  AnalysisResponse.cs       Status + Errors[] + LoadCaseResults + LoadCombinationResults
  CaseResultDto.cs          NodeDisplacements, SupportReactions, ElementForces (segments)
  DisplacementDto.cs + ForceDto.cs + SegmentDto.cs
Mapping/
  EnumMapping.cs            DTO ↔ doménový enum
  StructureBuilder.cs       AnalysisRequest → FEALiTE2D.Structure
  ResultMapper.cs           PostProcessor → CaseResultDto
  AnalysisService.cs        validate → build → solve → map (orchestrátor)
Validation/
  ValidationResult.cs
  AnalysisRequestValidator.cs   pravidla z §6
ApiJsonOptions.cs           sdílené JsonSerializerOptions
```

### 7.2 `FEALiTE2D.Api` (ASP.NET Core minimal API)

```csharp
// Program.cs (zkráceno)
builder.Services.AddSingleton<AnalysisService>();
builder.Services.Configure<JsonOptions>(o => /* sjednoceno s ApiJsonOptions.Default */);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/v1/analyze", (AnalysisRequest request, AnalysisService service) =>
{
    var response = service.Analyze(request);
    return response.Status == AnalysisStatusDto.Successful
        ? Results.Ok(response)
        : Results.UnprocessableEntity(response);
});
```

### 7.3 `FEALiTE2D.Client` (typed HttpClient)

```csharp
public sealed class FealiteApiClient
{
    public FealiteApiClient(HttpClient http) { ... }
    public Task<AnalysisResponse> AnalyzeAsync(AnalysisRequest request, CancellationToken ct = default);
    public Task<bool> HealthAsync(CancellationToken ct = default);
}

// Registrace v ASP.NET Core / Blazor:
services.AddFealiteApiClient(configuration);              // čte sekci "FealiteApi"
// nebo
services.AddFealiteApiClient(o => o.BaseAddress = new Uri("http://localhost:5180/"));
```

Klient sdílí stejný projekt `FEALiTE2D.Api.Contracts` se serverem — DTOs, polymorfismus a JSON options jsou identické na obou stranách. Klient lze použít z libovolné aplikace s `IHttpClientFactory` (ASP.NET Core, Blazor WebAssembly, console).

---

## 8. Shrnutí vstupů a výstupů

### Vstupy

| Objekt | Povinné pole | Klíčové vazby |
|---|---|---|
| Node | label, x, y | support (volitelné) |
| Material | label, materialType, E, U | — |
| Section | label, type, materialLabel | → Material |
| FrameElement | label, startNodeLabel, endNodeLabel, sectionLabel | → Node × 2, Section |
| SpringElement | label, startNodeLabel, endNodeLabel, K, R | → Node × 2 |
| LoadCase | label, loadCaseType | → Node (nodalLoads), Element (elementLoads) |
| LoadCombination | label, factors{lcLabel: factor} | → LoadCase |

### Výstupy (na každý LoadCase a LoadCombination)

| Výsledek | Granularita | Zdroj v knihovně |
|---|---|---|
| Uzlový posun (ux, uy, rz) | na uzel | `PostProcessor.GetNodeGlobalDisplacement` |
| Reakce podpory (fx, fy, mz) | na podepřený uzel | `PostProcessor.GetSupportReaction` |
| Průběh vnitřních sil (N, V, M) | na segment prvku | `PostProcessor.GetElementInternalForces` |
| Vnitřní síly v bodě x | libovolný bod na prvku | `PostProcessor.GetElementInternalForcesAt` |
| Posuny na prvku | na segment | `PostProcessor.GetElementDisplacementAt` |
