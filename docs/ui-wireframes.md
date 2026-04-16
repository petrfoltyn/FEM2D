# FEA Beam — UI Wireframes

Návrh dialogů v DCE design systému. Všechny třídy odpovídají skutečným třídám z CSSCPrototype.

---

## 0. TopBar — vstupní bod

Přidat "Beam" jako nový modul do `.topbar-modules` (existující pattern v `TopBar.razor`):

```
┌─────────────────────────────────────────────────────────────────────────────────────────────┐
│ .topbar  (height: 52px, bg: --surface, border-bottom: 1px --border)                        │
│                                                                                              │
│ [≡][💾][📂][💾as] │ [↩][↪] │ [🖨][◻] │ [⚙Loads][📐Sections][…]  ·····  ╔═══════════════╗ │
│                                                                              ║ .topbar-modules║ │
│                                                                              ║ bg: --bg       ║ │
│                                                                              ║ border-radius:8║ │
│                                                                              ║ padding: 2px   ║ │
│                                                                              ╠═══════════╦═══╣ │
│                                                                              ║Cross Sect ║Beam║ │
│                                                                              ║.module-btn║.mod║ │
│                                                                              ║           ║.act║ │
│                                                                              ╚═══════════╩═══╝ │
└─────────────────────────────────────────────────────────────────────────────────────────────┘
```

`.module-btn.active` → background: --surface, color: --accent, box-shadow: 0 1px 3px rgba(0,0,0,.1)

Klik na **Beam** → otevře `BeamAnalysisDialog`.

---

## 1. BeamAnalysisDialog — Tab: Geometrie

Používá `CatalogDialog` jako wrapper. **1200 × 760 px**.

