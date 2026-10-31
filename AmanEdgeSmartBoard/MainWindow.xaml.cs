using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AmanEdgeSmartBoard;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<StrokeCollection> _pages = new();
    private readonly Stack<StrokeCollection> _undo = new();
    private readonly Stack<StrokeCollection> _redo = new();
    private int _pageIndex;
    private bool _loading;
    private string _currentFile = "";

    public MainWindow()
    {
        InitializeComponent();
        _pages.Add(new StrokeCollection());
        Board.Strokes = _pages[0];
        Board.Strokes.StrokesChanged += (_, _) => { };
        ApplyBackground();
        SetPen();
    }

    private Color SelectedColor() => (ColourBox.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
    {
        "Black" => Colors.Black, "Red" => Colors.Red, "Blue" => Colors.DodgerBlue,
        "Green" => Colors.ForestGreen, "Yellow" => Colors.Gold, _ => Colors.White
    };

    private void SetPen()
    {
        Board.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = SelectedColor(), Width = WidthSlider.Value, Height = WidthSlider.Value,
            FitToCurve = true, IgnorePressure = false, StylusTip = StylusTip.Ellipse
        };
    }

    private StrokeCollection Snapshot() => new(Board.Strokes);

    private void SaveUndo()
    {
        _undo.Push(Snapshot());
        if (_undo.Count > 50) { var keep = _undo.Reverse().Take(50).Reverse().ToArray(); _undo.Clear(); foreach (var s in keep) _undo.Push(s); }
        _redo.Clear();
    }

    private void Pen_Click(object sender, RoutedEventArgs e) { Board.EditingMode = InkCanvasEditingMode.Ink; SetPen(); }
    private void Highlighter_Click(object sender, RoutedEventArgs e)
    {
        Board.EditingMode = InkCanvasEditingMode.Ink;
        var a = new DrawingAttributes { Color = SelectedColor(), Width = WidthSlider.Value * 4, Height = WidthSlider.Value * 4, IsHighlighter = true, IgnorePressure = true };
        Board.DefaultDrawingAttributes = a;
    }
    private void Select_Click(object sender, RoutedEventArgs e) => Board.EditingMode = InkCanvasEditingMode.Select;
    private void StrokeEraser_Click(object sender, RoutedEventArgs e) => Board.EditingMode = InkCanvasEditingMode.EraseByStroke;
    private void PointEraser_Click(object sender, RoutedEventArgs e) => Board.EditingMode = InkCanvasEditingMode.EraseByPoint;
    private void Colour_Changed(object sender, SelectionChangedEventArgs e) { if (!_loading && Board != null) SetPen(); }
    private void Width_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (WidthLabel != null) WidthLabel.Text = $"{WidthSlider.Value:0} px"; if (!_loading && Board != null && Board.EditingMode == InkCanvasEditingMode.Ink) SetPen(); }

    private void Background_Changed(object sender, SelectionChangedEventArgs e) { if (!_loading && Board != null) ApplyBackground(); }
    private void ApplyBackground()
    {
        if (Board == null) return;
        string mode = (BackgroundBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Plain";
        Brush brush = Brushes.White;
        if (mode == "Grid" || mode == "Ruled" || mode == "Dots")
        {
            var drawing = new DrawingGroup();
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(220, 228, 238)), 0.7);
            if (mode == "Dots")
            {
                for (int x = 10; x < 200; x += 20) for (int y = 10; y < 200; y += 20) drawing.Children.Add(new GeometryDrawing(Brushes.LightGray, null, new EllipseGeometry(new Point(x, y), 0.8, 0.8)));
            }
            else
            {
                for (int i = 0; i < 200; i += 20)
                {
                    if (mode == "Grid") { drawing.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(i, 0), new Point(i, 200)))); drawing.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(0, i), new Point(200, i)))); }
                    else drawing.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(0, i), new Point(200, i))));
                }
            }
            brush = new DrawingBrush(drawing) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 200, 200), ViewportUnits = BrushMappingMode.Absolute, Stretch = Stretch.None };
        }
        Board.Background = brush;
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_undo.Count == 0) return; _redo.Push(Snapshot()); Board.Strokes = _undo.Pop(); _pages[_pageIndex] = Board.Strokes;
    }
    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (_redo.Count == 0) return; _undo.Push(Snapshot()); Board.Strokes = _redo.Pop(); _pages[_pageIndex] = Board.Strokes;
    }
    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Clear all ink on this page?", "Clear page", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        SaveUndo(); Board.Strokes.Clear();
    }
    private void AddPage_Click(object sender, RoutedEventArgs e)
    {
        _pages[_pageIndex] = Board.Strokes; _pages.Add(new StrokeCollection()); _pageIndex = _pages.Count - 1;
        Board.Strokes = _pages[_pageIndex]; _undo.Clear(); _redo.Clear(); UpdatePageLabel();
    }
    private void Previous_Click(object sender, RoutedEventArgs e) => SwitchPage(Math.Max(0, _pageIndex - 1));
    private void Next_Click(object sender, RoutedEventArgs e) => SwitchPage(Math.Min(_pages.Count - 1, _pageIndex + 1));
    private void SwitchPage(int index)
    {
        if (index == _pageIndex) return; _pages[_pageIndex] = Board.Strokes; _pageIndex = index; Board.Strokes = _pages[index]; _undo.Clear(); _redo.Clear(); UpdatePageLabel();
    }
    private void UpdatePageLabel() => PageLabel.Text = $"Page {_pageIndex + 1} of {_pages.Count}";

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Start a new board? Unsaved changes will be lost.", "New board", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _pages.Clear(); _pages.Add(new StrokeCollection()); _pageIndex = 0; Board.Strokes = _pages[0]; _undo.Clear(); _redo.Clear(); _currentFile = ""; UpdatePageLabel();
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Aman Edge Board (*.aeb)|*.aeb", DefaultExt = ".aeb", FileName = string.IsNullOrWhiteSpace(_currentFile) ? "Lesson.aeb" : Path.GetFileName(_currentFile) };
        if (dialog.ShowDialog() != true) return;
        _pages[_pageIndex] = Board.Strokes;
        var data = _pages.Select(p => { using var ms = new MemoryStream(); p.Save(ms); return Convert.ToBase64String(ms.ToArray()); }).ToArray();
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(new BoardDocument { Version = 1, Pages = data }));
        _currentFile = dialog.FileName; MessageBox.Show("Board saved successfully.", "AMAN EDGE SMARTBOARD", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Aman Edge Board (*.aeb)|*.aeb" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var doc = JsonSerializer.Deserialize<BoardDocument>(File.ReadAllText(dialog.FileName)) ?? throw new InvalidDataException("Invalid board file.");
            _pages.Clear();
            foreach (var encoded in doc.Pages) { using var ms = new MemoryStream(Convert.FromBase64String(encoded)); _pages.Add(new StrokeCollection(ms)); }
            if (_pages.Count == 0) _pages.Add(new StrokeCollection());
            _pageIndex = 0; Board.Strokes = _pages[0]; _undo.Clear(); _redo.Clear(); _currentFile = dialog.FileName; UpdatePageLabel();
        }
        catch (Exception ex) { MessageBox.Show($"Could not open file: {ex.Message}", "Open error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", DefaultExt = ".png", FileName = $"Board-Page-{_pageIndex + 1}.png" };
        if (dialog.ShowDialog() != true) return;
        var size = new Size(Math.Max(1, Board.ActualWidth), Math.Max(1, Board.ActualHeight));
        Board.Measure(size); Board.Arrange(new Rect(size));
        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(Board);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(dialog.FileName); encoder.Save(stream);
    }
    private sealed class BoardDocument { public int Version { get; set; } = 1; public string[] Pages { get; set; } = Array.Empty<string>(); }
}
