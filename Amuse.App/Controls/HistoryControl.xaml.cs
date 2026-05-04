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

        public HistoryControl()
        {
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


        private Task OnItemSourceChanged()
        {
            CollectionView = new ListCollectionView(ItemSource) { IsLiveSorting = true };
            CollectionView.Filter = (obj) =>
            {
                if (obj is not IHistoryItem item)
                    return false;

                return true;
            };
            OnSortChanged();
            return Task.CompletedTask;
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