```
╔══════════════════════════════════════════════════════════════════════════════════════════════╗
║  .catalog-overlay  (position:fixed; inset:0; bg:rgba(44,42,38,.4); backdrop-filter:blur(4px))║
║                                                                                              ║
║  ╔════════════════════════════════════════════════════════════════════════════════════════╗  ║
║  ║  .catalog-dialog  (width:1200px; height:760px; bg:white; border-radius:16px)          ║  ║
║  ║                                                                                        ║  ║
║  ║  ┌──────────────────────────────────────────────────────────────────────────────────┐  ║  ║
║  ║  │  .catalog-header  (padding:14px 20px; border-bottom:1px --border)                │  ║  ║
║  ║  │                                                                                  │  ║  ║
║  ║  │   ≋  Beam Analysis                               .catalog-title (fw:700, 15px)  │  ║  ║
║  ║  │                                                                          [×]     │  ║  ║
║  ║  └──────────────────────────────────────────────────────────────────────────────────┘  ║  ║
║  ║                                                                                        ║  ║
║  ║  ┌──────────────────────────────────────────────────────────────────────────────────┐  ║  ║
║  ║  │  .se-filter-bar  (padding:9px 20px; bg:#FAFAF8; border-bottom:1px #EBE8E4)       │  ║  ║
║  ║  │                                                                                  │  ║  ║
║  ║  │   .se-filter-label  MODUL:                                                       │  ║  ║
║  ║  │   .se-chips                                                                      │  ║  ║
║  ║  │   ┌─────────────────────┐  ┌───────┐  ┌──────────┐                              │  ║  ║
║  ║  │   │ ●  Geometrie        │  │ Loads │  │ Results  │                              │  ║  ║
║  ║  │   │ .se-chip.active     │  │.se-chip│  │ .se-chip │                              │  ║  ║
║  ║  │   │ bg:#C45D3E; c:white │  │       │  │          │                              │  ║  ║
║  ║  │   └─────────────────────┘  └───────┘  └──────────┘                              │  ║  ║
║  ║  └──────────────────────────────────────────────────────────────────────────────────┘  ║  ║
║  ║                                                                                        ║  ║
║  ║  ┌────────────────────────────────────────────────────────────────────────────────────┐ ║  ║
║  ║  │  .catalog-body  (display:flex; flex:1; overflow:hidden)                           │ ║  ║
║  ║  │                                                                                   │ ║  ║
║  ║  │ ┌──────────────────┐┌───────────────────────────────────┐┌─────────────────────┐ │ ║  ║
║  ║  │ │.catalog-panel    ││  .catalog-panel.catalog-center    ││.catalog-panel        │ │ ║  ║
║  ║  │ │.catalog-left     ││  (flex:1; overflow:hidden)        ││.catalog-right        │ │ ║  ║
║  ║  │ │(width:200px)     ││                                   ││(width:320px)         │ │ ║  ║
║  ║  │ │                  ││ ┌───────────────────────────────┐ ││                      │ │ ║  ║
║  ║  │ │ .tree-category   ││ │ .preview-header               │ ││ ┌──────────────────┐ │ │ ║  ║
║  ║  │ │  ŠABLONY         ││ │ (padding:8px 14px)            │ ││ │ .lg-fieldset     │ │ │ ║  ║
║  ║  │ │                  ││ │ Schéma  [Select][Pan][+Node]  │ ││ │                  │ │ │ ║  ║
║  ║  │ │ .tree-item       ││ └───────────────────────────────┘ ││ │  Délka nosníku   │ │ │ ║  ║
║  ║  │ │  Prostý nosník   ││                                   ││ │  .lg-form-row    │ │ │ ║  ║
║  ║  │ │  ─▽─────────▽─   ││ ┌───────────────────────────────┐ ││ │ ┌────────────┐  │ │ │ ║  ║
║  ║  │ │                  ││ │ .preview-canvas               │ ││ │ │ L = [8.00] │m │ │ │ ║  ║
║  ║  │ │ .tree-item       ││ │ (bg:#f7f5f2; flex:1)          │ ││ │ └────────────┘  │ │ │ ║  ║
║  ║  │ │ .selected        ││ │                               │ ││ └──────────────────┘ │ │ ║  ║
║  ║  │ │  bg:accent-light ││ │                               │ ││                      │ │ ║  ║
║  ║  │ │  border-l:accent ││ │        n1            n2       │ ││ ┌──────────────────┐ │ │ ║  ║
║  ║  │ │  Vetknutý        ││ │        ●─────────────●        │ ││ │ .lg-fieldset     │ │ │ ║  ║
║  ║  │ │  ▐▬──────────▽─  ││ │        │             │        │ ││ │                  │ │ │ ║  ║
║  ║  │ │                  ││ │        e1             │        │ ││ │  Levá podpora    │ │ │ ║  ║
║  ║  │ │ .tree-item       ││ │       /               │        │ ││ │  .lg-form-select │ │ │ ║  ║
║  ║  │ │  Ob. vetknutý    ││ │  ▽───────────────────▽        │ ││ │ ┌──────────────┐ │ │ │ ║  ║
║  ║  │ │  ▐▬──────────▬▌  ││ │                               │ ││ │ │ Kloub      ▼ │ │ │ │ ║  ║
║  ║  │ │                  ││ │       ←── 8.00 m ──→          │ ││ │ └──────────────┘ │ │ │ ║  ║
║  ║  │ │ .tree-category   ││ │                               │ ││ └──────────────────┘ │ │ ║  ║
║  ║  │ │  SPOJITÉ         ││ │                               │ ││                      │ │ ║  ║
║  ║  │ │                  ││ └───────────────────────────────┘ ││ ┌──────────────────┐ │ │ ║  ║
║  ║  │ │ .tree-item       ││                                   ││ │ .lg-fieldset     │ │ │ ║  ║
║  ║  │ │  Dvoupole        ││                                   ││ │                  │ │ │ ║  ║
║  ║  │ │  ─▽──▽──▽─       ││                                   ││ │  Pravá podpora   │ │ │ ║  ║
║  ║  │ │                  ││                                   ││ │ ┌──────────────┐ │ │ │ ║  ║
║  ║  │ │ .tree-item       ││                                   ││ │ │ Kloub      ▼ │ │ │ │ ║  ║
║  ║  │ │  Třípole         ││                                   ││ │ └──────────────┘ │ │ │ ║  ║
║  ║  │ │  ─▽─▽─▽─▽─       ││                                   ││ └──────────────────┘ │ │ ║  ║
║  ║  │ │                  ││                                   ││                      │ │ ║  ║
║  ║  │ │                  ││                                   ││ ┌──────────────────┐ │ │ ║  ║
║  ║  │ │                  ││                                   ││ │ .lg-fieldset     │ │ │ ║  ║
║  ║  │ │                  ││                                   ││ │                  │ │ │ ║  ║
║  ║  │ │                  ││                                   ││ │  Průřezy         │ │ │ ║  ║
║  ║  │ │                  ││                                   ││ │  .se-tb-btn      │ │ │ ║  ║
║  ║  │ │                  ││                                   ││ │  [+ Přidat]      │ │ │ ║  ║
║  ║  │ │                  ││                                   ││ │                  │ │ │ ║  ║
║  ║  │ │                  ││                                   ││ │ ┌──────────────┐ │ │ │ ║  ║
║  ║  │ │                  ││                                   ││ │ │Od │Do │Průřez│ │ │ │ ║  ║
║  ║  │ │                  ││                                   ││ │ ├───┼───┼──────┤ │ │ │ ║  ║
║  ║  │ │                  ││                                   ││ │ │0.0│8.0│IPE300│ │ │ │ ║  ║
║  ║  │ │                  ││                                   ││ │ └──────────────┘ │ │ │ ║  ║
║  ║  │ └──────────────────┘└───────────────────────────────────┘└─────────────────────┘ │ ║  ║
║  ║  └────────────────────────────────────────────────────────────────────────────────────┘ ║  ║
║  ║                                                                                        ║  ║
║  ║  ┌──────────────────────────────────────────────────────────────────────────────────┐  ║  ║
║  ║  │  .catalog-footer  (padding:12px 20px; bg:#FAFAF8; border-top:1px #EBE8E4)        │  ║  ║
║  ║  │                                                                                  │  ║  ║
║  ║  │  [Zrušit]  .se-btn-secondary                  [Pokračovat →]  .se-btn-primary   │  ║  ║
║  ║  └──────────────────────────────────────────────────────────────────────────────────┘  ║  ║
║  ╚════════════════════════════════════════════════════════════════════════════════════════╝  ║
╚══════════════════════════════════════════════════════════════════════════════════════════════╝
```

