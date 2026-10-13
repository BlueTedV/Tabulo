# Tabulo

> **Tabulo** is a modern, high-performance, multi-table infinite canvas spreadsheet application built on .NET 10 and WPF.

Unlike traditional single-grid spreadsheets, Tabulo treats tables as first-class blocks that can be placed, moved, styled, and interconnected freely across an infinite 2D canvas — combining the flexibility of an infinite workspace with the computational power of a formula engine.

---

## ✨ Features

- **Infinite 2D Multi-Table Canvas**:
  - Pan, zoom (10% to 400%), and reposition tables anywhere on the infinite canvas.
  - Interactive Minimap / Navigator drawer with quick table switching, renaming, duplication, and deletion.
  - Grid snapping and alignment guides.
- **Cross-Table Formulas & Calculation Engine**:
  - Full formula support (`SUM`, `AVERAGE`, `COUNT`, `MIN`, `MAX`, `IF`, `AND`, `OR`, `NOT`, `VLOOKUP`, `XLOOKUP`, `INDEX`, `MATCH`, `LET`, and more).
  - Cross-table qualified references (e.g. `=Sales.B2 * Rates.C1` or `[Regional Sales]!B2:B10`).
  - Topological sorting DAG (Directed Acyclic Graph) for reactive cell recalculation and circular reference detection.
- **Advanced C# Roslyn Scripting**:
  - Integrated scripting host powered by Microsoft CodeAnalysis C# Scripting for dynamic formula automation and extensions.
- **Rich Vector UI & Modern Themes**:
  - 35+ custom vector SVG icons rendered natively via SharpVectors.
  - Ribbon navigation bar (Home, Table & Insert, Data & View) with currency, accounting, percent, date, integer, and comma number formatting.
  - Expandable multi-line Formula Bar with real-time function signature lightbulb tooltips.
- **Undo / Redo & File Persistence**:
  - Robust Command Pattern undo/redo history covering cell edits, structural changes, formatting, and table layout adjustments.
  - Native JSON document serialization (`*.sheetx` / `*.json`).

---

## 🛠️ Architecture

Tabulo is organized into modular .NET 10 libraries:

- **`SpreadsheetApp`**: WPF Presentation layer (MVVM, Ribbon tabs, Infinite Canvas rendering, Vector SVGs, Navigation drawer).
- **`SpreadsheetApp.Core`**: Domain models, Calculation Engine, AST lexer & parser, DAG dependency graph, Function Catalog, and Document Storage.
- **`SpreadsheetApp.Scripting`**: Roslyn-powered C# scripting execution host.
- **`SpreadsheetApp.Tests`**: Comprehensive unit & integration test suite.

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows 10/11 (WPF runtime)

### Build & Run
```bash
# Clone the repository
git clone https://github.com/your-username/Tabulo.git
cd Tabulo

# Restore and build the solution
dotnet build

# Run unit tests
dotnet test

# Launch Tabulo
dotnet run --project SpreadsheetApp/SpreadsheetApp.csproj
```

---

## 📄 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
