# AMAN EDGE SMARTBOARD

A Windows desktop teaching board designed for a laptop + graphics pen tablet. Built with WPF/.NET 10 so Windows Ink stylus input is handled natively.

## Current MVP features
- Pen input through WPF InkCanvas (stylus/tablet pressure is respected when the driver exposes it)
- Pen, highlighter, selection, stroke eraser and point eraser
- Colour and stroke-width controls
- Plain, grid, ruled and dotted board backgrounds
- Multiple pages
- Undo/redo, clear page
- Save/open multi-page lessons in the native `.aeb` format
- Straight line, rectangle, ellipse and arrow drawing tools
- Keyboard shortcuts: Ctrl+Z undo, Ctrl+Y redo, Esc return to pen
- Export the current page to PNG
- Windows GitHub Actions workflow publishes a self-contained x64 EXE

## Build on Windows
1. Install the .NET 10 SDK.
2. Open the repository root in PowerShell.
3. Run:
   ```powershell
   dotnet publish AmanEdgeSmartBoard/AmanEdgeSmartBoard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
   ```
4. Run `publish/AmanEdgeSmartBoard.exe`.

A Windows x64 build is also produced automatically by GitHub Actions on each push to `main`. Download it from the workflow run's **Artifacts** section.

## Pen tablet setup
Connect the tablet, install its Windows driver, and set Windows pen/stylus mode if the manufacturer offers that option. The board uses WPF stylus events and pressure-aware ink attributes; pressure support depends on the tablet driver.

## Roadmap
PDF import/annotation, LaTeX equations, graphing, physics visualizers, screen capture/recording, slide/PPT import, autosave and richer object tools are planned for subsequent releases.