---

## 2. BeamAnalysisDialog — Tab: Zatížení

Stejný wrapper, mění se obsah všech tří panelů.

```
║  ║  ┌──────────────────────────────────────────────────────────────────────────────────┐  ║
║  ║  │  .se-filter-bar                                                                  │  ║
║  ║  │   ┌─────────────┐  ┌─────────────────┐  ┌──────────┐                            │  ║
║  ║  │   │  Geometrie  │  │ ●  Zatížení     │  │ Results  │                            │  ║
║  ║  │   │  .se-chip   │  │ .se-chip.active │  │ .se-chip │                            │  ║
║  ║  │   └─────────────┘  └─────────────────┘  └──────────┘                            │  ║
║  ║  └──────────────────────────────────────────────────────────────────────────────────┘  ║
║  ║  ┌────────────────────────────────────────────────────────────────────────────────────┐ ║
║  ║  │  .catalog-body                                                                    │ ║
║  ║  │ ┌──────────────────┐┌───────────────────────────────────┐┌─────────────────────┐ │ ║
║  ║  │ │  .catalog-left   ││  .catalog-center                  ││  .catalog-right      │ │ ║
║  ║  │ │  (width:200px)   ││                                   ││  (width:320px)       │ │ ║
║  ║  │ │                  ││ ┌───────────────────────────────┐ ││                      │ │ ║
║  ║  │ │ ┌──────────────┐ ││ │ .preview-header               │ ││ (prázdné dokud není  │ │ ║
║  ║  │ │ │ .se-toolbar  │ ││ │  Zobrazit: [M][V][N][Def.]    │ ││  vybrán load)        │ │ ║
║  ║  │ │ │ [+] [✎] [🗑] │ ││ └───────────────────────────────┘ ││                      │ │ ║
║  ║  │ │ └──────────────┘ ││                                   ││ ┌──────────────────┐ │ │ ║
║  ║  │ │                  ││ ┌───────────────────────────────┐ ││ │ .lg-fieldset     │ │ │ ║
║  ║  │ │  .tree-category  ││ │ .preview-canvas               │ ││ │                  │ │ │ ║
║  ║  │ │   LOAD CASES     ││ │                               │ ││ │  [vybraný load]  │ │ │ ║
║  ║  │ │                  ││ │    ↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓        │ ││ │                  │ │ │ ║
║  ║  │ │ .lg-item         ││ │    ─────────────────────      │ ││ │  Typ             │ │ │ ║
║  ║  │ │ .selected        ││ │    │                  │        │ ││ │  ● Distributed   │ │ │ ║
║  ║  │ │  ● LC1 Dead      ││ │                               │ ││ │  ○ Point load    │ │ │ ║
║  ║  │ │                  ││ │           ↓ P=-50kN           │ ││ │  ○ Moment        │ │ │ ║
║  ║  │ │ .lg-item         ││ │    ─▽────────────────▽──      │ ││ │                  │ │ │ ║
║  ║  │ │  ○ LC2 Live      ││ │                               │ ││ │  Směr            │ │ │ ║
║  ║  │ │                  ││ │    ←───── 8.00 m ─────→       │ ││ │  ● Global        │ │ │ ║
║  ║  │ │ .lg-item         ││ │                               │ ││ │  ○ Local         │ │ │ ║
║  ║  │ │  ○ LC3 Wind      ││ └───────────────────────────────┘ ││ │                  │ │ │ ║
║  ║  │ │                  ││                                   ││ │  .lg-form-row    │ │ │ ║
║  ║  │ │                  ││                                   ││ │  wy [  -12.0  ] kN/m│ │ ║
║  ║  │ │                  ││                                   ││ │  wx [   0.0   ] kN/m│ │ ║
║  ║  │ │                  ││                                   ││ │                  │ │ │ ║
║  ║  │ │                  ││                                   ││ │  Od x [ 0.0  ] m │ │ │ ║
║  ║  │ │                  ││                                   ││ │  Do x [ 8.0  ] m │ │ │ ║
║  ║  │ │                  ││                                   ││ │                  │ │ │ ║
║  ║  │ │                  ││                                   ││ │        [✓ Uložit]│ │ │ ║
║  ║  │ └──────────────────┘└───────────────────────────────────┘└─────────────────────┘ │ ║
║  ║  └────────────────────────────────────────────────────────────────────────────────────┘ ║
║  ║  ┌──────────────────────────────────────────────────────────────────────────────────┐  ║
║  ║  │  .catalog-footer                                                                 │  ║
║  ║  │  [← Zpět]  .se-btn-secondary      [▶ Spustit výpočet]  .se-btn-primary         │  ║
║  ║  └──────────────────────────────────────────────────────────────────────────────────┘  ║
```

