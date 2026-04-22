using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Services;
using Amuse.App.Views;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : WindowMainBase
    {
        private readonly double _defaultWidth = 1600;
        private readonly double _defaultHeight = 900;
        private readonly View _defaultView = View.TextToImage;
        private View _view;
        private ViewCategory _viewCategory;

        public MainWindow(Settings settings, NavigationService navigation, IHistoryService historyService, IModelDownloadService downloadService)
        {
            Settings = settings;
            Navigation = navigation;
            HistoryService = historyService;
            DownloadService = downloadService;
            NavigateCommand = new AsyncRelayCommand<View>(NavigateAsync, CanNavigate);
            NavigateCategoryCommand = new AsyncRelayCommand<ViewCategory>(NavigateCategoryAsync, CanNavigateCategory);
            RemoveHistoryItemCommand = new AsyncRelayCommand<IHistoryItem>(RemoveHistoryItemAsync, CanRemoveHistoryItem);
            PreviewHistoryItemCommand = new AsyncRelayCommand<IHistoryItem>(PreviewHistoryItemAsync, CanPreviewHistoryItem);
            Settings.PropertyChanged += Settings_Changed;
            Width = _defaultWidth * Settings.UIScale;
            Height = _defaultHeight * Settings.UIScale;
            navigation.OnNavigated += (_, viewId) => OnNavigated(viewId);
            InitializeComponent();
            NavigateCommand.Execute(_defaultView);
        }

        public Settings Settings { get; }
        public NavigationService Navigation { get; }
        public AsyncRelayCommand<View> NavigateCommand { get; }
        public AsyncRelayCommand<ViewCategory> NavigateCategoryCommand { get; }
        public AsyncRelayCommand<IHistoryItem> RemoveHistoryItemCommand { get; }
        public AsyncRelayCommand<IHistoryItem> PreviewHistoryItemCommand { get; }
        public IHistoryService HistoryService { get; }
        public IModelDownloadService DownloadService { get; }

        public View View
        {
            get { return _view; }
            set { SetProperty(ref _view, value); }
        }

        public ViewCategory ViewCategory
        {
            get { return _viewCategory; }
            set { SetProperty(ref _viewCategory, value); }
        }


        private async Task NavigateAsync(View view)
        {
            View = view;
            ViewCategory = UpdateViewMap(view);
            await Navigation.NavigateAsync((int)view);
        }


        private void OnNavigated(int viewId)
        {
            var category = ViewManager.GetViewCategory((View)viewId);
            if (ViewCategory != category)
            {
                ViewCategory = category;
            }
        }


        private bool CanNavigate(View view)
        {
            return true;
        }


        private async Task NavigateCategoryAsync(ViewCategory category)
        {
            if (_viewCategory == category)
                return;

            var previousView = ViewManager.GetCurrentView(category);
            await NavigateAsync(previousView);
        }


        private bool CanNavigateCategory(ViewCategory category)
        {
            return true;
        }


        public override void OnDragBegin(DragDropType type)
        {
            base.OnDragBegin(type);
            Navigation.CurrentView.DragDropType = type;
            Navigation.CurrentView.IsDragDrop = true;
        }


        public override void OnDragEnd()
        {
            base.OnDragEnd();
            Navigation.CurrentView.IsDragDrop = false;
            Navigation.CurrentView.DragDropType = DragDropType.None;
        }


        private ViewCategory UpdateViewMap(View view)
        {
            return ViewManager.SetCurrentView(view);
        }


        private async Task RemoveHistoryItemAsync(IHistoryItem item)
        {
            await HistoryService.DeleteAsync(item);
        }


        private bool CanRemoveHistoryItem(IHistoryItem item)
        {
            return true;
        }


        private async Task PreviewHistoryItemAsync(IHistoryItem item)
        {
            var dialog = DialogService.GetDialog<MediaPreviewDialog>();
            await dialog.ShowDialogAsync(item);
        }


        private bool CanPreviewHistoryItem(IHistoryItem item)
        {
            return true;
        }


        private void Settings_Changed(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.UIScale))
            {
                UpdateUIScale();
            }
        }


        private void UpdateUIScale()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                Width = _defaultWidth * Settings.UIScale;
                Height = _defaultHeight * Settings.UIScale;
            });
        }


        protected override async void OnClosing(CancelEventArgs e)
        {
            if (DownloadService.IsDownloading)
            {
                if (!await DialogService.ShowMessageAsync("Active Downloads", "There are still have active downloads running, Are you sure you want to cancel and exit?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Question, TensorStack.WPF.Dialogs.MessageBoxStyleType.Info))
                {
                    e.Cancel = true;
                }
            }
            base.OnClosing(e);
        }
    }
}