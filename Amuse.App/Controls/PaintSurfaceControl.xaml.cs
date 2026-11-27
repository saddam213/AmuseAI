using Amuse.App.Common;
using Amuse.App.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Dialogs;
using TensorStack.WPF.Image;
using TensorStack.WPF.Resources;
using TensorStack.WPF.Services;

namespace Amuse.App.Controls
{
    public partial class PaintSurfaceControl : BaseControl
    {
        private readonly List<Stroke> _canvasStrokedRemoved = [];
        private int _drawingToolSize;
        private DateTime _canvasLastUpdate;
        private DrawingAttributes _drawingAttributes;
        private Color _selectedColor = Colors.Black;
        private InkCanvasEditingMode _canvasEditingMode = InkCanvasEditingMode.Ink;
        private PaintDrawingTool _canvasDrawingTool;
        private bool _isPickerOpen;
        private Color _previousColor = Colors.Red;
        private ObservableCollection<Color> _recentColors;
        private bool _isFitToCurveEnabled;
        private bool _isPressureEnabled = true;
        private double _toolOutlineX;
        private double _toolOutlineY;
        private Visibility _toolOutlineVisibility;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaintSurfaceControl" /> class.
        /// </summary>
        public PaintSurfaceControl()
        {
            DrawingToolSize = 10;
            RecentColors = new ObservableCollection<Color>();
            LoadCanvasCommand = new AsyncRelayCommand(LoadCanvas);
            ClearCanvasCommand = new AsyncRelayCommand(ClearCanvas);
            CopyCanvasCommand = new AsyncRelayCommand(CopyCanvas, CanCopyCanvas);
            PasteCanvasCommand = new AsyncRelayCommand(PasteCanvas);
            SaveCanvasCommand = new AsyncRelayCommand(SaveCanvas, CanSaveCanvas);
            UndoCanvasCommand = new AsyncRelayCommand(UndoCanvas, CanUndoCanvas);
            RedoCanvasCommand = new AsyncRelayCommand(RedoCanvas, CanRedoCanvas);
            FillCanvasCommand = new AsyncRelayCommand(FillCanvas);
            SelectToolCommand = new AsyncRelayCommand<PaintDrawingTool>(SelectDrawingTool);
            InitializeComponent();
            SetRecentColors();
            SelectedColor = Colors.Black;
        }

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(Settings), typeof(PaintSurfaceControl));
        public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(GenerateInputOptions), typeof(PaintSurfaceControl));
        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(ProgressInfo), typeof(PaintSurfaceControl));
        public static readonly DependencyProperty OverlayImageProperty = DependencyProperty.Register(nameof(OverlayImage), typeof(ImageInput), typeof(PaintSurfaceControl));
        public static readonly DependencyProperty OverlayOpacityProperty = DependencyProperty.Register(nameof(OverlayOpacity), typeof(double), typeof(PaintSurfaceControl), new PropertyMetadata(0.3));
        public static readonly DependencyProperty ExecuteCommandProperty = DependencyProperty.Register(nameof(ExecuteCommand), typeof(AsyncRelayCommand), typeof(PaintSurfaceControl));
        public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.Register(nameof(CancelCommand), typeof(AsyncRelayCommand), typeof(PaintSurfaceControl));
        public static readonly DependencyProperty IsGeneratingProperty = DependencyProperty.Register(nameof(IsGenerating), typeof(bool), typeof(PaintSurfaceControl));
        public static readonly DependencyProperty IsControlNetEnabledProperty = DependencyProperty.Register(nameof(IsControlNetEnabled), typeof(bool), typeof(PaintSurfaceControl));
        public AsyncRelayCommand LoadCanvasCommand { get; }
        public AsyncRelayCommand ClearCanvasCommand { get; }
        public AsyncRelayCommand FillCanvasCommand { get; }
        public AsyncRelayCommand CopyCanvasCommand { get; }
        public AsyncRelayCommand PasteCanvasCommand { get; }
        public AsyncRelayCommand SaveCanvasCommand { get; }
        public AsyncRelayCommand UndoCanvasCommand { get; }
        public AsyncRelayCommand RedoCanvasCommand { get; }
        public AsyncRelayCommand<PaintDrawingTool> SelectToolCommand { get; }

        public Settings Settings
        {
            get { return (Settings)GetValue(SettingsProperty); }
            set { SetValue(SettingsProperty, value); }
        }

        public GenerateInputOptions Options
        {
            get { return (GenerateInputOptions)GetValue(OptionsProperty); }
            set { SetValue(OptionsProperty, value); }
        }

        public ProgressInfo Progress
        {
            get { return (ProgressInfo)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        public double OverlayOpacity
        {
            get { return (double)GetValue(OverlayOpacityProperty); }
            set { SetValue(OverlayOpacityProperty, value); }
        }

        public ImageInput OverlayImage
        {
            get { return (ImageInput)GetValue(OverlayImageProperty); }
            set { SetValue(OverlayImageProperty, value); }
        }

        public AsyncRelayCommand ExecuteCommand
        {
            get { return (AsyncRelayCommand)GetValue(ExecuteCommandProperty); }
            set { SetValue(ExecuteCommandProperty, value); }
        }

        public AsyncRelayCommand CancelCommand
        {
            get { return (AsyncRelayCommand)GetValue(CancelCommandProperty); }
            set { SetValue(CancelCommandProperty, value); }
        }

        public bool IsGenerating
        {
            get { return (bool)GetValue(IsGeneratingProperty); }
            set { SetValue(IsGeneratingProperty, value); }
        }

        public bool IsControlNetEnabled
        {
            get { return (bool)GetValue(IsControlNetEnabledProperty); }
            set { SetValue(IsControlNetEnabledProperty, value); }
        }

        public bool IsFitToCurveEnabled
        {
            get { return _isFitToCurveEnabled; }
            set { _isFitToCurveEnabled = value; NotifyPropertyChanged(); UpdateBrushAttributes(); }
        }

        public bool IsPressureEnabled
        {
            get { return _isPressureEnabled; }
            set { _isPressureEnabled = value; NotifyPropertyChanged(); UpdateBrushAttributes(); }
        }

        public double ToolOutlineX
        {
            get { return _toolOutlineX; }
            set { _toolOutlineX = value; NotifyPropertyChanged(); }
        }

        public double ToolOutlineY
        {
            get { return _toolOutlineY; }
            set { _toolOutlineY = value; NotifyPropertyChanged(); }
        }

        public Visibility ToolOutlineVisibility
        {
            get { return _toolOutlineVisibility; }
            set { _toolOutlineVisibility = value; NotifyPropertyChanged(); }
        }

        public InkCanvasEditingMode CanvasEditingMode
        {
            get { return _canvasEditingMode; }
            set { _canvasEditingMode = value; NotifyPropertyChanged(); }
        }

        public DrawingAttributes BrushAttributes
        {
            get { return _drawingAttributes; }
            set { _drawingAttributes = value; NotifyPropertyChanged(); }
        }

        public int DrawingToolSize
        {
            get { return _drawingToolSize; }
            set
            {
                _drawingToolSize = value;
                NotifyPropertyChanged();
                UpdateBrushAttributes();
            }
        }

        public Color SelectedColor
        {
            get { return _selectedColor; }
            set
            {
                _selectedColor = value;
                NotifyPropertyChanged();
                UpdateBrushAttributes();
            }
        }

        public PaintDrawingTool CanvasDrawingTool
        {
            get { return _canvasDrawingTool; }
            set { _canvasDrawingTool = value; NotifyPropertyChanged(); }
        }

        public ObservableCollection<Color> RecentColors
        {
            get { return _recentColors; }
            set { _recentColors = value; NotifyPropertyChanged(); }
        }

        public bool IsPickerOpen
        {
            get { return _isPickerOpen; }
            set
            {
                _isPickerOpen = value;
                if (!_isPickerOpen && _previousColor != SelectedColor)
                {
                    AddRecentColor(_previousColor);
                    _previousColor = SelectedColor;
                }
                NotifyPropertyChanged();
            }
        }


        /// <summary>
        /// Gets the input image.
        /// </summary>
        public ImageInput GetInputImage()
        {
            var surfaceImage = CreateBitmap();
            return new ImageInput(surfaceImage);
        }


        /// <summary>
        /// Loads the image.
        /// </summary>
        /// <returns></returns>
        private Task LoadCanvas()
        {
            ShowCropImageDialog();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Clears the image.
        /// </summary>
        /// <returns></returns>
        private Task ClearCanvas()
        {
            PaintCanvas.Strokes.Clear();
            PaintCanvas.Background = new SolidColorBrush(Colors.White);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Fills the canvas with the SelectedColor.
        /// </summary>
        /// <returns></returns>
        private Task FillCanvas()
        {
            PaintCanvas.Background = new SolidColorBrush(SelectedColor);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Updates the brush attributes.
        /// </summary>
        private void UpdateBrushAttributes()
        {
            var previousShape = BrushAttributes?.StylusTip ?? StylusTip.Ellipse;
            if (CanvasDrawingTool == PaintDrawingTool.RoundBrush)
                previousShape = StylusTip.Ellipse;
            if (CanvasDrawingTool == PaintDrawingTool.SquareBrush)
                previousShape = StylusTip.Rectangle;

            BrushAttributes = new DrawingAttributes
            {
                Color = _selectedColor,
                Height = _drawingToolSize,
                Width = _drawingToolSize,
                IgnorePressure = !_isPressureEnabled,
                FitToCurve = _isFitToCurveEnabled,
                StylusTip = previousShape
            };

            if (CanvasDrawingTool == PaintDrawingTool.Highlight)
                BrushAttributes.Color = Color.FromArgb(128, BrushAttributes.Color.R, BrushAttributes.Color.G, BrushAttributes.Color.B);

            CanvasEditingMode = CanvasDrawingTool != PaintDrawingTool.Eraser
                ? InkCanvasEditingMode.Ink
                : InkCanvasEditingMode.EraseByPoint;
        }


        /// <summary>
        /// Selects the drawing tool.
        /// </summary>
        /// <param name="selectedTool">The selected tool.</param>
        /// <returns></returns>
        private Task SelectDrawingTool(PaintDrawingTool selectedTool)
        {
            CanvasDrawingTool = selectedTool;
            UpdateBrushAttributes();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Shows the crop image dialog.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="sourceFile">The source file.</param>
        private async void ShowCropImageDialog(BitmapSource source = null)
        {
            var cropImageDialog = DialogService.GetDialog<CropImageDialog>();
            if (!await cropImageDialog.ShowDialogAsync(source, new CropImageOptions { Width = Options.Width, Height = Options.Height }))
                return;

            PaintCanvas.Background = new ImageBrush(cropImageDialog.GetImageResult());
        }


        /// <summary>
        /// Copies the image.
        /// </summary>
        /// <returns></returns>
        private Task CopyCanvas()
        {
            Clipboard.SetImage(CreateBitmap());
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether this canvas can be copied
        /// </summary>
        /// <returns>
        ///   <c>true</c> if this instance can copy canvas; otherwise, <c>false</c>.
        /// </returns>
        private bool CanCopyCanvas()
        {
            return false;
        }


        /// <summary>
        /// Paste the image.
        /// </summary>
        /// <returns></returns>
        private async Task PasteCanvas()
        {
            if (Clipboard.ContainsImage())
            {
                var image = await LoadImageAsync(initialImage: Clipboard.GetImage());
                if (image != null)
                    PaintCanvas.Background = new ImageBrush(image.Image);
            }
            else if (Clipboard.ContainsFileDropList())
            {
                var imageFilename = Clipboard.GetFileDropList()
                    .OfType<string>()
                    .FirstOrDefault();
                var image = await LoadImageAsync(imageFilename);
                if (image != null)
                    PaintCanvas.Background = new ImageBrush(image.Image);
            }
        }


        /// <summary>
        /// Saves the canvas.
        /// </summary>
        private async Task SaveCanvas()
        {
            var canvasImage = GetInputImage();
            var saveFilename = await DialogService.SaveFileAsync(Default.SaveImage, Default.Image, filter: $"{Default.ImageFiles}|*.png", defualtExt: "png");
            if (!string.IsNullOrEmpty(saveFilename))
            {
                await canvasImage.SaveAsync(saveFilename);
            }
        }


        /// <summary>
        /// Determines whether this canvas can be saved
        /// </summary>
        private bool CanSaveCanvas()
        {
            return true;
        }


        /// <summary>
        /// Undo the last canvas stroke
        /// </summary>
        /// <returns></returns>
        private Task UndoCanvas()
        {
            if (PaintCanvas.Strokes.Count == 0)
                return Task.CompletedTask;

            var lastStroke = PaintCanvas.Strokes.Last();
            if (PaintCanvas.Strokes.Remove(lastStroke))
            {
                _canvasStrokedRemoved.Add(lastStroke);
            }
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether this instance can undo last stroke.
        /// </summary>
        private bool CanUndoCanvas()
        {
            return PaintCanvas.Strokes.Count > 0;
        }


        /// <summary>
        /// Redo the last canvas stroke
        /// </summary>
        /// <returns></returns>
        private Task RedoCanvas()
        {
            if (_canvasStrokedRemoved.Count == 0)
                return Task.CompletedTask;

            var lastStroke = _canvasStrokedRemoved.Last();
            if (_canvasStrokedRemoved.Remove(lastStroke))
            {
                PaintCanvas.Strokes.Add(lastStroke);
            }
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether this instance can redo last canvas stroke.
        /// </summary>
        private bool CanRedoCanvas()
        {
            return _canvasStrokedRemoved.Count > 0;
        }


        /// <summary>
        /// Sets the recent colors.
        /// </summary>
        private void SetRecentColors()
        {
            RecentColors.Add(Colors.Red);
            RecentColors.Add(Colors.Green);
            RecentColors.Add(Colors.Blue);
            RecentColors.Add(Colors.Gray);
            RecentColors.Add(Colors.Yellow);

            RecentColors.Add(Colors.Orange);
            RecentColors.Add(Colors.Brown);
            RecentColors.Add(Colors.Fuchsia);
            RecentColors.Add(Colors.Black);
            RecentColors.Add(Colors.White);

            RecentColors.Add(Colors.Purple);
            RecentColors.Add(Colors.Crimson);
            RecentColors.Add(Colors.Cyan);
            RecentColors.Add(Colors.Magenta);
            RecentColors.Add(Colors.Lime);
            RecentColors.Add(Colors.HotPink);
        }


        /// <summary>
        /// Adds the color to the recent list.
        /// </summary>
        /// <param name="color">The color.</param>
        private void AddRecentColor(Color color)
        {
            if (RecentColors.IsNullOrEmpty())
                return;

            if (RecentColors.Contains(color))
            {
                RecentColors.Move(RecentColors.IndexOf(color), 0);
                return;
            }

            RecentColors.RemoveAt(RecentColors.Count - 1);
            RecentColors.Add(color);
        }


        /// <summary>
        /// Handles the MouseLeftButtonDown event of the PaintCanvas control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Input.MouseButtonEventArgs"/> instance containing the event data.</param>
        private void PaintCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToolOutlineVisibility = Visibility.Hidden;
            _canvasStrokedRemoved.Clear();
        }


        /// <summary>
        /// Handles the MouseLeftButtonUp event of the PaintCanvas control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void PaintCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ToolOutlineVisibility = Visibility.Visible;
            Focus();
        }


        /// <summary>
        /// Handles the PreviewMouseMove event of the PaintCanvas control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseEventArgs"/> instance containing the event data.</param>
        private void PaintCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (DateTime.Now > _canvasLastUpdate)
                {
                    _canvasLastUpdate = DateTime.MaxValue;
                    _canvasLastUpdate = DateTime.Now.AddMilliseconds(200);
                }
            }
            else
            {
                RenderToolOutline(e);
            }
        }


        /// <summary>
        /// Renders the tool outline.
        /// </summary>
        /// <param name="e">The <see cref="MouseEventArgs"/> instance containing the event data.</param>
        private void RenderToolOutline(MouseEventArgs e)
        {
            var pos = e.GetPosition(PaintCanvasToolSurface);
            int posX = (int)(pos.X - (DrawingToolSize / 2d));
            int posY = (int)(pos.Y - (DrawingToolSize / 2d));
            if (posX != _toolOutlineX)
                ToolOutlineX = posX;
            if (posY != _toolOutlineY)
                ToolOutlineY = posY;
        }


        /// <summary>
        /// Handles the OnMouseWheel event of the PaintCanvas control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseWheelEventArgs"/> instance containing the event data.</param>
        private void PaintCanvas_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            DrawingToolSize = e.Delta > 0
                ? Math.Min(100, DrawingToolSize + 1)
                : Math.Max(1, DrawingToolSize - 1);

            RenderToolOutline(e);
        }


        /// <summary>
        /// Invoked when an unhandled <see cref="E:System.Windows.Input.Mouse.MouseEnter" /> attached event is raised on this element. Implement this method to add class handling for this event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.Windows.Input.MouseEventArgs" /> that contains the event data.</param>
        protected override void OnMouseEnter(MouseEventArgs e)
        {
            Focus();
            base.OnMouseEnter(e);
        }


        /// <summary>
        /// Invoked when an unhandled <see cref="E:System.Windows.Input.Mouse.MouseLeave" /> attached event is raised on this element. Implement this method to add class handling for this event.
        /// </summary>
        /// <param name="e">The <see cref="T:System.Windows.Input.MouseEventArgs" /> that contains the event data.</param>
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            Keyboard.ClearFocus();
            base.OnMouseLeave(e);
        }


        /// <summary>
        /// Load image as an image from file
        /// </summary>
        /// <param name="initialFilename">The initial filename.</param>
        /// <param name="initialImage">The initial image.</param>
        /// <returns>ImageInput</returns>
        protected virtual async Task<ImageInput> LoadImageAsync(string initialFilename = null, BitmapSource initialImage = null)
        {
            if (Options.Width > 0 && Options.Height > 0)
            {
                if (!string.IsNullOrEmpty(initialFilename))
                    initialImage = await ImageService.LoadFromFileAsync(initialFilename);

                if (initialImage?.Width == Options.Width && initialImage?.Height == Options.Height)
                    return new ImageInput(initialImage);

                var loadImageDialog = DialogService.GetDialog<CropImageDialog>();
                if (await loadImageDialog.ShowDialogAsync(initialImage, new CropImageOptions { Width = Options.Width, Height = Options.Height }))
                {
                    return new ImageInput(loadImageDialog.GetImageResult());
                }
            }
            else if (initialImage is not null)
            {
                return new ImageInput(initialImage);
            }
            else
            {
                var imageFilename = initialFilename ?? await DialogService.OpenFileAsync(Default.OpenImage, filter: TensorStack.WPF.Common.ImageFileFilter);
                if (!string.IsNullOrEmpty(imageFilename))
                {
                    return new ImageInput(imageFilename);
                }
            }
            return null;
        }


        /// <summary>
        /// On Drop
        /// </summary>
        /// <param name="e">The <see cref="T:System.Windows.DragEventArgs" /> that contains the event data.</param>
        protected override async void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);

            Progress?.Indeterminate();

            var image = await GetDropImage(e);
            if (image != null)
                PaintCanvas.Background = new ImageBrush(image.Image);

            Progress?.Clear();
        }


        /// <summary>
        /// Gets the drop image.
        /// </summary>
        /// <param name="e">The <see cref="DragEventArgs"/> instance containing the event data.</param>
        protected async Task<ImageInput> GetDropImage(DragEventArgs e)
        {
            var fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (!fileNames.IsNullOrEmpty())
            {
                return await LoadImageAsync(fileNames.FirstOrDefault());
            }
            else
            {
                var bitmapImage = e.Data.GetData(typeof(BitmapSource)) as BitmapSource;
                if (bitmapImage is not null)
                {
                    return await LoadImageAsync(initialImage: bitmapImage);
                }
            }
            return null;
        }


        /// <summary>
        /// Creates the bitmap.
        /// </summary>
        /// <returns>RenderTargetBitmap.</returns>
        private RenderTargetBitmap CreateBitmap()
        {
            var renderBitmap = new RenderTargetBitmap(Options.Width, Options.Height, 96, 96, PixelFormats.Pbgra32);
            PaintCanvas.Measure(new Size(Options.Width, Options.Height));
            PaintCanvas.Arrange(new Rect(new Size(Options.Width, Options.Height)));
            PaintCanvas.UpdateLayout();
            renderBitmap.Render(PaintCanvas);
            return renderBitmap;
        }
    }

    public enum PaintDrawingTool
    {
        [Display(Name = "PaintDrawingTool0", ResourceType = typeof(AppEnums))]
        RoundBrush = 0,

        [Display(Name = "PaintDrawingTool1", ResourceType = typeof(AppEnums))]
        SquareBrush = 1,

        [Display(Name = "PaintDrawingTool2", ResourceType = typeof(AppEnums))]
        Highlight = 2,

        [Display(Name = "PaintDrawingTool3", ResourceType = typeof(AppEnums))]
        Eraser = 3
    }
}
