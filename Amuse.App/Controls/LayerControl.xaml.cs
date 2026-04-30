using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TensorStack.Common;
using TensorStack.Image;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;


namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for LayerControl.xaml
    /// </summary>
    public partial class LayerControl : BaseControl
    {
        private int _frameWidth = 512;
        private int _frameHeight = 512;
        private Layer _selectedLayer;
        private ResizeAdorner _moveResizeAdorner;
        private Color _canvasColor = Colors.White;

        public LayerControl()
        {
            Layers = new ObservableCollection<Layer>();
            Layers.CollectionChanged += Layers_CollectionChanged;
            SaveCommand = new RelayCommand(Save, CanSave);
            ClearCommand = new AsyncRelayCommand(Clear);
            LayerUpCommand = new RelayCommand<Layer>(MoveLayerUp, CanMoveLayerUp);
            LayerDownCommand = new RelayCommand<Layer>(MoveLayerDown, CanMoveLayerDown);
            LayerRemoveCommand = new RelayCommand<Layer>(RemoveLayer);
            LayerCopyCommand = new RelayCommand<Layer>(CopyLayer);
            AddTextCommand = new RelayCommand(AddTextLayer);
            AddImageCommand = new AsyncRelayCommand(AddImageLayer);
            InitializeComponent();
        }

        public event EventHandler<BitmapSource> ImageGenerated;
        public ObservableCollection<Layer> Layers { get; set; }
        public RelayCommand SaveCommand { get; }
        public AsyncRelayCommand ClearCommand { get; }
        public RelayCommand<Layer> LayerUpCommand { get; }
        public RelayCommand<Layer> LayerDownCommand { get; set; }
        public RelayCommand<Layer> LayerRemoveCommand { get; set; }
        public RelayCommand<Layer> LayerCopyCommand { get; }
        public RelayCommand AddTextCommand { get; }
        public AsyncRelayCommand AddImageCommand { get; }

        public int FrameWidth
        {
            get { return _frameWidth; }
            set { SetProperty(ref _frameWidth, value); }
        }

        public int FrameHeight
        {
            get { return _frameHeight; }
            set { SetProperty(ref _frameHeight, value); }
        }

        public Color CanvasColor
        {
            get { return _canvasColor; }
            set
            {
                if (SetProperty(ref _canvasColor, value))
                {
                    Surface.Background = new SolidColorBrush(_canvasColor);
                }
            }
        }

        public Layer SelectedLayer
        {
            get { return _selectedLayer; }
            set
            {
                if (SetProperty(ref _selectedLayer, value))
                {
                    if (_selectedLayer != null)
                    {
                        if (_moveResizeAdorner != null)
                        {
                            AdornerLayer.GetAdornerLayer(_moveResizeAdorner.AdornedElement)?.Remove(_moveResizeAdorner);
                        }

                        _moveResizeAdorner = new ResizeAdorner(_selectedLayer.Element, _selectedLayer.CanResize, _selectedLayer.UpdateLayer);
                        AdornerLayer.GetAdornerLayer(_selectedLayer.Element)?.Add(_moveResizeAdorner);
                    }
                    else
                    {
                        _moveResizeAdorner = null;
                    }
                }
            }
        }


        private void Layers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                if (e.NewItems[0] is ImageLayer imageLayer)
                {
                    Canvas.SetLeft(imageLayer.Element, 0);
                    Canvas.SetTop(imageLayer.Element, 0);
                    Canvas.SetZIndex(imageLayer.Element, Layers.Count);
                    Surface.Children.Add(imageLayer.Element);

                    if (Layers.Count == 1)
                    {
                        FrameWidth = imageLayer.Image.PixelWidth;
                        FrameHeight = imageLayer.Image.PixelHeight;
                    }
                    else
                    {
                        if (imageLayer.Element.Width > FrameWidth)
                            imageLayer.Element.Width = FrameWidth;

                        if (imageLayer.Element.Height > FrameHeight)
                            imageLayer.Element.Height = FrameHeight;
                    }

                    SelectedLayer = imageLayer;
                }
                else if (e.NewItems[0] is TextLayer textLayer)
                {

                    Canvas.SetLeft(textLayer.Element, 0);
                    Canvas.SetTop(textLayer.Element, 0);
                    Canvas.SetZIndex(textLayer.Element, Layers.Count);
                    Surface.Children.Add(textLayer.Element);
                    SelectedLayer = textLayer;
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                var layer = e.OldItems[0] as Layer;
                Surface.Children.Remove(layer.Element);
                SelectedLayer = Layers.FirstOrDefault();
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {

                Surface.Children.Clear();
                SelectedLayer = Layers.FirstOrDefault();
            }
            else if (e.Action == NotifyCollectionChangedAction.Move)
            {
                foreach (var layer in Layers.Index())
                {
                    var zindex = (Layers.Count - layer.Index) - 1;
                    Canvas.SetZIndex(layer.Item.Element, zindex);
                }
            }

            CommandManager.InvalidateRequerySuggested();
        }


        private void Save()
        {
            var canvasImage = CreateBitmap();
            ImageGenerated?.Invoke(this, canvasImage);
        }


        private async Task Clear()
        {
            if (await DialogService.ShowMessageAsync("Clear Layers", "Are you sure you would like to clear all layers?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Question, TensorStack.WPF.Dialogs.MessageBoxStyleType.Info))
            {
                Layers.Clear();
            }
        }


        private bool CanSave()
        {
            return Layers.Count > 0;
        }


        private void MoveLayerUp(Layer layer)
        {
            var index = Layers.IndexOf(layer);
            Layers.Move(index, Math.Max(0, index - 1));
        }


        private bool CanMoveLayerUp(Layer layer)
        {
            var index = Layers.IndexOf(layer);
            return index != 0;
        }


        private void MoveLayerDown(Layer layer)
        {
            var index = Layers.IndexOf(layer);
            Layers.Move(index, Math.Min(Layers.Count - 1, index + 1));
        }


        private bool CanMoveLayerDown(Layer layer)
        {
            var index = Layers.IndexOf(layer);
            return index != Layers.Count - 1;
        }


        private void RemoveLayer(Layer layer)
        {
            Layers.Remove(layer);
        }

        private void CopyLayer(Layer layer)
        {
            if (layer is ImageLayer imageLayer)
            {
                var newLayer = new ImageLayer($"{imageLayer.Name} copy", imageLayer.Image);
                newLayer.Stretch = imageLayer.Stretch;
                newLayer.Width = imageLayer.Width;
                newLayer.Height = imageLayer.Height;
                newLayer.PositionX = imageLayer.PositionX;
                newLayer.PositionY = imageLayer.PositionY;
                Layers.Insert(0, newLayer);
            }
            else if (layer is TextLayer textLayer)
            {
                var newLayer = new TextLayer($"{textLayer.Name} copy", textLayer.Text);
                newLayer.PositionX = textLayer.PositionX;
                newLayer.PositionY = textLayer.PositionY;
                newLayer.FontSize = textLayer.FontSize;
                newLayer.Color = textLayer.Color;
                newLayer.FontWeight = textLayer.FontWeight;
                newLayer.FontFamily = textLayer.FontFamily;
                newLayer.FontStyle = textLayer.FontStyle;
                Layers.Insert(0, newLayer);
            }
        }


        private void AddTextLayer()
        {
            Layers.Insert(0, new TextLayer($"Layer {Layers.Count + 1}", "Hello World"));
        }


        private async Task AddImageLayer()
        {
            var imageFile = await DialogService.OpenFileAsync("Open Image", filter: "Image Files|*.bmp;*.jpg;*.jpeg;*.png;*.gif;*.tif;*.tiff|All Files|*.*");
            if (string.IsNullOrEmpty(imageFile))
                return;

            var bitmapSource = ImageService.LoadFromFile(imageFile);
            if (bitmapSource == null)
                return;

            Layers.Insert(0, new ImageLayer($"Layer {Layers.Count + 1}", bitmapSource));
        }


        protected override void OnDrop(DragEventArgs e)
        {
            var fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);

            BitmapSource bitmapSource;
            if (!fileNames.IsNullOrEmpty())
            {
                bitmapSource = ImageService.LoadFromFile(fileNames.First());
            }
            else
            {
                bitmapSource = e.Data.GetData(typeof(BitmapSource)) as BitmapSource;
            }


            if (bitmapSource != null)
            {
                Layers.Insert(0, new ImageLayer($"Layer {Layers.Count + 1}", bitmapSource));
            }
            base.OnDrop(e);
        }


        protected override void OnDragEnter(DragEventArgs e)
        {
            if (_moveResizeAdorner != null)
                _moveResizeAdorner.IsHitTestVisible = false;
            base.OnDragEnter(e);
        }


        protected override void OnDragLeave(DragEventArgs e)
        {
            if (_moveResizeAdorner != null)
                _moveResizeAdorner.IsHitTestVisible = true;
            base.OnDragLeave(e);
        }


        private BitmapSource CreateBitmap()
        {
            var renderBitmap = new RenderTargetBitmap(FrameWidth, FrameHeight, 96, 96, PixelFormats.Pbgra32);
            Surface.Measure(new Size(FrameWidth, FrameHeight));
            Surface.Arrange(new Rect(new Size(FrameWidth, FrameHeight)));
            Surface.UpdateLayout();
            renderBitmap.Render(Surface);
            return renderBitmap;
        }
    }

    public class ImageLayer : Layer
    {
        private Stretch _stretch = Stretch.Uniform;

        public ImageLayer(string name, BitmapSource bitmapSource) : base()
        {
            Name = name;
            CanResize = true;
            Image = bitmapSource;
            Element.Width = bitmapSource.PixelWidth;
            Element.Height = bitmapSource.PixelHeight;
            Element.Content = new System.Windows.Controls.Image
            {
                Stretch = _stretch,
                Source = bitmapSource
            };
            Initialize();
            UpdateLayer();
        }

        public BitmapSource Image { get; }


        public Stretch Stretch
        {
            get { return _stretch; }
            set
            {
                if (SetProperty(ref _stretch, value))
                    UpdateLayer();
            }
        }


        public override void UpdateLayer()
        {
            base.UpdateLayer();
            if (Element.Content is System.Windows.Controls.Image image)
            {
                image.Stretch = _stretch;
            }
        }
    }

    public class TextLayer : Layer
    {
        private string _text;
        private int _fontSize = 50;
        private Color _color = Colors.Gray;
        private FontStyle _fontStyle = FontStyles.Normal;
        private FontWeight _fontWeight = FontWeights.Normal;
        private FontFamily _fontFamily = FontOptions.FontFamilies.First();

        public TextLayer(string name, string text) : base()
        {
            Name = name;
            Text = text;
            FontSize = _fontSize;
            Element.Content = new TextBlock
            {
                Text = text,
                FontSize = _fontSize,
                Foreground = new SolidColorBrush(_color),
                FontWeight = _fontWeight,
                FontFamily = _fontFamily,
                FontStyle = _fontStyle
            };
            Initialize();
            UpdateLayer();
        }


        public string Text
        {
            get { return _text; }
            set
            {
                if (SetProperty(ref _text, value))
                    UpdateLayer();
            }
        }

        public int FontSize
        {
            get { return _fontSize; }
            set
            {
                if (SetProperty(ref _fontSize, value))
                    UpdateLayer();
            }
        }

        public Color Color
        {
            get { return _color; }
            set
            {
                if (SetProperty(ref _color, value))
                    UpdateLayer();
            }
        }

        public FontStyle FontStyle
        {
            get { return _fontStyle; }
            set
            {
                if (SetProperty(ref _fontStyle, value))
                    UpdateLayer();
            }
        }

        public FontWeight FontWeight
        {
            get { return _fontWeight; }
            set
            {
                if (SetProperty(ref _fontWeight, value))
                    UpdateLayer();
            }
        }

        public FontFamily FontFamily
        {
            get { return _fontFamily; }
            set
            {
                if (SetProperty(ref _fontFamily, value))
                    UpdateLayer();
            }
        }


        public override void UpdateLayer()
        {
            base.UpdateLayer();
            if (Element?.Content is TextBlock textblock)
            {
                textblock.Text = _text;
                textblock.FontSize = _fontSize;
                textblock.Foreground = new SolidColorBrush(_color);
                textblock.FontWeight = _fontWeight;
                textblock.FontFamily = _fontFamily;
                textblock.FontStyle = _fontStyle;

                textblock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Element.Width = textblock.DesiredSize.Width;
                Element.Height = textblock.DesiredSize.Height;
            }
        }
    }

    public class Layer : BaseModel
    {
        private int _positionX;
        private int _positionY;
        private int _rotation = 0;
        private bool _isVisible = true;
        private RotateTransform _rotateTransform;
        private string _name;

        public Layer()
        {
            _rotateTransform = new RotateTransform(0);
            Element = new ContentControl();
            Element.SizeChanged += Element_SizeChanged;
        }


        protected void Initialize()
        {
            var element = Element.Content as FrameworkElement;
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = _rotateTransform;
        }

        public ContentControl Element { get; }
        public bool CanResize { get; set; }

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        public int Width
        {
            get { return (int)Element.Width; }
            set { Element.Width = value; }
        }

        public int Height
        {
            get { return (int)Element.Height; }
            set { Element.Height = value; }
        }

        public int PositionX
        {
            get { return _positionX; }
            set
            {
                if (SetProperty(ref _positionX, value))
                    Canvas.SetLeft(Element, _positionX);
            }
        }

        public int PositionY
        {
            get { return _positionY; }
            set
            {
                if (SetProperty(ref _positionY, value))
                    Canvas.SetTop(Element, _positionY);
            }
        }

        public int Rotation
        {
            get { return _rotation; }
            set
            {
                if (SetProperty(ref _rotation, value))
                    _rotateTransform.Angle = _rotation;
            }
        }

        public bool IsVisible
        {
            get { return _isVisible; }
            set
            {
                if (SetProperty(ref _isVisible, value))
                {
                    Element.Visibility = _isVisible ? Visibility.Visible : Visibility.Hidden;
                }
            }
        }

        public virtual void UpdateLayer()
        {
            PositionX = (int)Canvas.GetLeft(Element);
            PositionY = (int)Canvas.GetTop(Element);
        }

        private void Element_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            NotifyPropertyChanged(nameof(Width));
            NotifyPropertyChanged(nameof(Height));
        }

    }

    public class ResizeAdorner : Adorner
    {
        private readonly VisualCollection _visuals;
        private readonly Thumb _topLeft, _topRight, _bottomLeft, _bottomRight, _border;
        private Point _startPoint;
        private bool _isDragging;
        private Action _refreshCallback;

        public ResizeAdorner(UIElement adornedElement, bool resize, Action refreshCallback) : base(adornedElement)
        {
            IsHitTestVisible = true;
            MouseLeftButtonDown += MoveAdorner_MouseLeftButtonDown;
            MouseMove += MoveAdorner_MouseMove;
            MouseLeftButtonUp += MoveAdorner_MouseLeftButtonUp;

            _visuals = new VisualCollection(this);
            _topLeft = CreateThumb(Cursors.SizeNWSE);
            _topRight = CreateThumb(Cursors.SizeNESW);
            _bottomLeft = CreateThumb(Cursors.SizeNESW);
            _bottomRight = CreateThumb(Cursors.SizeNWSE);
            _border = CreateBorder();
            _visuals.Add(_border);
            if (resize)
            {
                _topLeft.DragDelta += (s, e) => Resize(e.HorizontalChange, e.VerticalChange, true, true);
                _topRight.DragDelta += (s, e) => Resize(e.HorizontalChange, e.VerticalChange, false, true);
                _bottomLeft.DragDelta += (s, e) => Resize(e.HorizontalChange, e.VerticalChange, true, false);
                _bottomRight.DragDelta += (s, e) => Resize(e.HorizontalChange, e.VerticalChange, false, false);
                _visuals.Add(_topLeft);
                _visuals.Add(_topRight);
                _visuals.Add(_bottomLeft);
                _visuals.Add(_bottomRight);
            }
            _refreshCallback = refreshCallback;
        }

        protected override int VisualChildrenCount => _visuals.Count;
        protected override Visual GetVisualChild(int index) => _visuals[index];


        protected override void OnRender(DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        }


        protected override System.Windows.Size ArrangeOverride(Size finalSize)
        {
            if (AdornedElement is FrameworkElement adorned)
            {
                double offset = -_topLeft.Width / 2;

                _border.Width = finalSize.Width;
                _border.Height = finalSize.Height;
                _border.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));

                _topLeft.Arrange(new Rect(offset, offset, _topLeft.Width, _topLeft.Height));
                _topRight.Arrange(new Rect(adorned.ActualWidth - offset - _topRight.Width, offset, _topRight.Width, _topRight.Height));
                _bottomLeft.Arrange(new Rect(offset, adorned.ActualHeight - offset - _bottomLeft.Height, _bottomLeft.Width, _bottomLeft.Height));
                _bottomRight.Arrange(new Rect(adorned.ActualWidth - offset - _bottomRight.Width, adorned.ActualHeight - offset - _bottomRight.Height, _bottomRight.Width, _bottomRight.Height));
            }
            return finalSize;
        }


        protected override void OnMouseEnter(MouseEventArgs e)
        {
            Cursor = Cursors.SizeAll;
            foreach (var thumb in _visuals.OfType<Thumb>())
            {
                thumb.Visibility = Visibility.Visible;
            }
            base.OnMouseEnter(e);
        }


        protected override void OnMouseLeave(MouseEventArgs e)
        {
            Cursor = null;
            foreach (var thumb in _visuals.OfType<Thumb>())
            {
                thumb.Visibility = Visibility.Hidden;
            }
            base.OnMouseLeave(e);
        }


        private void Resize(double deltaX, double deltaY, bool adjustLeft, bool adjustTop)
        {
            if (AdornedElement is not FrameworkElement element)
                return;

            double newWidth = element.Width + deltaX * (adjustLeft ? -1 : 1);
            double newHeight = element.Height + deltaY * (adjustTop ? -1 : 1);
            newWidth = Math.Max(newWidth, 10);
            newHeight = Math.Max(newHeight, 10);
            if (adjustLeft)
                Canvas.SetLeft(element, Canvas.GetLeft(element) + (element.Width - newWidth));
            if (adjustTop)
                Canvas.SetTop(element, Canvas.GetTop(element) + (element.Height - newHeight));

            element.Width = newWidth;
            element.Height = newHeight;
            InvalidateArrange();
        }


        private void MoveAdorner_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = Mouse.GetPosition(this);
            _isDragging = true;
            CaptureMouse();
            e.Handled = true;
        }


        private void MoveAdorner_MouseMove(object sender, MouseEventArgs e)
        {
            var currentPoint = Mouse.GetPosition(this);
            if (_isDragging && AdornedElement is FrameworkElement element)
            {
                var left = Canvas.GetLeft(element);
                var top = Canvas.GetTop(element);
                var deltaX = _startPoint.X - currentPoint.X;
                var deltaY = _startPoint.Y - currentPoint.Y;
                Canvas.SetLeft(element, left - deltaX);
                Canvas.SetTop(element, top - deltaY);
                _refreshCallback.Invoke();
            }
        }


        private void MoveAdorner_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                e.Handled = true;
                _refreshCallback.Invoke();
            }
        }


        private void Thumb_MouseLeftButtonUp(object sender, MouseEventArgs e)
        {
            _refreshCallback.Invoke();
        }


        private Thumb CreateThumb(Cursor cursor)
        {
            var scale = GetScale();
            var borderSize = 1 * scale;
            var ellipseSize = 10.0 * scale;
            var ellipseFactory = new FrameworkElementFactory(typeof(Ellipse));
            ellipseFactory.SetValue(Shape.FillProperty, Brushes.Red);
            ellipseFactory.SetValue(Shape.StrokeProperty, Brushes.Black);
            ellipseFactory.SetValue(Shape.StrokeThicknessProperty, borderSize);
            ellipseFactory.SetValue(FrameworkElement.WidthProperty, ellipseSize);
            ellipseFactory.SetValue(FrameworkElement.HeightProperty, ellipseSize);

            var thumb = new Thumb
            {
                Width = ellipseSize,
                Height = ellipseSize,
                Cursor = cursor,
                Visibility = Visibility.Hidden,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = ellipseFactory
                }
            };

            thumb.PreviewMouseUp += Thumb_MouseLeftButtonUp;
            return thumb;
        }


        private Thumb CreateBorder()
        {
            var borderSize = GetScale();
            var rectangleFactory = new FrameworkElementFactory(typeof(Rectangle));
            rectangleFactory.SetValue(Shape.StrokeProperty, Brushes.Red);
            rectangleFactory.SetValue(Shape.StrokeThicknessProperty, borderSize);
            var thumb = new Thumb
            {
                IsHitTestVisible = false,
                Visibility = Visibility.Hidden,
                Template = new ControlTemplate(typeof(Thumb))
                {
                    VisualTree = rectangleFactory
                }
            };
            return thumb;
        }


        private double GetScale()
        {
            var scale = 1.0;
            if (AdornedElement is FrameworkElement adorned && adorned.Parent is FrameworkElement parent)
            {
                scale *= Math.Max(parent.Width, parent.Height) / 512;
            }
            return scale;
        }
    }

}