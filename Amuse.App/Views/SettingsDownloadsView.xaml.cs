using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for SettingsDownloadsView.xaml
    /// </summary>
    public partial class SettingsDownloadsView : ViewBase
    {
        private readonly IDownloadService _downloadService;
        private DownloadQueueItem _selectedModel;
        private string _filterText;

        public SettingsDownloadsView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IHistoryService historyService, IDownloadService downloadService, ILogger<SettingsDownloadsView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            _downloadService = downloadService;
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new AsyncRelayCommand(CancelAsync, CanCancel);
            CancelAllCommand = new AsyncRelayCommand(CancelAllAsync, CanCancelAll);

            FilterClearCommand = new AsyncRelayCommand(FilterClearAsync, CanClearFilter);
            ModelCollection = new ListCollectionView(_downloadService.Queue) { Filter = CollectionFilter(), IsLiveSorting = true };
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(DownloadQueueItem.Index), ListSortDirection.Ascending));
            SelectedModel = _downloadService.Queue.FirstOrDefault();
            InitializeComponent();
        }

        public override View View => View.Downloads;
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand CancelAllCommand { get; }
        public AsyncRelayCommand FilterClearCommand { get; }
        public ListCollectionView ModelCollection { get; }

        public DownloadQueueItem SelectedModel
        {
            get { return _selectedModel; }
            set { SetProperty(ref _selectedModel, value); }
        }

        public string FilterText
        {
            get { return _filterText; }
            set { SetProperty(ref _filterText, value); ModelCollection?.Refresh(); }
        }


        public override Task OpenAsync(OpenViewArgs args = null)
        {
            return base.OpenAsync(args);
        }


        private Predicate<object> CollectionFilter()
        {
            return (obj) =>
            {
                if (obj is not DownloadQueueItem model)
                    return false;

                if (!string.IsNullOrEmpty(_filterText))
                {
                    return model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
                }
                return true;
            };
        }


        private Task FilterClearAsync()
        {
            FilterText = null;
            return Task.CompletedTask;
        }


        private bool CanClearFilter()
        {
            return !string.IsNullOrWhiteSpace(_filterText);
        }


        private async Task CancelAsync()
        {
            await _downloadService.CancelAsync(SelectedModel);
            SelectedModel = _downloadService.Queue.FirstOrDefault();
        }


        private bool CanCancel()
        {
            return SelectedModel != null;
        }


        private async Task CancelAllAsync()
        {
            await _downloadService.CancelAllAsync();
            SelectedModel = default;
        }


        private bool CanCancelAll()
        {
            return _downloadService.CanCancel;
        }


        private async Task SaveAsync()
        {
            await SettingsManager.SaveAsync(Settings);
            Settings.ScanModels();
        }
    }
}