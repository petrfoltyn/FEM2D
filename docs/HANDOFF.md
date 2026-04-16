# Handoff — FEALiTE2D

Snapshot pro pokračování práce na jiném počítači. Aktualizováno **2026-04-16**.

---

## 1. Aktuální stav

### Solution
6 projektů, vše na **.NET 10 LTS** (SDK pinnuto v `global.json`):

| Projekt | Stav | Poznámka |
|---|---|---|
| `FEALiTE2D` | ✅ stabilní | core knihovna, NuGet 1.1.2 |
| `FEALiTE2D.Plotting` | ✅ stabilní | DXF výstup, NuGet 1.1.2 |
| `FEALiTE2D.Api.Contracts` | ✅ nový | DTOs + mapping + validator + `AnalysisService`, NuGet 0.1.0 |
| `FEALiTE2D.Api` | ✅ nový | ASP.NET Core minimal API, port 5180 |
| `FEALiTE2D.Client` | ✅ nový | typed `HttpClient` + DI extension, NuGet 0.1.0 |
| `FEALiTE2D.Tests` | ✅ | NUnit 3, **44/44 zelených** |

### Klíčová dokumentace
- [`README.md`](../README.md) — přehled solution + quick start
- [`CLAUDE.md`](../CLAUDE.md) — navigace pro Claude Code (build, architektura, konvence)
- [`docs/rest-api-design.md`](rest-api-design.md) — kompletní REST API specifikace (814 řádků)
- [`docs/ui-design-dce-integration.md`](ui-design-dce-integration.md) — návrh UI pro DCE
- [`docs/ui-wireframes.md`](ui-wireframes.md) — ASCII wireframy
- [`docs/ui-preview.html`](ui-preview.html) — interaktivní HTML náhledy 6 obrazovek

---

## 2. Co je hotovo (od posledního upstream commitu)

1. **Migrace na .NET 10 LTS** (z původního net45/netstandard2.0/net8.0)
   - `global.json` pinuje SDK 10.0.0
   - Všechny projekty na `net10.0`, sdílená metadata v `Directory.Build.props`
2. **REST API stack** kompletní
   - DTO contract s polymorfismem (System.Text.Json, `[JsonDerivedType]`)
   - JSON konvence: camelCase, uppercase pro `E`/`Iz`/`A`/…, `DictionaryKeyPolicy=null`, `AllowOutOfOrderMetadataProperties=true`
   - `POST /api/v1/analyze` → 200 (Successful) / 422 (Failure se strukturovaným tělem)
   - Validátor pokrývá všechna pravidla z designu §6
3. **Typed klient** `FealiteApiClient` (Blazor WASM compatible)
4. **Build infrastruktura** — PowerShell skripty v `build/`, výstup do `/out/`
5. **Doc / UI návrhy** — REST API design, UI návrhy pro DCE integraci

---

## 3. Otevřené body (priorita shora dolů)

### H — Integrace `FEALiTE2D.Client` do DCE
**Priorita: vysoká** (jediný blocker pro reálné UI)
- Cílový projekt: `d:\CSSCPrototype` (Blazor WebAssembly app)
- Postup:
  1. `services.AddFealiteApiClient(configuration)` v Program.cs DCE
  2. Implementovat `BeamAnalysisDialog` Razor komponentu podle [`docs/ui-preview.html`](ui-preview.html)
  3. Tři taby (Geometrie / Zatížení / Výsledky) + `CharacteristicSections` dialog
  4. Output (N_Ed, M_Ed, V_Ed) předat do existujícího cross-section posouzení v DCE

### C — Dockerfile + docker-compose pro `FEALiTE2D.Api`
**Priorita: střední** (až na druhém PC)
- `FROM mcr.microsoft.com/dotnet/aspnet:10.0`
- Multi-stage build, expose 8080
- `docker-compose.yml` pro dev (FEALiTE2D.Api + případně Redis pro budoucí stateful variantu)

### E — Vulnerability `System.Drawing.Common 4.5.0`
**Priorita: nízká** (transitive z `netDxf.netstandard 2.4.0`, jen Plotting projekt)
- Bumpnout `netDxf.netstandard` na 3.x (pokud existuje compatible verze)
- Nebo přidat explicit `<PackageReference Include="System.Drawing.Common" Version="8.0.x" />` jako transitive override

### F — README.md pro `FEALiTE2D.Api.Contracts` a `FEALiTE2D.Client`
**Priorita: nízká** (NuGet warning při buildu)

### G — Smoke JSON files hygiene
**Priorita: nízká**
- `FEALiTE2D.Api/smoke.json` a `FEALiTE2D.Api/smoke-disorder.json` — buď přesunout do `docs/examples/` nebo přidat do `.gitignore` a pak smazat

---

## 4. Continue na jiném počítači

```powershell
# 1. Klon (pokud ještě nemáš)
git clone https://github.com/petrfoltyn/FEM2D.git
cd FEM2D

# 2. Pull nejnovější
git pull petrfoltyn main      # nebo "git pull" pokud je petrfoltyn nastavený jako tracking

# 3. Ověřit .NET 10 SDK
dotnet --list-sdks            # musí obsahovat 10.0.x

# 4. Restore + build + test
.\build\test.ps1              # 44/44 musí být zelené

# 5. Spuštění API
dotnet run --project FEALiTE2D.Api
# → http://localhost:5180/swagger
```

### Git remotes

| Remote | URL | Účel |
|---|---|---|
| `origin` | `https://github.com/FEALiTE/FEALiTE2D.git` | Upstream, **read-only** (nemáme push práva) |
| `petrfoltyn` | `https://github.com/petrfoltyn/FEM2D.git` | Naše práce, **push sem** |

```powershell
# Vždy explicitně:
git push petrfoltyn main
```

---

## 5. Konvence k zachování

- **Jazyk** — status/commit zprávy česky, kód a komentáře anglicky
- **Build** — PowerShell skripty v `build/`, ne přímé `dotnet` (ale funguje obojí)
- **Output** — vše do `/out/` (gitignored), žádné `bin/`, `obj/` mimo NuGet packages
- **JSON contract** — neměnit beze shody s `ApiJsonOptions.Default`; jakákoli změna naming policy nebo discriminator strategie rozbije DCE klienta
- **Polymorfismus** — nové subtypy přidávat přes `[JsonDerivedType]` na abstract base
- **Mapping** — DTO ↔ doménový enum vždy přes `EnumMapping` (extension methods), neduplikovat
- **Testy** — každý nový endpoint potřebuje test přes `WebApplicationFactory`

---

## 6. Známé footguny

1. **NuGet OOM v MSBuildu** — pokud build hodí `OutOfMemoryException` v `NuGet.Frameworks`, spustit `dotnet build-server shutdown` a build znovu. Stalo se po dlouhém běhu API serveru paralelně s testy.
2. **Stale `dotnet.exe` procesy** — po `dotnet run` v background módu je třeba TaskStop, jinak port 5180 zůstane obsazený.
3. **Discriminator JSON** — díky .NET 10 může být `"type"` kdekoli v objektu, ale na .NET 8 musel být první. Pokud někdy downgrade, vrátí se old footgun.
