using Amuse.App.Common;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Utils;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for HistoryControl.xaml
    /// </summary>
    public partial class HistoryControl : BaseControl
    {
        private ICollectionView _collectionView;
        private Point _dragStartPoint;
        private ScrollBarVisibility _horizontalScrollBarVisibility = ScrollBarVisibility.Visible;
        private ScrollBarVisibility _verticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        private bool _isImageFilter;
        private bool _isVideoFilter;
        private bool _isAudioFilter;
        private bool _isTextFilter;

        /// <summary>
        /// Initializes a new instance of the <see cref="HistoryControl"/> class.
        /// </summary>
        public HistoryControl()
        {
            ClearFilterCommand = new RelayCommand(ClearFilter, CanClearFilter);
            InitializeComponent();
        }

        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register(nameof(ItemSource), typeof(ObservableCollection<IHistoryItem>), typeof(HistoryControl), new PropertyMetadata<HistoryControl>(x => x.OnItemSourceChanged()));
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(IHistoryItem), typeof(HistoryControl));
        public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(HistoryControl));
        public static readonly DependencyProperty ItemsPanelTemplateProperty = DependencyProperty.Register(nameof(ItemsPanelTemplate), typeof(ItemsPanelTemplate), typeof(HistoryControl));
        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(HistoryControl), new PropertyMetadata<HistoryControl>(x => x.OnOrientationChanged()) { DefaultValue = Orientation.Horizontal });
        public static readonly DependencyProperty SortPropertyProperty = DependencyProperty.Register(nameof(SortProperty), typeof(string), typeof(HistoryControl), new PropertyMetadata<HistoryControl>(x => x.OnSortChanged()) { DefaultValue = nameof(IHistoryItem.LastAccess) });
        public static readonly DependencyProperty SortDirectionProperty = DependencyProperty.Register(nameof(SortDirection), typeof(ListSortDirection), typeof(HistoryControl), new PropertyMetadata<HistoryControl>(x => x.OnSortChanged()) { DefaultValue = ListSortDirection.Descending });
        public static readonly DependencyProperty PreviewItemCommandProperty = DependencyProperty.Register(nameof(PreviewItemCommand), typeof(AsyncRelayCommand<IHistoryItem>), typeof(HistoryControl));
        public static readonly DependencyProperty RemoveItemCommandProperty = DependencyProperty.Register(nameof(RemoveItemCommand), typeof(AsyncRelayCommand<IHistoryItem>), typeof(HistoryControl));

        public ObservableCollection<IHistoryItem> ItemSource
        {
            get { return (ObservableCollection<IHistoryItem>)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }

        public IHistoryItem SelectedItem
        {
            get { return (IHistoryItem)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        public DataTemplate ItemTemplate
        {
            get { return (DataTemplate)GetValue(ItemTemplateProperty); }
            set { SetValue(ItemTemplateProperty, value); }
        }

        public ItemsPanelTemplate ItemsPanelTemplate
        {
            get { return (ItemsPanelTemplate)GetValue(ItemsPanelTemplateProperty); }
            set { SetValue(ItemsPanelTemplateProperty, value); }
        }

        public Orientation Orientation
        {
            get { return (Orientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        public string SortProperty
        {
            get { return (string)GetValue(SortPropertyProperty); }
            set { SetValue(SortPropertyProperty, value); }
        }

        public ListSortDirection SortDirection
        {
            get { return (ListSortDirection)GetValue(SortDirectionProperty); }
            set { SetValue(SortDirectionProperty, value); }
        }

        public ICollectionView CollectionView
        {
            get { return _collectionView; }
            set { SetProperty(ref _collectionView, value); }
        }

        public ScrollBarVisibility HorizontalScrollBarVisibility
        {
            get { return _horizontalScrollBarVisibility; }
            set { SetProperty(ref _horizontalScrollBarVisibility, value); }
        }

        public ScrollBarVisibility VerticalScrollBarVisibility
        {
            get { return _verticalScrollBarVisibility; }
            set { SetProperty(ref _verticalScrollBarVisibility, value); }
        }

        public AsyncRelayCommand<IHistoryItem> PreviewItemCommand
        {
            get { return (AsyncRelayCommand<IHistoryItem>)GetValue(PreviewItemCommandProperty); }
            set { SetValue(PreviewItemCommandProperty, value); }
        }

        public AsyncRelayCommand<IHistoryItem> RemoveItemCommand
        {
            get { return (AsyncRelayCommand<IHistoryItem>)GetValue(RemoveItemCommandProperty); }
            set { SetValue(RemoveItemCommandProperty, value); }
        }

        public RelayCommand ClearFilterCommand { get; }

        public bool IsImageFilter
        {
            get { return _isImageFilter; }
            set { SetProperty(ref _isImageFilter, value); RefreshFilter(); }
        }

        public bool IsVideoFilter
        {
            get { return _isVideoFilter; }
            set { SetProperty(ref _isVideoFilter, value); RefreshFilter(); }
        }

        public bool IsAudioFilter
        {
            get { return _isAudioFilter; }
            set { SetProperty(ref _isAudioFilter, value); RefreshFilter(); }
        }

        public bool IsTextFilter
        {
            get { return _isTextFilter; }
            set { SetProperty(ref _isTextFilter, value); RefreshFilter(); }
        }

        public bool IsFilterActive
        {
            get { return _isImageFilter || _isVideoFilter || _isAudioFilter || _isTextFilter; }
        }


        /// <summary>
        /// Called when the item source changed.
        /// </summary>
        private Task OnItemSourceChanged()
        {
            CollectionView = new ListCollectionView(ItemSource) { IsLiveSorting = true };
            CollectionView.Filter = (obj) =>
            {
                if (obj is not IHistoryItem item)
                    return false;

                return IsMediaTypeVisible(item.MediaType);
            };
            OnSortChanged();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether the media type passes the current filter.
        /// </summary>
        /// <param name="mediaType">The media type.</param>
        private bool IsMediaTypeVisible(MediaType mediaType)
        {
            if (!IsFilterActive)
                return true;

            return mediaType switch
            {
                MediaType.Image => _isImageFilter,
                MediaType.Video => _isVideoFilter,
                MediaType.Audio => _isAudioFilter,
                MediaType.Text => _isTextFilter,
                _ => false
            };
        }


        /// <summary>
        /// Refreshes the filter.
        /// </summary>
        private void RefreshFilter()
        {
            NotifyPropertyChanged(nameof(IsFilterActive));
            CollectionView?.Refresh();
        }


        /// <summary>
        /// Clears the filter.
        /// </summary>
        private void ClearFilter()
        {
            _isImageFilter = false;
            _isVideoFilter = false;
            _isAudioFilter = false;
            _isTextFilter = false;
            NotifyPropertyChanged(nameof(IsImageFilter));
            NotifyPropertyChanged(nameof(IsVideoFilter));
            NotifyPropertyChanged(nameof(IsAudioFilter));
            NotifyPropertyChanged(nameof(IsTextFilter));
            RefreshFilter();
        }


        /// <summary>
        /// Determines whether the filter can be cleared.
        /// </summary>
        private bool CanClearFilter()
        {
            return IsFilterActive;
        }


        private Task OnOrientationChanged()
        {
            VerticalScrollBarVisibility = Orientation == Orientation.Vertical ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
            HorizontalScrollBarVisibility = Orientation == Orientation.Horizontal ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
            return Task.CompletedTask;
        }


        private Task OnSortChanged()
        {
            if (CollectionView?.SortDescriptions is not null)
            {
                CollectionView.SortDescriptions.Clear();
                CollectionView.SortDescriptions.Add(new SortDescription(SortProperty, SortDirection));
                CollectionView.SortDescriptions.Add(new SortDescription(nameof(IHistoryItem.Timestamp), SortDirection));
            }
            return Task.CompletedTask;
        }


        protected void ListBoxPreviewMouseMove(object sender, MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (e.OriginalSource is not DependencyObject source || ItemsControl.ContainerFromElement(ListBoxControl, source) is null)
                    return;

                // check if drag threshold is passed
                var diff = e.GetPosition(null) - _dragStartPoint;
                if (Math.Abs(diff.X) > TensorStack.WPF.Common.DragDistance || Math.Abs(diff.Y) > TensorStack.WPF.Common.DragDistance)
                {
                    if (SelectedItem is not null)
                    {
                        var listBoxItem = (ListBoxItem)ListBoxControl.ItemContainerGenerator.ContainerFromItem(SelectedItem);
                        if (listBoxItem == null)
                            return;

                        var dropType = SelectedItem.MediaType switch
                        {
                            MediaType.Image => DragDropType.Image,
                            MediaType.Video => DragDropType.Video,
                            MediaType.Audio => DragDropType.Audio,
                            MediaType.Text => DragDropType.Text,
                            _ => throw new NotSupportedException()
                        };

                        if (SelectedItem.Source == Views.View.TextConverse)
                        {
                            dropType = DragDropType.Conversation;
                        }
                        DragDropHelper.DoDragDropFile(this, SelectedItem.MediaPath, dropType, listBoxItem);
                    }
                }
            }
            else if (e.LeftButton == MouseButtonState.Released)
            {
                _dragStartPoint = e.GetPosition(null); // reset for next drag
            }
        }


        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            if (Orientation == Orientation.Horizontal)
            {
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
            }
            else
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            }
            e.Handled = true;
        }


        private void ListBox_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }


        static HistoryControl()
        {
            // Create a default ItemsPanelTemplate with a VirtualizingStackPanel
            var factory = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
            factory.SetValue(VirtualizingStackPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
            factory.SetBinding(VirtualizingStackPanel.OrientationProperty, new Binding(nameof(Orientation))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(HistoryControl), 1)
            });

            var template = new ItemsPanelTemplate(factory);
            template.Seal();
            ItemsPanelTemplateProperty.OverrideMetadata(typeof(HistoryControl), new FrameworkPropertyMetadata(template));
        }
    }


    public class MediaTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ImageTemplate { get; set; }
        public DataTemplate VideoTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is IHistoryItem historyItem)
            {
                return historyItem.MediaType switch
                {
                    MediaType.Image => ImageTemplate,
                    MediaType.Video => VideoTemplate,
                    _ => base.SelectTemplate(item, container)
                };
            }
            return base.SelectTemplate(item, container);
        }
    }
}
