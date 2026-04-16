# FEALiTE2D — UI Design: Integrace do DCE

Návrh uživatelského rozhraní pro modul 2D prutové analýzy integrovaný do aplikace CSSCPrototype (DCE).

---

## 1. Kontext a propojení modulů

DCE dnes vyžaduje, aby uživatel zadal vnitřní síly (**N_Ed, M_Ed, V_Ed**) ručně do load cases.
FEALiTE2D tyto síly **vypočítá** z prutového modelu.

```
[Prutový model]  →  FEALiTE2D  →  [N, M, V na prvcích]
                                         │
                              přenos do DCE Load Cases
                                         │
                              [Průřezový posudek] → výsledky
```

FEA modul tedy není izolovaná kalkulačka — je to **vstup do existující DCE pipeline**.

---

## 2. Kde modul žije v DCE

### Vstupní bod: TopBar

Přidat nové tlačítko do TopBar za existující sekci Loads:

```
[File] [Section] [Reinf] [Code] [Materials] | [Loads ▸] | [2D Frame ▸] | [Undo][Redo]...
```

Klik na **"2D Frame"** otevře `FrameAnalysisDialog` — wide modal přes celou obrazovku
(analogie: `SectionEditorDialog`).

### Alternativa pro výsledky: nová kapitola v A4

Výsledky analýzy (průběhy M, V, N) se zobrazí jako nová kapitola v A4 výstupu
(analogie: existující `CheckChapter`).

---

## 3. Struktura modalu `FrameAnalysisDialog`

```
┌─────────────────────────────────────────────────────────────────────┐
│  2D Frame Analysis                                         [×] Zavřít│
├──────────┬──────────────────────────────────────────────────────────┤
│ PANEL    │  CANVAS (SVG)                                            │
│ (280px)  │                                                          │
│          │         n3 ●                                             │
│ Nodes    │        /    \                                            │
│ ──────── │      e1      e2                                          │
│ + Přidat │      /   ⟱    \                                          │
│          │    n1 △       n2 △                                       │
│ Elements │                                                          │
│ ──────── │  [Zoom +] [Zoom -] [Fit] [Select] [Pan]                 │
│ + Přidat ├──────────────────────────────────────────────────────────┤
│          │  [Model] [Loads] [Results]                               │
│ Load     │                                                          │
│ Cases    │  (spodní tab panel — kontext dle aktivního tabu)         │
│ ──────── │                                                          │
│ + Přidat ├──────────────────────────────────────────────────────────┤
│          │  [Analyze]  ● LC1: Live   ▼         [Export to DCE]      │
└──────────┴──────────────────────────────────────────────────────────┘
```

### 3.1 Levý panel — navigace a seznam objektů

Tři sklápěcí sekce (analogie filter chips v LoadGroupEditorDialog):

**Nodes**
- seznam: `n1 (0, 0) [△]`, `n2 (9, 0) [△]`, `n3 (0, 6)`
- klik na řádek = select uzel na canvasu + otevře Node Editor v dolním panelu
- `[+ Add Node]` tlačítko

**Elements**
- seznam: `e1  n1→n3  IPE200`, `e2  n2→n3  IPE200`
- klik = select element
- `[+ Add Element]`

**Load Cases**
- seznam: `LC1 Live`, `LC2 Dead`
- výběr aktivního LC pro zobrazení zatížení a výsledků
- `[+ Add Case]`

### 3.2 Canvas (SVG — střed)

Interaktivní SVG vygenerovaný C# (rozšíření DCESVG nebo nová `FrameSvgRenderer` třída):

| Prvek | Zobrazení |
|---|---|
| Uzel (volný) | ● (filled circle, accent barva) |
| Uzel (vybraný) | ● s outline ring |
| Tuhé vetknutí | △ filled (standardní FEA notace) |
| Kloubová podpora | △ outline |
| Pružinová podpora | △ + spirála |
| Prvek (Frame) | úsečka |
| Prvek (vybraný) | silnější úsečka, accent barva |
| Bodové zatížení | šipka s hodnotou |
| Spojité zatížení | pole šipek s obdélníkem |
| Deformovaný tvar | gestrichelte čára (po výpočtu) |
| Diagram M | vyplněná oblast pod/nad prvkem (červená/modrá) |
| Diagram V | totéž |
| Diagram N | totéž |

**Toolbar canvasu**: Select / Pan / Add Node / Add Element (tool mode buttons)

### 3.3 Spodní tab panel

**Tab: Model** (výchozí)
- Zobrazí editor vybraného objektu (viz sekce 4)
- Pokud nic není vybráno: prázdné / hint text

**Tab: Loads**
- Přidávání a editace zatížení na vybraném prvku/uzlu
- Výběr load case

**Tab: Results**
- Přepínač: `Deformed Shape | M diagram | V diagram | N diagram`
- Výběr Load Case / Load Combination ze selectboxu
- Tabulka extrémních hodnot (min/max M, V, N po prvcích)

### 3.4 Dolní toolbar

```
[▶ Analyze]   Load Case: [LC1 - Live ▼]   [Export to DCE ▶]
```