**Detail: load item v levém panelu**

`.lg-item` s badge typem a hodnotou:

```
┌──────────────────────────┐
│ .lg-item  (padding:9px)  │
│                          │
│  ⬇  Distributed          │
│     wy = -12.0 kN/m      │
│  .lg-badge  DEAD         │
└──────────────────────────┘

┌──────────────────────────┐
│ .lg-item.selected        │
│ border-left:3px #C45D3E  │
│ bg: rgba(196,93,62,.08)  │
│                          │
│  ↓  Point                │
│     Fy = -50.0 kN        │
│     x = 4.0 m            │
│  .lg-badge  LIVE         │
└──────────────────────────┘
```

---

## 3. BeamAnalysisDialog — Tab: Výsledky

```
║  ║  ┌──────────────────────────────────────────────────────────────────────────────────┐  ║
║  ║  │  .se-filter-bar                                                                  │  ║
║  ║  │   ┌─────────────┐  ┌───────────┐  ┌───────────────────┐                         │  ║
║  ║  │   │  Geometrie  │  │ Zatížení  │  │ ●  Výsledky       │                         │  ║
║  ║  │   └─────────────┘  └───────────┘  └───────────────────┘                         │  ║
║  ║  │                                                                                  │  ║
║  ║  │   LC/Komb:  [LC1 - Dead  ▼]     Diagram: [M][V][N][Def.]  .se-chip pattern      │  ║
║  ║  └──────────────────────────────────────────────────────────────────────────────────┘  ║
║  ║  ┌────────────────────────────────────────────────────────────────────────────────────┐ ║
║  ║  │  .catalog-body                                                                    │ ║
║  ║  │ ┌──────────────────┐┌───────────────────────────────────┐┌─────────────────────┐ │ ║
║  ║  │ │  .catalog-left   ││  .catalog-center                  ││  .catalog-right      │ │ ║
║  ║  │ │                  ││                                   ││                      │ │ ║
║  ║  │ │  .tree-category  ││ ┌───────────────────────────────┐ ││ .lg-fieldset         │ │ ║
║  ║  │ │   UZLY           ││ │ .preview-canvas               │ ││                      │ │ ║
║  ║  │ │                  ││ │                               │ ││  Reakce podpor       │ │ ║
║  ║  │ │ .tree-item       ││ │        62.5 kNm ★             │ ││ ┌──────────────────┐ │ │ ║
║  ║  │ │  n1: Rx=25.3 kN  ││ │     ╭──────────╮              │ ││ │ .forces-table    │ │ │ ║
║  ║  │ │ .tree-item       ││ │     │   M dia  │              │ ││ ├────┬──────┬──────┤ │ │ ║
║  ║  │ │  n2: Rx=−25 kN   ││ │ ────┼──────────┼─────         │ ││ │Node│ Fy  │ Mz   │ │ │ ║
║  ║  │ │                  ││ │     ╰──────────╯              │ ││ ├────┼──────┼──────┤ │ │ ║
║  ║  │ │  .tree-category  ││ │  ▽                   ▽        │ ││ │ n1 │ 37.2 │ 0.0  │ │ │ ║
║  ║  │ │   PRVKY          ││ │                               │ ││ │ n2 │ 12.8 │ 0.0  │ │ │ ║
║  ║  │ │                  ││ └───────────────────────────────┘ ││ └──────────────────┘ │ │ ║
║  ║  │ │ .tree-item       ││                                   ││                      │ │ ║
║  ║  │ │  e1 Mmax=62.5    ││                                   ││  Extrémy prvku e1    │ │ ║
║  ║  │ │     ★ (accent)   ││                                   ││ ┌──────────────────┐ │ │ ║
║  ║  │ │                  ││                                   ││ │ .forces-table    │ │ │ ║
║  ║  │ │                  ││                                   ││ ├────┬──────┬──────┤ │ │ ║
║  ║  │ │                  ││                                   ││ │    │ M    │ V    │ │ │ ║
║  ║  │ │                  ││                                   ││ ├────┼──────┼──────┤ │ │ ║
║  ║  │ │                  ││                                   ││ │max │★62.5 │ 37.2 │ │ │ ║
║  ║  │ │                  ││                                   ││ │min │ 0.0  │ -12.8│ │ │ ║
║  ║  │ │                  ││                                   ││ └──────────────────┘ │ │ ║
║  ║  │ └──────────────────┘└───────────────────────────────────┘└─────────────────────┘ │ ║
║  ║  └────────────────────────────────────────────────────────────────────────────────────┘ ║
║  ║  ┌──────────────────────────────────────────────────────────────────────────────────┐  ║
║  ║  │  .catalog-footer                                                                 │  ║
║  ║  │  [Charakteristické řezy…]  .se-btn-secondary    [Odeslat do DCE ▶] .se-btn-primary│ ║
║  ║  └──────────────────────────────────────────────────────────────────────────────────┘  ║
```

