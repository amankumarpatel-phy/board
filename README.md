# AMAN EDGE SMARTBOARD

A Windows desktop teaching board designed for a laptop + graphics pen tablet, with an immersive full-screen lecture interface, dark studio chrome and compact bottom toolbars. Built with WPF/.NET 10 so Windows Ink stylus input is handled natively.

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
- Zoom controls (50%–250%) with scrollable canvas navigation
- Mouse-wheel/scrollbar navigation through the zoomed board
- Open PDF files and navigate page-by-page with rendered page thumbnails
- Keep handwriting and inserted objects independently for every PDF page in the lesson project
- Insert editable text boxes and image objects onto the board
- Save and restore inserted text and images in `.aeb` lesson files
- Render PDF pages as the board background while keeping pen annotations editable
- Save the source PDF inside the `.aeb` lesson file so the lesson remains portable
- Live webcam picture-in-picture preview with camera on/off control (OpenCvSharp; default camera device)
- Temporary red laser-pointer strokes that automatically disappear after 10 seconds and are excluded from lesson saves and PNG exports
- Lecture title/start screen for launching a teaching session
- Teacher Mode full-screen presentation (F5 to enter, Esc to exit)
- Compact floating quick-tools palette
- Movable and resizable webcam picture-in-picture window
- Configurable laser pointer colour and fade duration (2–30 seconds)
- Blackboard and cream board backgrounds in addition to plain, grid, ruled and dotted styles
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
PowerPoint import, annotated PDF export, LaTeX equations, graphing, physics visualizers, screen capture/recording, autosave and richer object tools are planned for subsequent releases.