- **Analyze**: spustí `FEALiTE2D.Structure.Solve()`, zobrazí spinner
- **Export to DCE**: přenese N, M, V z vybraného LC do DCE Load Cases

---

## 4. Editory objektů (v dolním tab panelu)

Přesně kopírují DCE vzor: `.editor-form-group` + `.form-input` + `.editor-unit`.

### 4.1 Node Editor

```
Label:        [n1          ]
X:            [0.0         ] m
Y:            [0.0         ] m
Rotation:     [0.0         ] °

Support:      ○ None  ● Rigid  ○ Spring

  Rigid:    [✓] Ux   [✓] Uy   [✓] Rz

  (nebo Spring:)
  Kx:       [5000.0      ] kN/m
  Ky:       [8000.0      ] kN/m
  Cz:       [0.0         ] kNm/rad
```

### 4.2 Element Editor

```
Label:        [e1          ]
Start Node:   [n1   ▼]
End Node:     [n3   ▼]

Section:      [IPE200  ▼]   [✎ Edit]
  → otevře mini-selector ze sekce katalogů

End Release:  ● No Release
              ○ Start Release (kloub na začátku)
              ○ End Release (kloub na konci)
              ○ Full Release (příhradový prut)

Length:       6.00 m  (read-only, vypočteno)
```

### 4.3 Material Editor (mini — pro FEA sekce)

```
Label:        [S355        ]
Type:         [Steel  ▼]
E:            [210 000 000 ] Pa
ν (Poisson):  [0.30        ]
α (thermal):  [0.000012    ] 1/°C
γ (weight):   [78 500      ] N/m³
```

### 4.4 Section Selector (dropdown + mini katalog)

Sekce vybírané přes dropdown analogický `SectionEditorDialog`:

```
Section: [IPE200  ▼]
Type:    Generic / Rectangular / Circular / IPE / HollowTube
         ──────────────────────────────────────────────────
         [ Generic   ]  A, Az, Ay, Iz, Iy, J (numericka pole)
         [ Rectangle ]  b × h
         [ Circular  ]  d
         [ IPE       ]  tf, tw, b, h, r
         [ HollowTube]  d, thickness
```

### 4.5 Load Editor (Tab Loads, pro vybraný prvek)

```
Load Case: [LC1 - Live ▼]                [+ Add Load]

┌────────────────────────────────────────────────────┐
│ #  Type       Direction  Value        Position     │
│ 1  Uniform    Local      wy = -12 kN/m  full span  │
│ 2  Point      Global     Fy = -50 kN    x = 3.0 m  │
└────────────────────────────────────────────────────┘

[vybraný řádek rozevře inline form níže:]

Type:      ● Point  ○ Uniform  ○ Trapezoidal
Direction: ● Global  ○ Local
Fx:        [0.0     ] kN
Fy:        [-50.0   ] kN
Mz:        [0.0     ] kNm
L1 (dist): [3.0     ] m
                         [✓ Save]  [🗑 Delete]
```

---

## 5. Results view

### 5.1 Diagram na canvasu

Po `Analyze` se na canvasu zobrazí diagram dle aktivního přepínače:

```
[Deformed] [M] [V] [N]
```

Hodnoty na diagramu: max/min labels přímo na SVG.
Měřítko diagramu: automatické + možnost manuálního zadání.

### 5.2 Výsledková tabulka (Tab: Results)

```
Load Case: [LC1 - Live ▼]

Nodal Displacements:
┌──────┬───────────┬───────────┬───────────┐
│ Node │  Ux [mm]  │  Uy [mm]  │  Rz [mrad]│
├──────┼───────────┼───────────┼───────────┤
│  n1  │   0.000   │   0.000   │   0.000   │
│  n2  │   0.000   │   0.000   │   0.000   │
│  n3  │   1.234   │  -4.567   │   0.890   │
└──────┴───────────┴───────────┴───────────┘

Support Reactions:
┌──────┬───────────┬───────────┬───────────┐
│ Node │  Fx [kN]  │  Fy [kN]  │  Mz [kNm] │
├──────┼───────────┼───────────┼───────────┤
│  n1  │  25.30    │  30.10    │  -5.20    │
│  n2  │ -25.30    │  19.90    │   5.20    │
└──────┴───────────┴───────────┴───────────┘

Element Extremes:
┌──────┬───────────┬───────────┬───────────┐
│ Elem │ Mmax [kNm]│ Vmax [kN] │ Nmax [kN] │
├──────┼───────────┼───────────┼───────────┤
│  e1  │  ★ 62.5   │   37.2    │   18.4    │
│  e2  │    48.1   │   28.6    │   22.1    │
└──────┴───────────┴───────────┴───────────┘
```

Styl: `.forces-table` (existující DCE třída), ★ pro extrém.

---

## 6. Export do DCE — `[Export to DCE]`

Klik otevře malý dialog:

