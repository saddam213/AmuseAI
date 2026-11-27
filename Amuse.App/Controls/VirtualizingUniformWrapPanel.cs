using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Amuse.App.Controls
{
    /// <summary>
    /// A virtualizing wrap panel that lays out items in uniform cells, sizing the cell width between MinItemWidth and MaxItemWidth to fill each row.
    /// </summary>
    public sealed class VirtualizingUniformWrapPanel : VirtualizingPanel, IScrollInfo
    {
        private const double LineSize = 40;

        private ScrollViewer _scrollOwner;
        private Size _extent;
        private Size _viewport;
        private Point _offset;
        private double _itemWidth;
        private double _itemHeight;
        private int _columns = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualizingUniformWrapPanel"/> class.
        /// </summary>
        public VirtualizingUniformWrapPanel()
        {
            Background = Brushes.Transparent;
        }

        public static readonly DependencyProperty MinItemWidthProperty =
            DependencyProperty.Register(nameof(MinItemWidth), typeof(double), typeof(VirtualizingUniformWrapPanel), new FrameworkPropertyMetadata(140d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty MaxItemWidthProperty =
            DependencyProperty.Register(nameof(MaxItemWidth), typeof(double), typeof(VirtualizingUniformWrapPanel), new FrameworkPropertyMetadata(200d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double MinItemWidth
        {
            get { return (double)GetValue(MinItemWidthProperty); }
            set { SetValue(MinItemWidthProperty, value); }
        }

        public double MaxItemWidth
        {
            get { return (double)GetValue(MaxItemWidthProperty); }
            set { SetValue(MaxItemWidthProperty, value); }
        }

        public bool CanHorizontallyScroll { get; set; }
        public bool CanVerticallyScroll { get; set; }
        public double ExtentWidth => _extent.Width;
        public double ExtentHeight => _extent.Height;
        public double ViewportWidth => _viewport.Width;
        public double ViewportHeight => _viewport.Height;
        public double HorizontalOffset => _offset.X;
        public double VerticalOffset => _offset.Y;

        public ScrollViewer ScrollOwner
        {
            get { return _scrollOwner; }
            set { _scrollOwner = value; }
        }


        /// <summary>
        /// Measures the visible items, realizing and virtualizing containers as needed.
        /// </summary>
        /// <param name="availableSize">The available size.</param>
        protected override Size MeasureOverride(Size availableSize)
        {
            var children = InternalChildren;
            var generator = ItemContainerGenerator;
            var itemCount = GetItemCount();
            var width = double.IsInfinity(availableSize.Width) ? MaxItemWidth : availableSize.Width;
            var height = double.IsInfinity(availableSize.Height) ? SystemParameters.PrimaryScreenHeight : availableSize.Height;

            _itemWidth = CalculateItemWidth(width, MinItemWidth, MaxItemWidth);
            _columns = Math.Max(1, (int)Math.Floor((width / _itemWidth) + 0.0001));

            if (itemCount == 0)
            {
                CleanUpItems(0, -1);
                UpdateScrollInfo(new Size(width, 0), new Size(width, height));
                return new Size(width, double.IsInfinity(availableSize.Height) ? 0 : height);
            }

            if (_itemHeight <= 0)
                _itemHeight = MeasureItemHeight(0);

            var rowCount = (itemCount + _columns - 1) / _columns;
            var extentHeight = rowCount * _itemHeight;
            var offsetY = Math.Max(0, Math.Min(_offset.Y, extentHeight - height));
            var firstRow = (int)Math.Floor(offsetY / _itemHeight);
            var lastRow = (int)Math.Ceiling((offsetY + height) / _itemHeight);
            var firstIndex = Math.Min(itemCount - 1, firstRow * _columns);
            var lastIndex = Math.Min(itemCount - 1, (lastRow * _columns) + _columns - 1);

            var maxHeight = 0d;
            var startPosition = generator.GeneratorPositionFromIndex(firstIndex);
            var childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;
            using (generator.StartAt(startPosition, GeneratorDirection.Forward, true))
            {
                for (int itemIndex = firstIndex; itemIndex <= lastIndex; itemIndex++, childIndex++)
                {
                    var child = (UIElement)generator.GenerateNext(out var isNewlyRealized);
                    if (child == null)
                        break;

                    if (isNewlyRealized)
                    {
                        if (childIndex >= children.Count)
                            AddInternalChild(child);
                        else
                            InsertInternalChild(childIndex, child);

                        generator.PrepareItemContainer(child);
                    }

                    child.Measure(new Size(_itemWidth, double.PositiveInfinity));
                    maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
                }
            }

            CleanUpItems(firstIndex, lastIndex);

            if (maxHeight > 0 && Math.Abs(maxHeight - _itemHeight) > 0.5)
            {
                _itemHeight = maxHeight;
                extentHeight = rowCount * _itemHeight;
            }

            _offset.Y = Math.Max(0, Math.Min(_offset.Y, extentHeight - height));
            UpdateScrollInfo(new Size(width, extentHeight), new Size(width, height));
            return new Size(width, double.IsInfinity(availableSize.Height) ? extentHeight : height);
        }


        /// <summary>
        /// Arranges the realized containers into their cells.
        /// </summary>
        /// <param name="finalSize">The final size.</param>
        protected override Size ArrangeOverride(Size finalSize)
        {
            var generator = ItemContainerGenerator;
            for (int i = 0; i < InternalChildren.Count; i++)
            {
                var child = InternalChildren[i];
                var itemIndex = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
                var row = itemIndex / _columns;
                var column = itemIndex % _columns;
                child.Arrange(new Rect(column * _itemWidth, (row * _itemHeight) - _offset.Y, _itemWidth, _itemHeight));
            }
            return finalSize;
        }


        /// <summary>
        /// Removes containers for items that were removed, replaced or moved.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="ItemsChangedEventArgs"/> instance containing the event data.</param>
        protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move:
                    RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    if (InternalChildren.Count > 0)
                        RemoveInternalChildRange(0, InternalChildren.Count);
                    break;
            }
            base.OnItemsChanged(sender, args);
        }


        /// <summary>
        /// Scrolls the item at the specified index into view, used by keyboard navigation.
        /// </summary>
        /// <param name="index">The item index.</param>
        protected override void BringIndexIntoView(int index)
        {
            if (index < 0 || index >= GetItemCount() || _itemHeight <= 0)
                return;

            var top = (index / _columns) * _itemHeight;
            if (top < _offset.Y)
                SetVerticalOffset(top);
            else if (top + _itemHeight > _offset.Y + _viewport.Height)
                SetVerticalOffset(top + _itemHeight - _viewport.Height);
        }


        /// <summary>
        /// Scrolls the specified visual into view.
        /// </summary>
        /// <param name="visual">The visual.</param>
        /// <param name="rectangle">The rectangle.</param>
        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            if (visual is UIElement element)
            {
                var childIndex = InternalChildren.IndexOf(element);
                if (childIndex >= 0)
                {
                    var itemIndex = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(childIndex, 0));
                    BringIndexIntoView(itemIndex);
                }
            }
            return rectangle;
        }


        /// <summary>
        /// Scrolls up one line.
        /// </summary>
        public void LineUp()
        {
            SetVerticalOffset(_offset.Y - LineSize);
        }


        /// <summary>
        /// Scrolls down one line.
        /// </summary>
        public void LineDown()
        {
            SetVerticalOffset(_offset.Y + LineSize);
        }


        /// <summary>
        /// Scrolls up one page.
        /// </summary>
        public void PageUp()
        {
            SetVerticalOffset(_offset.Y - _viewport.Height);
        }


        /// <summary>
        /// Scrolls down one page.
        /// </summary>
        public void PageDown()
        {
            SetVerticalOffset(_offset.Y + _viewport.Height);
        }


        /// <summary>
        /// Scrolls up for a mouse wheel notch.
        /// </summary>
        public void MouseWheelUp()
        {
            SetVerticalOffset(_offset.Y - (SystemParameters.WheelScrollLines * LineSize));
        }


        /// <summary>
        /// Scrolls down for a mouse wheel notch.
        /// </summary>
        public void MouseWheelDown()
        {
            SetVerticalOffset(_offset.Y + (SystemParameters.WheelScrollLines * LineSize));
        }


        /// <summary>
        /// Horizontal scrolling is not supported.
        /// </summary>
        public void LineLeft() { }


        /// <summary>
        /// Horizontal scrolling is not supported.
        /// </summary>
        public void LineRight() { }


        /// <summary>
        /// Horizontal scrolling is not supported.
        /// </summary>
        public void PageLeft() { }


        /// <summary>
        /// Horizontal scrolling is not supported.
        /// </summary>
        public void PageRight() { }


        /// <summary>
        /// Horizontal scrolling is not supported.
        /// </summary>
        public void MouseWheelLeft() { }


        /// <summary>
        /// Horizontal scrolling is not supported.
        /// </summary>
        public void MouseWheelRight() { }


        /// <summary>
        /// Horizontal scrolling is not supported.
        /// </summary>
        /// <param name="offset">The offset.</param>
        public void SetHorizontalOffset(double offset) { }


        /// <summary>
        /// Sets the vertical scroll offset.
        /// </summary>
        /// <param name="offset">The offset.</param>
        public void SetVerticalOffset(double offset)
        {
            offset = Math.Max(0, Math.Min(offset, _extent.Height - _viewport.Height));
            if (double.IsNaN(offset) || Math.Abs(offset - _offset.Y) < 0.01)
                return;

            _offset.Y = offset;
            _scrollOwner?.InvalidateScrollInfo();
            InvalidateMeasure();
        }


        /// <summary>
        /// Calculates the cell width that best fills the container between the min and max widths.
        /// </summary>
        /// <param name="containerWidth">Width of the container.</param>
        /// <param name="minWidth">The minimum width.</param>
        /// <param name="maxWidth">The maximum width.</param>
        public static double CalculateItemWidth(double containerWidth, double minWidth, double maxWidth)
        {
            if (minWidth <= 0 || maxWidth <= 0 || containerWidth <= 0)
                return Math.Max(1, containerWidth);

            var maxItemCount = (int)Math.Floor(containerWidth / minWidth);
            var minItemCount = (int)Math.Ceiling(containerWidth / maxWidth);
            if (maxItemCount == 0)
                return containerWidth;

            for (int itemCount = maxItemCount; itemCount >= minItemCount; itemCount--)
            {
                var potentialItemWidth = containerWidth / itemCount;
                if (potentialItemWidth >= minWidth && potentialItemWidth <= maxWidth)
                    return potentialItemWidth;
            }

            if (containerWidth / minItemCount < minWidth)
                return minWidth;

            return maxWidth;
        }


        /// <summary>
        /// Gets the number of items in the owning ItemsControl.
        /// </summary>
        private int GetItemCount()
        {
            var itemsControl = ItemsControl.GetItemsOwner(this);
            return itemsControl?.HasItems == true ? itemsControl.Items.Count : 0;
        }


        /// <summary>
        /// Realizes and measures a single item to estimate the row height.
        /// </summary>
        /// <param name="itemIndex">The item index.</param>
        private double MeasureItemHeight(int itemIndex)
        {
            var generator = ItemContainerGenerator;
            var startPosition = generator.GeneratorPositionFromIndex(itemIndex);
            var childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;
            using (generator.StartAt(startPosition, GeneratorDirection.Forward, true))
            {
                var child = (UIElement)generator.GenerateNext(out var isNewlyRealized);
                if (child == null)
                    return LineSize;

                if (isNewlyRealized)
                {
                    if (childIndex >= InternalChildren.Count)
                        AddInternalChild(child);
                    else
                        InsertInternalChild(childIndex, child);

                    generator.PrepareItemContainer(child);
                }

                child.Measure(new Size(_itemWidth, double.PositiveInfinity));
                return Math.Max(1, child.DesiredSize.Height);
            }
        }


        /// <summary>
        /// Removes containers outside the visible range.
        /// </summary>
        /// <param name="firstIndex">The first visible item index.</param>
        /// <param name="lastIndex">The last visible item index.</param>
        private void CleanUpItems(int firstIndex, int lastIndex)
        {
            var generator = ItemContainerGenerator;
            for (int i = InternalChildren.Count - 1; i >= 0; i--)
            {
                var position = new GeneratorPosition(i, 0);
                var itemIndex = generator.IndexFromGeneratorPosition(position);
                if (itemIndex < firstIndex || itemIndex > lastIndex)
                {
                    if (InternalChildren[i].IsKeyboardFocusWithin)
                        continue;

                    generator.Remove(position, 1);
                    RemoveInternalChildRange(i, 1);
                }
            }
        }


        /// <summary>
        /// Updates the extent and viewport and notifies the scroll owner.
        /// </summary>
        /// <param name="extent">The extent.</param>
        /// <param name="viewport">The viewport.</param>
        private void UpdateScrollInfo(Size extent, Size viewport)
        {
            if (extent == _extent && viewport == _viewport)
                return;

            _extent = extent;
            _viewport = viewport;
            _scrollOwner?.InvalidateScrollInfo();
        }
    }
}