---

## 4. CharacteristicSectionsDialog

Menší dialog. Volán z footeru Výsledků nebo z A4 kapitoly.  
Používá `.lg-*` vzor (jako LoadGroupEditorDialog). **860 × 560 px**.

```
╔══════════════════════════════════════════════════════════════════════════════════╗
║  .catalog-overlay                                                                ║
║                                                                                  ║
║  ╔════════════════════════════════════════════════════════════════════════════╗  ║
║  ║  .lg-dialog  (width:860px; height:560px)                                   ║  ║
║  ║                                                                            ║  ║
║  ║  ┌──────────────────────────────────────────────────────────────────────┐  ║  ║
║  ║  │  .lg-header                                                          │  ║  ║
║  ║  │   ✂  Charakteristické řezy pro posouzení             .lg-title      │  ║  ║
║  ║  │                                                                [×]   │  ║  ║
║  ║  └──────────────────────────────────────────────────────────────────────┘  ║  ║
║  ║                                                                            ║  ║
║  ║  ┌──────────────────────────────────────────────────────────────────────┐  ║  ║
║  ║  │  .lg-filter-bar                                                      │  ║  ║
║  ║  │                                                                      │  ║  ║
║  ║  │  .lg-filter-label  TYP:                                              │  ║  ║
║  ║  │  ┌─────┐  ┌─────────────────┐  ┌──────────┐                         │  ║  ║
║  ║  │  │ Vše │  │ ★  Automatické  │  │  Vlastní │                         │  ║  ║
║  ║  │  └─────┘  └─────────────────┘  └──────────┘                         │  ║  ║
║  ║  └──────────────────────────────────────────────────────────────────────┘  ║  ║
║  ║                                                                            ║  ║
║  ║  ┌──────────────────────────────────────────────────────────────────────┐  ║  ║
║  ║  │  .lg-body  (display:flex)                                            │  ║  ║
║  ║  │                                                                      │  ║  ║
║  ║  │  ┌──────────────────────┐  ┌───────────────────────────────────────┐ │  ║  ║
║  ║  │  │  .lg-master          │  │  .lg-detail  (padding:20px 24px)      │ │  ║  ║
║  ║  │  │  (width:280px)       │  │                                       │ │  ║  ║
║  ║  │  │                      │  │  ┌───────────────────────────────────┐ │ │  ║  ║
║  ║  │  │  .lg-toolbar         │  │  │  Mini SVG — beam s vyznačenou     │ │ │  ║  ║
║  ║  │  │  [+ Vlastní řez]     │  │  │  polohou řezu (červená svislá     │ │ │  ║  ║
║  ║  │  │                      │  │  │  čára na canvasu)                 │ │ │  ║  ║
║  ║  │  │  .lg-item.selected   │  │  │                                   │ │ │  ║  ║
║  ║  │  │  ★  Mmax             │  │  │    ────────x────────────          │ │ │  ║  ║
║  ║  │  │     x = 4.00 m       │  │  │    ▽                   ▽          │ │ │  ║  ║
║  ║  │  │     LC: ULS1         │  │  └───────────────────────────────────┘ │ │  ║  ║
║  ║  │  │                      │  │                                       │ │  ║  ║
║  ║  │  │  .lg-item            │  │  .lg-fieldset                         │ │  ║  ║
║  ║  │  │  ★  Mmin             │  │   Poloha a zatížení:                  │ │  ║  ║
║  ║  │  │     x = 0.00 m       │  │                                       │ │  ║  ║
║  ║  │  │     LC: ULS2         │  │   .lg-form-row                        │ │  ║  ║
║  ║  │  │                      │  │   Poloha x    [ 4.00 ]   m  (RO★)    │ │  ║  ║
║  ║  │  │  .lg-item            │  │   LC/Komb.    [ ULS1  ▼ ]             │ │  ║  ║
║  ║  │  │  ★  Vmax             │  │                                       │ │  ║  ║
║  ║  │  │     x = 0.00 m       │  │   Vnitřní síly (read-only):           │ │  ║  ║
║  ║  │  │     LC: ULS1         │  │   .lg-form-row                        │ │  ║  ║
║  ║  │  │                      │  │   N_Ed   [  -1.2  ]   kN              │ │  ║  ║
║  ║  │  │  ─ ─ ─ ─ ─ ─ ─ ─ ─  │  │   M_Ed   [  62.5  ]   kNm  ★         │ │  ║  ║
║  ║  │  │                      │  │   V_Ed   [  37.2  ]   kN              │ │  ║  ║
║  ║  │  │  .lg-item            │  │                                       │ │  ║  ║
║  ║  │  │  +  Vlastní řez 1    │  │  .lg-fieldset  (jen vlastní řezy)    │ │  ║  ║
║  ║  │  │     x = 2.50 m       │  │   Poloha x    [ 2.50 ]   m  [✎]      │ │  ║  ║
║  ║  │  │     LC: ULS1         │  │   LC/Komb.    [ ULS1  ▼ ]             │ │  ║  ║
║  ║  │  │                      │  │                                       │ │  ║  ║
║  ║  │  └──────────────────────┘  └───────────────────────────────────────┘ │  ║  ║
║  ║  └──────────────────────────────────────────────────────────────────────┘  ║  ║
║  ║                                                                            ║  ║
║  ║  ┌──────────────────────────────────────────────────────────────────────┐  ║  ║
║  ║  │  .lg-footer                                                          │  ║  ║
║  ║  │                                                                      │  ║  ║
║  ║  │  .lg-footer-left                           .lg-footer-right          │  ║  ║
║  ║  │   ● 4 řezy celkem                          [Zrušit]  [Posoudit →]   │  ║  ║
║  ║  └──────────────────────────────────────────────────────────────────────┘  ║  ║
║  ╚════════════════════════════════════════════════════════════════════════════╝  ║
╚══════════════════════════════════════════════════════════════════════════════════╝
```