```
┌─────────────────────────────────────┐
│  Export Internal Forces to DCE      │
│                                     │
│  Source:   LC1 - Live          ▼   │
│  Element:  e1  (n1→n3)         ▼   │
│  Location: ○ Start  ● x = 3.0 m    │
│            ○ End                    │
│                                     │
│  Extracted:                         │
│    N = -18.4 kN                     │
│    M =  62.5 kNm                    │
│    V =  37.2 kN                     │
│                                     │
│  Target DCE Load Case:              │
│    [LC1 - Live               ▼]    │
│    ● Replace existing               │
│    ○ Add as new combination         │
│                                     │
│       [Cancel]   [Export ▶]         │
└─────────────────────────────────────┘
```

Po exportu se hodnoty N, M, V automaticky propíší do DCE `LoadCaseDto`
a spustí se přepočet průřezového posudku.

---

## 7. State management (DCE vzor)

Nová scoped service `FrameModelStateService` (analogie `ProjectStateService`):

```csharp
public class FrameModelStateService
{
    public FrameModelData Data { get; private set; }     // model
    public FrameAnalysisResults? Results { get; private set; }  // null = not solved
    public bool IsSolved => Results != null;

    public void SetNode(Node2DDto node)    // → invalidate Results
    public void SetElement(ElementDto el) // → invalidate Results
    public void AddLoad(...)              // → invalidate Results
    public void SetResults(FrameAnalysisResults r) { Results = r; NotifyChanged(); }
    public event Action? OnChanged;
}
```

**Undo/Redo**: stejný `UndoRedoService` jako DCE — `FrameModelData` musí být serializovatelný.

**Persistence**: `FrameModelData` se uloží do `ProjectData` (nový field) společně s ostatními daty projektu.

---

## 8. Nová A4 kapitola: Frame Analysis

Po provedené analýze přibyde v A4 výstupu nová kapitola (analogie `CheckChapter`):

```
4. Frame Analysis
   4.1  Structural Model
        [SVG s prutovým modelem, rozměry, podpory]

   4.2  Load Cases
        Tabulka zatížení (typ, hodnota, poloha)

   4.3  Internal Forces — LC1: Live
        [SVG s M diagramem]
        [SVG s V diagramem]
        Tabulka extremů

   4.4  Nodal Displacements
        Tabulka posunů
```

Viditelnost sekcí řídí `ChapterVisibility` parametr (existující DCE mechanismus).

---

## 9. Nové soubory a projekty

```
DCE.UI/
├── Components/
│   └── Frame/                          ← nová složka
│       ├── FrameAnalysisDialog.razor   ← hlavní modal
│       ├── FrameCanvas.razor           ← SVG canvas + interakce
│       ├── FrameObjectPanel.razor      ← levý panel (nodes/elements/lc)
│       ├── NodeEditor.razor            ← form pro uzel
│       ├── ElementEditor.razor         ← form pro prvek
│       ├── LoadEditor.razor            ← form pro zatížení
│       ├── FrameResultsTab.razor       ← tabulky výsledků
│       └── ExportToDceDialog.razor     ← export modal
│
├── Services/
│   └── FrameModelStateService.cs       ← state management
│
├── Models/
│   └── Frame/
│       ├── FrameModelData.cs           ← serializovatelný model
│       ├── Node2DDto.cs
│       ├── ElementDto.cs
│       ├── SectionDto.cs
│       ├── LoadCaseDto.cs              ← (pozor: kolize s existujícím DCE LoadCaseDto)
│       └── FrameAnalysisResults.cs

DCE.Shared/
└── Components/
    └── Presentation/
        └── FrameAnalysisChapter.razor  ← A4 kapitola

FEALiTE2D.Api/                          ← nový projekt (ASP.NET Core Web API)
├── Controllers/AnalyzeController.cs
├── Models/Request/...
├── Models/Response/...
└── Services/StructureBuilder.cs
```

> **Pozor na namespace kolizi**: DCE.Shared již obsahuje `LoadCaseDto`.
> FEA DTO pojmenovat `FrameLoadCaseDto` nebo dát do namespace `FEALiTE2D.Models`.

---

## 10. Doporučené pořadí implementace

```
Fáze 1 — Backend (API)
  ├── FEALiTE2D.Api projekt (ASP.NET Core)
  ├── StructureBuilder: JSON → FEALiTE2D objekty → Solve → JSON
  ├── Validace vstupu (FluentValidation)
  └── POST /api/v1/analyze

Fáze 2 — State & DTOs
  ├── FrameModelData + všechny DTO třídy
  ├── FrameModelStateService
  └── FrameAnalysisService (volá API endpoint)

Fáze 3 — Canvas
  ├── FrameSvgRenderer (nebo rozšíření DCESVG)
  ├── FrameCanvas.razor (SVG interakce: select, pan, zoom)
  └── Základní zobrazení: uzly, prvky, podpory

Fáze 4 — Editory
  ├── NodeEditor, ElementEditor, SectionEditor
  ├── LoadEditor
  └── FrameObjectPanel (levý panel)

Fáze 5 — Výsledky
  ├── Diagram rendering (M, V, N) na canvasu
  ├── FrameResultsTab (tabulky)
  └── FrameAnalysisChapter (A4 kapitola)

Fáze 6 — Export
  └── ExportToDceDialog + propojení s ProjectStateService
```
