using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Resources;
using Amuse.App.Services;
using Amuse.App.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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
        private readonly Size _defaultSize = new Size(1600, 900);
        private readonly Size _minimumSize = new Size(1280, 720);
        private readonly View _defaultView = View.Home;
        private View _view;
        private bool _isCloseConfirmed;
        private ViewCategory _viewCategory;
        private Dictionary<View, bool> _loadedViews;
        private Dictionary<ViewCategory, bool> _loadedCategories;

        public MainWindow(Settings settings, NavigationService navigation, IHistoryService historyService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IGenerateService generateService, ILanguageService languageService, IAudioService audioService, IUpscaleService upscaleService, IExtractService extractService, IInterpolationService interpolationService)
        {
            Settings = settings;
            ModelAccessService = modelAccessService;
            Navigation = navigation;
            HistoryService = historyService;
            DownloadService = downloadService;
            NavigateCommand = new AsyncRelayCommand<View>(NavigateAsync, CanNavigate);
            NavigateCategoryCommand = new AsyncRelayCommand<ViewCategory>(NavigateCategoryAsync, CanNavigateCategory);
            NavigateBackCommand = new AsyncRelayCommand(NavigateBackAsync, CanNavigateBack);
            NavigateForwardCommand = new AsyncRelayCommand(NavigateForwardAsync, CanNavigateForward);
            RemoveHistoryItemCommand = new AsyncRelayCommand<IHistoryItem>(RemoveHistoryItemAsync, CanRemoveHistoryItem);
            PreviewHistoryItemCommand = new AsyncRelayCommand<IHistoryItem>(PreviewHistoryItemAsync, CanPreviewHistoryItem);
            SubscribeLoadState(generateService, languageService, audioService, upscaleService, extractService, interpolationService);
            if (!Settings.IsFirstRunComplete)
                Settings.UIScale = GetFitScale();

            InitializeUIScale(Settings, _defaultSize, _minimumSize);
            navigation.OnNavigated += (_, viewId) => OnNavigated(viewId);
            Loaded += OnLoaded;
            UpdateLoadedViews();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public NavigationService Navigation { get; }
        public AsyncRelayCommand<View> NavigateCommand { get; }
        public AsyncRelayCommand<ViewCategory> NavigateCategoryCommand { get; }
        public AsyncRelayCommand NavigateBackCommand { get; }
        public AsyncRelayCommand NavigateForwardCommand { get; }
        public AsyncRelayCommand<IHistoryItem> RemoveHistoryItemCommand { get; }
        public AsyncRelayCommand<IHistoryItem> PreviewHistoryItemCommand { get; }
        public IHistoryService HistoryService { get; }
        public IModelDownloadService DownloadService { get; }
        public IModelAccessService ModelAccessService { get; }

        public Dictionary<View, bool> LoadedViews
        {
            get { return _loadedViews; }
            set { SetProperty(ref _loadedViews, value); }
        }

        public Dictionary<ViewCategory, bool> LoadedCategories
        {
            get { return _loadedCategories; }
            set { SetProperty(ref _loadedCategories, value); }
        }

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


        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            NavigateCommand.Execute(_defaultView);
        }


        private void OnNavigated(int viewId)
        {
            var view = (View)viewId;
            if (View != view)
                View = view;

            var category = UpdateViewMap(view);
            if (ViewCategory != category)
            {
                ViewCategory = category;
            }

            CommandManager.InvalidateRequerySuggested();
        }


        /// <summary>
        /// Subscribes to the load state of the services that load models.
        /// </summary>
        /// <param name="services">The services.</param>
        private void SubscribeLoadState(params object[] services)
        {
            foreach (var service in services.OfType<INotifyPropertyChanged>())
                service.PropertyChanged += Service_Changed;
        }


        /// <summary>
        /// Updates the loaded views when a model starts loading, is loaded or is unloaded, it can be raised from a background thread.
        /// </summary>
        /// <param name="sender">The service.</param>
        /// <param name="e">The property that changed.</param>
        private void Service_Changed(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(IGenerateService.IsLoaded) or nameof(IGenerateService.IsLoading) or nameof(IGenerateService.IsExecuting))
                _ = Dispatcher.InvokeAsync(UpdateLoadedViews);
        }


        /// <summary>
        /// Updates the views and categories that have a pipeline loaded, they show a dot on their menu button.
        /// </summary>
        private void UpdateLoadedViews()
        {
            var loadedViews = ModelAccessService.GetLoadedViews();
            var loadedCategories = loadedViews.Select(ViewManager.GetViewCategory).ToList();
            LoadedViews = Enum.GetValues<View>().ToDictionary(x => x, x => loadedViews.Contains(x));
            LoadedCategories = Enum.GetValues<ViewCategory>().ToDictionary(x => x, x => loadedCategories.Contains(x));
        }

        private async Task NavigateAsync(View view)
        {
            View = view;
            ViewCategory = UpdateViewMap(view);
            await Navigation.NavigateAsync((int)view);
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


        /// <summary>
        /// Navigates back to the previous view.
        /// </summary>
        private async Task NavigateBackAsync()
        {
            await Navigation.GoBackAsync();
        }


        /// <summary>
        /// Determines whether there is a view to navigate back to.
        /// </summary>
        private bool CanNavigateBack()
        {
            return Navigation.CanGoBack;
        }


        /// <summary>
        /// Navigates forward to the view that was left by navigating back.
        /// </summary>
        private async Task NavigateForwardAsync()
        {
            await Navigation.GoForwardAsync();
        }


        /// <summary>
        /// Determines whether there is a view to navigate forward to.
        /// </summary>
        private bool CanNavigateForward()
        {
            return Navigation.CanGoForward;
        }


        /// <summary>
        /// Navigates back or forward when the mouse back or forward button is released.
        /// </summary>
        /// <param name="e">The mouse button event data.</param>
        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseUp(e);
            if (e.ChangedButton == MouseButton.XButton1)
                e.Handled = ExecuteNavigateCommand(NavigateBackCommand);
            else if (e.ChangedButton == MouseButton.XButton2)
                e.Handled = ExecuteNavigateCommand(NavigateForwardCommand);
        }


        /// <summary>
        /// Navigates back on Alt+Left or the keyboard back key, and forward on Alt+Right or the keyboard forward key.
        /// </summary>
        /// <param name="e">The key event data.</param>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            var isAlt = e.Key == Key.System && Keyboard.Modifiers == ModifierKeys.Alt;
            if ((isAlt && e.SystemKey == Key.Left) || e.Key == Key.BrowserBack)
                e.Handled = ExecuteNavigateCommand(NavigateBackCommand);
            else if ((isAlt && e.SystemKey == Key.Right) || e.Key == Key.BrowserForward)
                e.Handled = ExecuteNavigateCommand(NavigateForwardCommand);
        }


        /// <summary>
        /// Executes a back or forward command when it can run, the command stops a second navigation starting while one is running.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns><c>true</c> if the command was executed, otherwise <c>false</c>.</returns>
        private static bool ExecuteNavigateCommand(ICommand command)
        {
            if (!command.CanExecute(null))
                return false;

            command.Execute(null);
            return true;
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


        /// <summary>
        /// Gets the largest UI scale, up to 100%, that fits the default window size in the screen work area.
        /// </summary>
        private double GetFitScale()
        {
            var workArea = SystemParameters.WorkArea;
            var fit = Math.Min(workArea.Width / _defaultSize.Width, workArea.Height / _defaultSize.Height);
            return ScaleOption.FloorToStep(fit, 1);
        }


        protected override async void OnClosing(CancelEventArgs e)
        {
            if (DownloadService.IsDownloading && !_isCloseConfirmed)
            {
                e.Cancel = true;
                if (await DialogService.ShowMessageAsync(AppDefault.ActiveDownloads, AppDefault.ActiveDownloadsMessage, TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Question, TensorStack.WPF.Dialogs.MessageBoxStyleType.Info))
                {
                    _isCloseConfirmed = true;
                    _ = Dispatcher.InvokeAsync(Close);
                }
                return;
            }
            base.OnClosing(e);
        }
    }
}