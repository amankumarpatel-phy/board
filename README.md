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
- Equation Composer with common Greek, calculus and mathematical symbols and LaTeX-style command substitutions
- 2D function plotter for arithmetic, powers, trigonometric, inverse-trigonometric, exponential, logarithmic and square-root functions; graphs can be inserted as board images
- Export the current page to PNG
- Windows GitHub Actions workflow publishes a self-contained x64 EXE

## Install on Windows
1. Open the latest successful **Build Windows installer** run in GitHub Actions.
2. Download the artifact named `AMAN-EDGE-SMARTBOARD-Setup-Windows-x64`.
3. Extract the artifact ZIP and run the setup EXE.
4. Follow the setup wizard. A Start Menu shortcut is created; a desktop shortcut is optional.
5. The installer includes the self-contained application, so a separate .NET runtime installation is not required.

The installer supports 64-bit Windows. Windows may display a SmartScreen warning because the installer is not code-signed; only proceed if you downloaded it from this repository's Actions artifact.

## Build on Windows manually
1. Install the .NET 10 SDK and Inno Setup 6.
2. Publish the application:
   ```powershell
   dotnet publish AmanEdgeSmartBoard/AmanEdgeSmartBoard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
   ```
3. Compile `installer/AmanEdgeSmartBoard.iss` with Inno Setup Compiler (ISCC), from the repository root.

Every push to `main` and manual workflow dispatch builds a Windows x64 installer and uploads it as a downloadable GitHub Actions artifact. A separate portable EXE artifact is also published.

## Pen tablet setup
Connect the tablet, install its Windows driver, and set Windows pen/stylus mode if the manufacturer offers that option. The board uses WPF stylus events and pressure-aware ink attributes; pressure support depends on the tablet driver.

## Roadmap
PowerPoint import, annotated PDF export, LaTeX equations, graphing, physics visualizers, screen capture/recording, autosave and richer object tools are planned for subsequent releases.
