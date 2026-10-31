using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;
using VideoCapture = OpenCvSharp.VideoCapture;
using Mat = OpenCvSharp.Mat;
using Cv2 = OpenCvSharp.Cv2;
using System.Windows.Threading;

namespace AmanEdgeSmartBoard;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<StrokeCollection> _pages = new();
    private readonly Stack<StrokeCollection> _undo = new();
    private readonly Stack<StrokeCollection> _redo = new();
    private int _pageIndex;
    private bool _loading;
    private string _currentFile = "";
    private string _shapeTool = "Pen";
    private Point _shapeStart;
    private bool _drawingShape;
    private readonly ScaleTransform _boardScale = new(1, 1);
    private byte[]? _pdfBytes;
    private int _pdfPageIndex;
    private int _pdfPageCount;
    private string? _pdfFileName;
    private int _textObjectCount;
    private readonly Dictionary<int, StrokeCollection> _pdfAnnotations = new();
    private readonly Dictionary<int, List<BoardElement>> _pdfPageElements = new();
    private bool _changingPdfPage;
    private VideoCapture? _camera;
    private readonly DispatcherTimer _cameraTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly DispatcherTimer _laserExpiryTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Dictionary<Stroke, DateTime> _laserStrokes = new();
    private bool _laserMode;
    private bool _laserDrawing;
    private StylusPointCollection? _laserPoints;
    private bool _presentationMode;
    private Color _laserColor = Colors.Red;
    private double _laserDurationSeconds = 10;
    private Point _cameraDragStart;
    private double _cameraOffsetX, _cameraOffsetY;

    public MainWindow()
    {
        InitializeComponent();
        _pages.Add(new StrokeCollection());
        Board.Strokes = _pages[0];
        Board.LayoutTransform = _boardScale;
        Board.Strokes.StrokesChanged += (_, _) => { };
        ApplyBackground();
        SetPen();
        Board.PreviewMouseLeftButtonDown += Board_MouseDown;
        Board.PreviewMouseLeftButtonUp += Board_MouseUp;
        Board.PreviewMouseMove += Board_MouseMove;
        _cameraTimer.Tick += CameraTimer_Tick;
        _laserExpiryTimer.Tick += LaserExpiryTimer_Tick;
        _laserExpiryTimer.Start();
        Closed += (_, _) => StopCamera();
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

    private StrokeCollection Snapshot()
    {
        RemoveLaserStrokes();
        return new StrokeCollection(Board.Strokes);
    }

    private void StartLecture_Click(object sender, RoutedEventArgs e) { LectureStartOverlay.Visibility = Visibility.Collapsed; Title = $"AMAN EDGE SMARTBOARD — {LectureTitleInput.Text}"; }

    private void Presentation_Click(object sender, RoutedEventArgs e)
    {
        _presentationMode = !_presentationMode;
        TopBar.Visibility = _presentationMode ? Visibility.Collapsed : Visibility.Visible;
        PageSidebar.Visibility = _presentationMode ? Visibility.Collapsed : Visibility.Visible;
        BottomToolbar.Visibility = _presentationMode ? Visibility.Collapsed : Visibility.Visible;
        PresentationExitButton.Visibility = _presentationMode ? Visibility.Visible : Visibility.Collapsed;
        WindowStyle = _presentationMode ? WindowStyle.None : WindowStyle.SingleBorderWindow;
        WindowState = WindowState.Maximized;
    }

    private void RadialTools_Click(object sender, RoutedEventArgs e) => RadialToolsPanel.Visibility = RadialToolsPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    private void CameraFrame_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Primitives.Thumb) return;
        _cameraDragStart = e.GetPosition(WorkspaceGrid);
        CameraFrame.CaptureMouse();
        MouseMove += CameraFrame_DragMove;
        MouseLeftButtonUp += CameraFrame_DragEnd;
    }
    private void CameraFrame_DragMove(object sender, MouseEventArgs e)
    {
        if (!CameraFrame.IsMouseCaptured) return;
        var p = e.GetPosition(WorkspaceGrid);
        _cameraOffsetX += p.X - _cameraDragStart.X; _cameraOffsetY += p.Y - _cameraDragStart.Y;
        CameraFrame.Margin = new Thickness(Math.Max(0, 18 - _cameraOffsetX), Math.Max(0, 18 + _cameraOffsetY), Math.Max(0, 18 + _cameraOffsetX), Math.Max(0, 18 - _cameraOffsetY));
        _cameraDragStart = p;
    }
    private void CameraFrame_DragEnd(object sender, MouseButtonEventArgs e) { CameraFrame.ReleaseMouseCapture(); MouseMove -= CameraFrame_DragMove; MouseLeftButtonUp -= CameraFrame_DragEnd; }
    private void CameraResize_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) { CameraFrame.Width = Math.Clamp(CameraFrame.Width + e.HorizontalChange, 150, 600); CameraFrame.Height = Math.Clamp(CameraFrame.Height + e.VerticalChange, 100, 420); }

    private void LaserSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window { Title = "Laser pointer settings", Width = 340, Height = 230, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(17, 27, 41)), ResizeMode = ResizeMode.NoResize };
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = "Laser colour", Foreground = Brushes.White, Margin = new Thickness(0,0,0,6) });
        var colors = new ComboBox { ItemsSource = new[] { "Red", "Green", "Blue", "Yellow", "White" }, SelectedItem = _laserColor == Colors.Lime ? "Green" : _laserColor == Colors.DodgerBlue ? "Blue" : _laserColor == Colors.Gold ? "Yellow" : _laserColor == Colors.White ? "White" : "Red", Padding = new Thickness(6) }; panel.Children.Add(colors);
        panel.Children.Add(new TextBlock { Text = "Fade duration (seconds)", Foreground = Brushes.White, Margin = new Thickness(0,14,0,5) });
        var duration = new Slider { Minimum = 2, Maximum = 30, Value = _laserDurationSeconds, TickFrequency = 1, IsSnapToTickEnabled = true }; panel.Children.Add(duration);
        var label = new TextBlock { Text = $"{duration.Value:0} seconds", Foreground = Brushes.White }; duration.ValueChanged += (_, _) => label.Text = $"{duration.Value:0} seconds"; panel.Children.Add(label);
        var save = new Button { Content = "Apply", Margin = new Thickness(0,14,0,0), Padding = new Thickness(10) }; save.Click += (_, _) => { _laserColor = colors.SelectedItem?.ToString() switch { "Green" => Colors.Lime, "Blue" => Colors.DodgerBlue, "Yellow" => Colors.Gold, "White" => Colors.White, _ => Colors.Red }; _laserDurationSeconds = duration.Value; dialog.DialogResult = true; }; panel.Children.Add(save); dialog.Content = panel; dialog.ShowDialog();
    }

    private void LaserToggle_Click(object sender, RoutedEventArgs e)
    {
        _laserMode = !_laserMode;
        if (_laserMode) { _shapeTool = "Laser"; Board.EditingMode = InkCanvasEditingMode.None; }
        else { _shapeTool = "Pen"; Board.EditingMode = InkCanvasEditingMode.Ink; SetPen(); }
        ((Button)sender).Content = _laserMode ? "Laser ON (10s)" : "Laser 10s";
    }

    private void LaserExpiryTimer_Tick(object? sender, EventArgs e) => RemoveLaserStrokes();

    private void RemoveLaserStrokes()
    {
        var expired = _laserStrokes.Where(pair => pair.Value <= DateTime.UtcNow).Select(pair => pair.Key).ToList();
        foreach (var stroke in expired) { Board.Strokes.Remove(stroke); _laserStrokes.Remove(stroke); }
    }

    private void CameraToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_camera != null) { StopCamera(); ((Button)sender).Content = "Camera On / Off"; return; }
        try
        {
            _camera = new VideoCapture(0);
            if (!_camera.IsOpened()) { StopCamera(); MessageBox.Show("No camera was found. Connect a webcam and check Windows camera permissions.", "Camera unavailable", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            CameraFrame.Visibility = Visibility.Visible;
            _cameraTimer.Start();
            ((Button)sender).Content = "Camera Off";
        }
        catch (Exception ex) { StopCamera(); MessageBox.Show($"Unable to start camera: {ex.Message}", "Camera error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void StopCamera()
    {
        _cameraTimer.Stop();
        _camera?.Release();
        _camera?.Dispose();
        _camera = null;
        if (CameraFrame != null) CameraFrame.Visibility = Visibility.Collapsed;
        if (CameraPreview != null) CameraPreview.Source = null;
    }

    private void CameraTimer_Tick(object? sender, EventArgs e)
    {
        if (_camera == null || !_camera.IsOpened()) return;
        try
        {
            using var frame = new Mat();
            if (!_camera.Read(frame) || frame.Empty()) return;
            Cv2.ImEncode(".png", frame, out byte[] bytes);
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
            CameraPreview.Source = bitmap;
        }
        catch { StopCamera(); }
    }

    private void SaveUndo()
    {
        _undo.Push(Snapshot());
        if (_undo.Count > 50) { var keep = _undo.Reverse().Take(50).Reverse().ToArray(); _undo.Clear(); foreach (var s in keep) _undo.Push(s); }
        _redo.Clear();
    }

    private void OpenPdf_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "PDF documents (*.pdf)|*.pdf" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            _pdfBytes = File.ReadAllBytes(dialog.FileName);
            _pdfFileName = Path.GetFileName(dialog.FileName);
            _pdfPageIndex = 0;
            _pdfAnnotations.Clear(); _pdfPageElements.Clear();
            using var reader = DocLib.Instance.GetDocReader(_pdfBytes, new PageDimensions(1.5));
            _pdfPageCount = reader.GetPageCount();
            BuildPdfThumbnails();
            RenderPdfPage();
        }
        catch (Exception ex)
        {
            _pdfBytes = null; _pdfPageCount = 0;
            MessageBox.Show($"Unable to open PDF: {ex.Message}", "PDF error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PreviousPdf_Click(object sender, RoutedEventArgs e) => ChangePdfPage(-1);
    private void NextPdf_Click(object sender, RoutedEventArgs e) => ChangePdfPage(1);
    private void ChangePdfPage(int delta)
    {
        if (_pdfBytes == null) return;
        int next = Math.Clamp(_pdfPageIndex + delta, 0, _pdfPageCount - 1);
        if (next == _pdfPageIndex) return;
        StoreCurrentPdfPage();
        _pdfPageIndex = next;
        RenderPdfPage();
    }

    private void RenderPdfPage()
    {
        if (_pdfBytes == null) return;
        using var reader = DocLib.Instance.GetDocReader(_pdfBytes, new PageDimensions(1.5));
        using var page = reader.GetPageReader(_pdfPageIndex);
        int width = page.GetPageWidth(), height = page.GetPageHeight();
        byte[] pixels = page.GetImage();
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        Board.Background = new ImageBrush(bitmap) { Stretch = Stretch.Uniform, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
        Board.Strokes = _pdfAnnotations.TryGetValue(_pdfPageIndex, out var pageInk) ? new StrokeCollection(pageInk) : new StrokeCollection();
        RestoreElements(_pdfPageElements.TryGetValue(_pdfPageIndex, out var pageElements) ? pageElements : new List<BoardElement>());
        _pages[_pageIndex] = Board.Strokes;
        _changingPdfPage = true; PdfThumbnails.SelectedIndex = _pdfPageIndex; _changingPdfPage = false;
        PdfPageLabel.Text = $"PDF: {_pdfPageIndex + 1} / {_pdfPageCount}";
    }

    private void BuildPdfThumbnails()
    {
        PdfThumbnails.Items.Clear();
        if (_pdfBytes == null) return;
        using var reader = DocLib.Instance.GetDocReader(_pdfBytes, new PageDimensions(0.32));
        for (int i = 0; i < reader.GetPageCount(); i++)
        {
            using var page = reader.GetPageReader(i);
            var bmp = BitmapSource.Create(page.GetPageWidth(), page.GetPageHeight(), 96, 96, PixelFormats.Bgra32, null, page.GetImage(), page.GetPageWidth() * 4);
            bmp.Freeze();
            PdfThumbnails.Items.Add(bmp);
        }
    }

    private void StoreCurrentPdfPage()
    {
        if (_pdfBytes == null) return;
        RemoveLaserStrokes();
        _pdfAnnotations[_pdfPageIndex] = new StrokeCollection(Board.Strokes);
        _pdfPageElements[_pdfPageIndex] = CaptureElements();
    }

    private void PdfThumbnail_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_changingPdfPage || PdfThumbnails.SelectedIndex < 0 || _pdfBytes == null) return;
        StoreCurrentPdfPage();
        _pdfPageIndex = PdfThumbnails.SelectedIndex;
        RenderPdfPage();
    }

    private void AddText_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window { Title = "Insert text", Width = 420, Height = 260, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = Brushes.White, ResizeMode = ResizeMode.NoResize };
        var panel = new DockPanel { Margin = new Thickness(12) };
        var input = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 20, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Text = "Type your lesson text here..." };
        DockPanel.SetDock(input, Dock.Top); panel.Children.Add(input);
        var add = new Button { Content = "Insert on board", IsDefault = true, Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(12, 8, 12, 8) };
        add.Click += (_, _) => { dialog.DialogResult = true; dialog.Close(); };
        DockPanel.SetDock(add, Dock.Bottom); panel.Children.Add(add); dialog.Content = panel;
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(input.Text)) return;
        SaveUndo();
        var box = new TextBox { Text = input.Text, FontSize = 24, FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), Foreground = new SolidColorBrush(SelectedColor()), Background = Brushes.Transparent, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinWidth = 180, MinHeight = 45, Padding = new Thickness(5), Tag = "AEB_TEXT" };
        InkCanvas.SetLeft(box, 80 + (_textObjectCount % 8) * 24); InkCanvas.SetTop(box, 70 + (_textObjectCount % 8) * 24); _textObjectCount++;
        Board.Children.Add(box);
    }

    private void AddImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(dialog.FileName); bitmap.DecodePixelWidth = 900; bitmap.EndInit(); bitmap.Freeze();
            SaveUndo();
            var image = new System.Windows.Controls.Image { Source = bitmap, Width = Math.Min(bitmap.Width, 520), Height = Math.Min(bitmap.Height, 360), Stretch = Stretch.Uniform, Tag = "AEB_IMAGE" };
            InkCanvas.SetLeft(image, 100); InkCanvas.SetTop(image, 100); Board.Children.Add(image);
        }
        catch (Exception ex) { MessageBox.Show($"Unable to insert image: {ex.Message}", "Image error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private List<BoardElement> CaptureElements()
    {
        var elements = new List<BoardElement>();
        foreach (UIElement child in Board.Children)
        {
            double left = InkCanvas.GetLeft(child), top = InkCanvas.GetTop(child);
            if (child is TextBox text && Equals(text.Tag, "AEB_TEXT"))
                elements.Add(new BoardElement { Type = "text", Content = text.Text, X = double.IsNaN(left) ? 0 : left, Y = double.IsNaN(top) ? 0 : top, Width = double.IsNaN(text.Width) ? text.ActualWidth : text.Width, Height = double.IsNaN(text.Height) ? text.ActualHeight : text.Height, FontSize = text.FontSize });
            else if (child is System.Windows.Controls.Image image && Equals(image.Tag, "AEB_IMAGE") && image.Source is BitmapSource source)
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source)); using var ms = new MemoryStream(); encoder.Save(ms);
                elements.Add(new BoardElement { Type = "image", Content = Convert.ToBase64String(ms.ToArray()), X = double.IsNaN(left) ? 0 : left, Y = double.IsNaN(top) ? 0 : top, Width = image.Width, Height = image.Height });
            }
        }
        return elements;
    }

    private void RestoreElements(List<BoardElement>? elements)
    {
        Board.Children.Clear();
        if (elements == null) return;
        foreach (var item in elements)
        {
            UIElement? child = null;
            if (item.Type == "text") child = new TextBox { Text = item.Content, FontSize = item.FontSize <= 0 ? 24 : item.FontSize, Width = item.Width, Height = item.Height, Foreground = new SolidColorBrush(SelectedColor()), Background = Brushes.Transparent, BorderBrush = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Padding = new Thickness(5), Tag = "AEB_TEXT" };
            else if (item.Type == "image")
            {
                var bytes = Convert.FromBase64String(item.Content); var bitmap = new BitmapImage(); using var ms = new MemoryStream(bytes); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = ms; bitmap.EndInit(); bitmap.Freeze();
                child = new System.Windows.Controls.Image { Source = bitmap, Width = item.Width, Height = item.Height, Stretch = Stretch.Uniform, Tag = "AEB_IMAGE" };
            }
            if (child != null) { InkCanvas.SetLeft(child, item.X); InkCanvas.SetTop(child, item.Y); Board.Children.Add(child); }
        }
    }

    private void Zoom_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_boardScale == null) return;
        _boardScale.ScaleX = ZoomSlider.Value;
        _boardScale.ScaleY = ZoomSlider.Value;
    }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomSlider.Value = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + 0.1);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 0.1);
    private void ZoomReset_Click(object sender, RoutedEventArgs e) => ZoomSlider.Value = 1;

    private void Pen_Click(object sender, RoutedEventArgs e) { _shapeTool = "Pen"; Board.EditingMode = InkCanvasEditingMode.Ink; SetPen(); }
    private void Line_Click(object sender, RoutedEventArgs e) => SetShapeTool("Line");
    private void Rectangle_Click(object sender, RoutedEventArgs e) => SetShapeTool("Rectangle");
    private void Ellipse_Click(object sender, RoutedEventArgs e) => SetShapeTool("Ellipse");
    private void Arrow_Click(object sender, RoutedEventArgs e) => SetShapeTool("Arrow");
    private void SetShapeTool(string tool) { _shapeTool = tool; Board.EditingMode = InkCanvasEditingMode.None; }
    private void Board_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_laserMode)
        {
            _laserDrawing = true; _laserPoints = new StylusPointCollection();
            var p = e.GetPosition(Board); _laserPoints.Add(new StylusPoint(p.X, p.Y));
            Board.CaptureMouse(); e.Handled = true; return;
        }
        if (_shapeTool == "Pen" || _shapeTool == "Highlighter") return;
        _shapeStart=e.GetPosition(Board); _drawingShape=true; Board.CaptureMouse(); e.Handled=true;
    }

    private void Board_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_laserDrawing || _laserPoints == null || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(Board); _laserPoints.Add(new StylusPoint(p.X, p.Y));
    }
    private void Board_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_laserDrawing && _laserPoints != null)
        {
            var points = _laserPoints; _laserPoints = null; _laserDrawing = false; Board.ReleaseMouseCapture();
            if (points.Count > 1)
            {
                var laser = new Stroke(points, new DrawingAttributes { Color = _laserColor, Width = 5, Height = 5, FitToCurve = true, IgnorePressure = true });
                Board.Strokes.Add(laser); _laserStrokes[laser] = DateTime.UtcNow.AddSeconds(_laserDurationSeconds);
            }
            e.Handled = true; return;
        }
        if (!_drawingShape) return;
        Point end=e.GetPosition(Board); _drawingShape=false; Board.ReleaseMouseCapture();
        var shapePoints=CreateShapePoints(_shapeStart,end,_shapeTool);
        if(shapePoints.Count>1){SaveUndo(); Board.Strokes.Add(new Stroke(shapePoints,new DrawingAttributes{Color=SelectedColor(),Width=WidthSlider.Value,Height=WidthSlider.Value,FitToCurve=true,IgnorePressure=true}));}
        e.Handled=true;
    }
    private static StylusPointCollection CreateShapePoints(Point start,Point end,string tool)
    {
        var points=new StylusPointCollection(); void Add(Point p)=>points.Add(new StylusPoint(p.X,p.Y));
        if(tool=="Line"){Add(start);Add(end);}
        else if(tool=="Rectangle"){Add(start);Add(new Point(end.X,start.Y));Add(end);Add(new Point(start.X,end.Y));Add(start);}
        else if(tool=="Ellipse"){double cx=(start.X+end.X)/2,cy=(start.Y+end.Y)/2,rx=Math.Abs(end.X-start.X)/2,ry=Math.Abs(end.Y-start.Y)/2;for(int i=0;i<=64;i++){double a=2*Math.PI*i/64;Add(new Point(cx+rx*Math.Cos(a),cy+ry*Math.Sin(a)));}}
        else if(tool=="Arrow"){double dx=end.X-start.X,dy=end.Y-start.Y,len=Math.Sqrt(dx*dx+dy*dy);if(len<1)return points;double ux=dx/len,uy=dy/len,h=Math.Min(22,len*.35);Add(start);Add(end);Add(new Point(end.X-h*ux-h*.55*uy,end.Y-h*uy+h*.55*ux));Add(end);Add(new Point(end.X-h*ux+h*.55*uy,end.Y-h*uy-h*.55*ux));}
        return points;
    }
    private void Window_PreviewKeyDown(object sender,System.Windows.Input.KeyEventArgs e)
    {
        if(System.Windows.Input.Keyboard.Modifiers==System.Windows.Input.ModifierKeys.Control&&e.Key==System.Windows.Input.Key.Z){Undo_Click(this,new RoutedEventArgs());e.Handled=true;}
        else if(System.Windows.Input.Keyboard.Modifiers==System.Windows.Input.ModifierKeys.Control&&e.Key==System.Windows.Input.Key.Y){Redo_Click(this,new RoutedEventArgs());e.Handled=true;}
        else if(e.Key==System.Windows.Input.Key.F5){Presentation_Click(this,new RoutedEventArgs());e.Handled=true;}
        else if(e.Key==System.Windows.Input.Key.Escape){if(_presentationMode){Presentation_Click(this,new RoutedEventArgs());}else{_shapeTool="Pen";Board.EditingMode=InkCanvasEditingMode.Ink;SetPen();}}
    }
    private void Highlighter_Click(object sender, RoutedEventArgs e)
    {
        _shapeTool = "Highlighter";
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
        Brush brush = mode == "Blackboard" ? new SolidColorBrush(Color.FromRgb(28, 48, 43)) : mode == "Cream" ? new SolidColorBrush(Color.FromRgb(250, 244, 225)) : Brushes.White;
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
        _pages.Clear(); _pages.Add(new StrokeCollection()); _pageIndex = 0; Board.Strokes = _pages[0]; _undo.Clear(); _redo.Clear(); _currentFile = ""; _pdfBytes = null; _pdfPageCount = 0; _pdfAnnotations.Clear(); _pdfPageElements.Clear(); PdfThumbnails.Items.Clear(); PdfPageLabel.Text = "No PDF"; Board.Children.Clear(); Board.Background = Brushes.White; ApplyBackground(); UpdatePageLabel();
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        RemoveLaserStrokes();
        var dialog = new SaveFileDialog { Filter = "Aman Edge Board (*.aeb)|*.aeb", DefaultExt = ".aeb", FileName = string.IsNullOrWhiteSpace(_currentFile) ? "Lesson.aeb" : Path.GetFileName(_currentFile) };
        if (dialog.ShowDialog() != true) return;
        _pages[_pageIndex] = Board.Strokes;
        if (_pdfBytes != null) StoreCurrentPdfPage();
        var data = _pages.Select(p => { using var ms = new MemoryStream(); p.Save(ms); return Convert.ToBase64String(ms.ToArray()); }).ToArray();
        var annotationData = _pdfAnnotations.ToDictionary(k => k.Key, v => { using var ms = new MemoryStream(); v.Value.Save(ms); return Convert.ToBase64String(ms.ToArray()); });
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(new BoardDocument { Version = 4, Pages = data, PdfBytes = _pdfBytes == null ? null : Convert.ToBase64String(_pdfBytes), PdfPageIndex = _pdfPageIndex, PdfFileName = _pdfFileName, Elements = CaptureElements(), PdfAnnotations = annotationData, PdfPageElements = _pdfPageElements }));
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
            _pageIndex = 0; Board.Strokes = _pages[0]; _undo.Clear(); _redo.Clear(); _currentFile = dialog.FileName; UpdatePageLabel(); RestoreElements(doc.Elements);
            _pdfBytes = string.IsNullOrEmpty(doc.PdfBytes) ? null : Convert.FromBase64String(doc.PdfBytes);
            _pdfFileName = doc.PdfFileName; _pdfPageIndex = doc.PdfPageIndex;
            _pdfAnnotations.Clear(); _pdfPageElements.Clear();
            if (doc.PdfAnnotations != null) foreach (var entry in doc.PdfAnnotations) { using var ms = new MemoryStream(Convert.FromBase64String(entry.Value)); _pdfAnnotations[entry.Key] = new StrokeCollection(ms); }
            if (doc.PdfPageElements != null) foreach (var entry in doc.PdfPageElements) _pdfPageElements[entry.Key] = entry.Value;
            if (_pdfBytes != null) { using var pdf = DocLib.Instance.GetDocReader(_pdfBytes, new PageDimensions(1.5)); _pdfPageCount = pdf.GetPageCount(); _pdfPageIndex = Math.Clamp(_pdfPageIndex, 0, Math.Max(0, _pdfPageCount - 1)); BuildPdfThumbnails(); RenderPdfPage(); }
            else { _pdfPageCount = 0; _pdfAnnotations.Clear(); _pdfPageElements.Clear(); PdfThumbnails.Items.Clear(); PdfPageLabel.Text = "No PDF"; Board.Background = Brushes.White; ApplyBackground(); }
        }
        catch (Exception ex) { MessageBox.Show($"Could not open file: {ex.Message}", "Open error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        RemoveLaserStrokes();
        var dialog = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", DefaultExt = ".png", FileName = $"Board-Page-{_pageIndex + 1}.png" };
        if (dialog.ShowDialog() != true) return;
        var size = new Size(Math.Max(1, Board.ActualWidth), Math.Max(1, Board.ActualHeight));
        Board.Measure(size); Board.Arrange(new Rect(size));
        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(Board);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(dialog.FileName); encoder.Save(stream);
    }
    private sealed class BoardDocument { public int Version { get; set; } = 4; public string[] Pages { get; set; } = Array.Empty<string>(); public string? PdfBytes { get; set; } public int PdfPageIndex { get; set; } public string? PdfFileName { get; set; } public List<BoardElement>? Elements { get; set; } public Dictionary<int,string>? PdfAnnotations { get; set; } public Dictionary<int,List<BoardElement>>? PdfPageElements { get; set; } }
    private sealed class BoardElement { public string Type { get; set; } = ""; public string Content { get; set; } = ""; public double X { get; set; } public double Y { get; set; } public double Width { get; set; } public double Height { get; set; } public double FontSize { get; set; } = 24; }
}