**Legend: ★** = automatický řez (read-only poloha), **+** = vlastní (editovatelný).  
Badge barvy: ★ uses `.lg-badge` s `bg: var(--warning)` (#c49a3e), + uses `bg: var(--info)` (#3e7ec4).

---

## 5. A4 Kapitola — Beam Analysis

Nová kapitola v A4 výstupu (analogie `CheckChapter`). Klikatelné SVG prvky.

```
┌─────────────────────────────────────────────────────────┐
│  A4 Page  (794px wide)                                  │
│                                                         │
│  ┌────────────────────────────────────────────────────┐ │
│  │  4.  Beam Analysis                    .chapter-hdr │ │
│  └────────────────────────────────────────────────────┘ │
│                                                         │
│  ┌────────────────────────────────────────────────────┐ │
│  │  4.1  Schéma konstrukce               .section-hdr │ │
│  │                                                    │ │
│  │  ┌──────────────────────────────────────────────┐  │ │
│  │  │  [SVG — klikatelný, cursor:pointer]          │  │ │
│  │  │  .dce-svg-view (hover: outline accent)       │  │ │
│  │  │                                              │  │ │
│  │  │         ↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓               │  │ │
│  │  │         ───────────────────────              │  │ │
│  │  │         │              ↓ P=-50kN             │  │ │
│  │  │    ▽────────────────────────────▽            │  │ │
│  │  │    ←────────── 8.00 m ──────────→            │  │ │
│  │  │                                              │  │ │
│  │  │  ╔══ tooltip při hover ══╗                   │  │ │
│  │  │  ║  Klikněte pro úpravu ║                   │  │ │
│  │  │  ╚═══════════════════════╝                   │  │ │
│  │  └──────────────────────────────────────────────┘  │ │
│  │  → onclick: otevře BeamAnalysisDialog tab:Geometrie │ │
│  └────────────────────────────────────────────────────┘ │
│                                                         │
│  ┌────────────────────────────────────────────────────┐ │
│  │  4.2  Zatížení — LC1 Dead             .section-hdr │ │
│  │                                                    │ │
│  │  ┌──────────────────────────────────────────────┐  │ │
│  │  │  [SVG — klikatelný]                          │  │ │
│  │  │                                              │  │ │
│  │  │         ↓↓↓↓↓ g=-12 kN/m ↓↓↓↓↓↓             │  │ │
│  │  │         ───────────────────────              │  │ │
│  │  │    ▽                            ▽            │  │ │
│  │  └──────────────────────────────────────────────┘  │ │
│  │  → onclick: otevře BeamAnalysisDialog tab:Zatížení  │ │
│  │                                                    │ │
│  │  ┌──────────────────────────────────────────────┐  │ │
│  │  │  Tabulka zatížení  .forces-table             │  │ │
│  │  │  ┌───────┬─────────┬─────┬────┬────┬──────┐  │  │ │
│  │  │  │  LC   │  Typ    │  wy │ wx │ Od │  Do  │  │  │ │
│  │  │  ├───────┼─────────┼─────┼────┼────┼──────┤  │  │ │
│  │  │  │ Dead  │ Uniform │ -12 │  0 │  0 │  8.0 │  │  │ │
│  │  │  │ Live  │ Point   │ -50 │  0 │4.0 │  —   │  │  │ │
│  │  │  └───────┴─────────┴─────┴────┴────┴──────┘  │  │ │
│  │  └──────────────────────────────────────────────┘  │ │
│  └────────────────────────────────────────────────────┘ │
│                                                         │
│  ┌────────────────────────────────────────────────────┐ │
│  │  4.3  Vnitřní síly — ULS1             .section-hdr │ │
│  │                                                    │ │
│  │  [SVG M-diagram]  [SVG V-diagram]                  │ │
│  │                                                    │ │
│  │  ┌──────────────────────────────────────────────┐  │ │
│  │  │  Extrémy  .forces-table                      │  │ │
│  │  │  ┌──────┬────────┬──────────┬────────────┐   │  │ │
│  │  │  │ Prvek│  x [m] │ M [kNm]  │   V [kN]   │   │  │ │
│  │  │  ├──────┼────────┼──────────┼────────────┤   │  │ │
│  │  │  │  e1  │  4.00  │ ★ 62.50  │   37.20    │   │  │ │
│  │  │  └──────┴────────┴──────────┴────────────┘   │  │ │
│  │  └──────────────────────────────────────────────┘  │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

---

## 6. Strom Razor komponent

```
BeamAnalysisDialog.razor          ← CatalogDialog wrapper, 1200×760px
├── FilterBar (slot)
│   └── BeamTabChips.razor        ← Geometry / Loads / Results chips
│
├── LeftPanel (slot)
│   ├── BeamTemplatePanel.razor   ← Tab Geometrie: tree-panel se šablonami
│   ├── LoadCaseListPanel.razor   ← Tab Zatížení: lg-master s CRUD
│   └── ResultsNavPanel.razor     ← Tab Výsledky: tree uzly / prvky
│
├── CenterPanel (slot)
│   └── BeamCanvas.razor          ← SVG canvas (mění obsah dle tabu)
│       ├── BeamSvgRenderer.cs    ← čistá C# třída, generuje SVG string
│       └── preview-header toolbar (Sel/Pan, diagram switcher)
│
├── RightPanel (slot)
│   ├── GeometryPropertiesPanel.razor   ← délka, podpory, průřezy
│   ├── LoadPropertiesPanel.razor       ← editor vybraného zatížení
│   └── ResultsPropertiesPanel.razor    ← tabulky reakcí a extrémů
│
└── Footer (slot)
    └── BeamDialogFooter.razor    ← tlačítka dle aktivního tabu

CharacteristicSectionsDialog.razor   ← lg-dialog 860×560px
├── lg-master: seznam řezů (auto ★ + vlastní +)
└── lg-detail: mini SVG + form (poloha, N/M/V read-only)

BeamAnalysisChapter.razor            ← A4 CheckChapter varianta
├── klikatelný SVG geometrie
├── klikatelný SVG zatížení + tabulka
└── SVG M/V diagramy + tabulka extrémů
```

---

## 7. CSS třídy — co existuje vs. co je nové

| Třída | Stav | Popis |
|---|---|---|
| `.catalog-overlay/dialog/header/body/footer` | ✅ existuje | `CatalogDialog.razor.css` |
| `.catalog-left/center/right` | ✅ existuje | `CatalogDialog.razor.css` |
| `.se-filter-bar`, `.se-chip`, `.se-chip.active` | ✅ existuje | `SectionEditorDialog.razor.css` |
| `.se-toolbar`, `.se-tb-btn` | ✅ existuje | `SectionEditorDialog.razor.css` |
| `.se-btn-primary`, `.se-btn-secondary` | ✅ existuje | `SectionEditorDialog.razor.css` |
| `.tree-panel`, `.tree-item`, `.tree-category` | ✅ existuje | `SectionEditorTreePanel.razor.css` |
| `.tree-item.selected` | ✅ existuje | `SectionEditorTreePanel.razor.css` |
| `.preview-header`, `.preview-canvas` | ✅ existuje | `SectionEditorPreview.razor.css` |
| `.lg-master`, `.lg-item`, `.lg-item.selected` | ✅ existuje | `LoadGroupEditorDialog.razor.css` |
| `.lg-toolbar`, `.lg-tb-btn` | ✅ existuje | `LoadGroupEditorDialog.razor.css` |
| `.lg-form-row`, `.lg-form-input`, `.lg-form-unit` | ✅ existuje | `LoadGroupEditorDialog.razor.css` |
| `.lg-fieldset`, `.lg-fieldset-title` | ✅ existuje | `LoadGroupEditorDialog.razor.css` |
| `.lg-footer`, `.lg-btn`, `.lg-btn-primary` | ✅ existuje | `LoadGroupEditorDialog.razor.css` |
| `.forces-table` | ✅ existuje | DCE výsledkové tabulky |
| `.module-btn`, `.module-btn.active` | ✅ existuje | `TopBar.razor` (topbar-modules) |
| `.beam-canvas-*` | 🆕 nové | SVG canvas interakce |
| `.beam-support-*` | 🆕 nové | SVG symboly podpor |
