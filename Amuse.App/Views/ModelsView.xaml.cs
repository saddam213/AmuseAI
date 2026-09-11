using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Services;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for ModelsView.xaml
    /// </summary>
    public partial class ModelsView : ViewBase
    {
        private const string String_AllPipelines = "All Pipelines";
        private IDownloadModel _selectedItem;
        private readonly IModelDownloadService _downloadService;
        private DownloadQueueItem _selectedDownload;
        private string _filterText;
        private string _filterPipeline;
        private ModelCategoryType? _filterCategoryType;
        private MediaType? _filterMediaType;
        private BackendType? _filterBackendType;
        private bool _filterInstalled = true;

        public ModelsView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IHistoryService historyService, ILogger<ModelsView> logger)
            : base(settings, navigationService, downloadService, historyService, logger)
        {
            _downloadService = downloadService;
            FilterPipelines = new ObservableCollection<string>();
            FilterModelTypes = new ObservableCollection<string>();
            RemoveFiltersCommand = new AsyncRelayCommand(RemoveFilters, CanRemoveFilters);
            DownloadCommand = new AsyncRelayCommand(DownloadAsync, CanDownload);
            DownloadRetryCommand = new AsyncRelayCommand(DownloadRetryAsync, CanDownloadRetry);
            DownloadCancelCommand = new AsyncRelayCommand(DownloadCancelAsync, CanDownloadCancel);
            DownloadCancelAllCommand = new AsyncRelayCommand(DownloadCancelAllAsync, CanDownloadCancelAll);
            DownloadInformationCommand = new AsyncRelayCommand(DownloadInformationAsync, CanDownloadInformation);
            DownloadCollection = new ListCollectionView(_downloadService.Queue) { IsLiveSorting = true };
            DownloadCollection.SortDescriptions.Add(new SortDescription(nameof(DownloadQueueItem.Index), ListSortDirection.Ascending));
            var composite = new List<IDownloadModel>();
            composite.AddRange(Settings.DiffusionModels.OrderByDescending(x => x.Backend).ThenBy(x => x.Name));
            composite.AddRange(Settings.LanguageModels.OrderByDescending(x => x.Backend).ThenBy(x => x.Name));
            composite.AddRange(Settings.LoraAdapterModels.OrderBy(x => x.Name));
            composite.AddRange(Settings.ControlNetModels.OrderBy(x => x.Name));
            composite.AddRange(Settings.UpscaleModels.OrderByDescending(x => x.Backend).ThenBy(x => x.Name));
            composite.AddRange(Settings.ExtractModels.OrderByDescending(x => x.Backend).ThenBy(x => x.Name));
            composite.AddRange(Settings.Components.OrderBy(x => x.Name));
            ModelCollection = new ListCollectionView(composite) { Filter = CollectionFilter() };
            Populate();
            InitializeComponent();
            ModelCollection.MoveCurrentToFirst();
            DownloadCollection.MoveCurrentToFirst();
            SelectedItem = ModelCollection.CurrentItem as IDownloadModel;
            SelectedDownload = DownloadCollection.CurrentItem as DownloadQueueItem;
        }

        public override View View => View.Models;
        public ListCollectionView ModelCollection { get; }
        public ListCollectionView DownloadCollection { get; }
        public AsyncRelayCommand DownloadCommand { get; }
        public AsyncRelayCommand DownloadRetryCommand { get; }
        public AsyncRelayCommand DownloadCancelCommand { get; }
        public AsyncRelayCommand DownloadCancelAllCommand { get; }
        public AsyncRelayCommand DownloadInformationCommand { get; }
        public AsyncRelayCommand RemoveFiltersCommand { get; }
        public ObservableCollection<string> FilterPipelines { get; }
        public ObservableCollection<string> FilterModelTypes { get; }

        public IDownloadModel SelectedItem
        {
            get { return _selectedItem; }
            set { SetProperty(ref _selectedItem, value); }
        }

        public DownloadQueueItem SelectedDownload
        {
            get { return _selectedDownload; }
            set { SetProperty(ref _selectedDownload, value); }
        }

        public string FilterText
        {
            get { return _filterText; }
            set { SetProperty(ref _filterText, value); ModelCollection?.Refresh(); }
        }

        public string FilterPipeline
        {
            get { return _filterPipeline; }
            set { SetProperty(ref _filterPipeline, value); ModelCollection?.Refresh(); }
        }

        public ModelCategoryType? FilterCategoryType
        {
            get { return _filterCategoryType; }
            set { SetProperty(ref _filterCategoryType, value); ModelCollection?.Refresh(); }
        }

        public MediaType? FilterMediaType
        {
            get { return _filterMediaType; }
            set { SetProperty(ref _filterMediaType, value); ModelCollection?.Refresh(); }
        }

        public BackendType? FilterBackendType
        {
            get { return _filterBackendType; }
            set { SetProperty(ref _filterBackendType, value); ModelCollection?.Refresh(); }
        }

        public bool FilterInstalled
        {
            get { return _filterInstalled; }
            set { SetProperty(ref _filterInstalled, value); ModelCollection?.Refresh(); }
        }


        public override async Task OpenAsync(OpenViewArgs args = null)
        {
            await base.OpenAsync(args);
            if (args is ModelViewOpenArgs modelViewArgs)
            {
                _filterCategoryType = modelViewArgs.ModelType;
                _filterPipeline = modelViewArgs.PipelineType.HasValue ? modelViewArgs.PipelineType.GetDisplayName() : String_AllPipelines;
                NotifyPropertyChanged(nameof(FilterPipeline));
                NotifyPropertyChanged(nameof(FilterCategoryType));
                ModelCollection.Refresh();
            }
        }


        public override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        private Predicate<object> CollectionFilter()
        {
            return (obj) =>
            {
                if (obj is not IDownloadModel model)
                    return false;
                if (model.Id > Utils.FixedIdRange)
                    return false;
                if (!_filterInstalled && model.Status == ModelStatusType.Installed)
                    return false;

                var isValid = true;
                if (model is DiffusionModel diffusionModel)
                {
                    var pipelineName = diffusionModel.Pipeline.GetDisplayName();
                    if (_filterCategoryType.HasValue)
                        isValid = isValid && _filterCategoryType == ModelCategoryType.Diffusion;
                    if (_filterMediaType.HasValue)
                        isValid = isValid && diffusionModel.MediaType == _filterMediaType.Value;
                    if (_filterBackendType.HasValue)
                        isValid = isValid && diffusionModel.Backend == _filterBackendType.Value;
                    if (_filterPipeline != String_AllPipelines)
                        isValid = isValid && pipelineName == _filterPipeline;
                    if (!string.IsNullOrEmpty(_filterText))
                        isValid = isValid && (model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                                         || pipelineName.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
                }
                else if (model is LoraAdapterModel loraAdapterModel)
                {
                    if (_filterMediaType.HasValue)
                        return false;

                    var pipelineName = loraAdapterModel.Pipeline.GetDisplayName();
                    if (_filterCategoryType.HasValue)
                        isValid = isValid && _filterCategoryType == ModelCategoryType.LoraAdapter;
                    if (_filterBackendType.HasValue)
                        isValid = isValid && _filterBackendType != BackendType.OnnxRuntime;
                    if (_filterPipeline != String_AllPipelines)
                        isValid = isValid && pipelineName == _filterPipeline;
                    if (!string.IsNullOrEmpty(_filterText))
                        isValid = isValid && (model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                                         || pipelineName.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
                }
                else if (model is ControlNetModel controlnet)
                {
                    if (_filterMediaType.HasValue)
                        return false;

                    var pipelineName = controlnet.Pipeline.GetDisplayName();
                    if (_filterCategoryType.HasValue)
                        isValid = isValid && _filterCategoryType == ModelCategoryType.ControlNet;
                    if (_filterBackendType.HasValue)
                        isValid = isValid && _filterBackendType != BackendType.OnnxRuntime;
                    if (_filterPipeline != String_AllPipelines)
                        isValid = isValid && pipelineName == _filterPipeline;
                    if (!string.IsNullOrEmpty(_filterText))
                        isValid = isValid && (model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                                         || pipelineName.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
                }
                else if (model is UpscaleModel upscaleModel)
                {
                    var pipelineName = upscaleModel.Pipeline.GetDisplayName();
                    if (_filterCategoryType.HasValue)
                        isValid = isValid && _filterCategoryType == ModelCategoryType.Upscale;
                    if (_filterMediaType.HasValue)
                        isValid = isValid && (_filterMediaType.Value == MediaType.Image || _filterMediaType.Value == MediaType.Video);
                    if (_filterBackendType.HasValue)
                        isValid = isValid && upscaleModel.Backend == _filterBackendType.Value;
                    if (_filterPipeline != String_AllPipelines)
                        isValid = isValid && pipelineName == _filterPipeline;
                    if (!string.IsNullOrEmpty(_filterText))
                        isValid = isValid && (model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                                         || pipelineName.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
                }
                else if (model is ExtractModel extractModel)
                {
                    var pipelineName = extractModel.Pipeline.GetDisplayName();
                    if (_filterCategoryType.HasValue)
                        isValid = isValid && _filterCategoryType == ModelCategoryType.Extract;
                    if (_filterMediaType.HasValue)
                        isValid = isValid && (_filterMediaType.Value == MediaType.Image || _filterMediaType.Value == MediaType.Video);
                    if (_filterBackendType.HasValue)
                        isValid = isValid && extractModel.Backend == _filterBackendType.Value;
                    if (_filterPipeline != String_AllPipelines)
                        isValid = isValid && pipelineName == _filterPipeline;
                    if (!string.IsNullOrEmpty(_filterText))
                        isValid = isValid && (model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                                         || pipelineName.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
                }
                else if (model is LanguageModel languageModel)
                {
                    var pipelineName = languageModel.Pipeline.GetDisplayName();
                    if (_filterCategoryType.HasValue)
                        isValid = isValid && _filterCategoryType == ModelCategoryType.LLM;
                    if (_filterMediaType.HasValue)
                        isValid = isValid && languageModel.MediaType == _filterMediaType.Value;
                    if (_filterBackendType.HasValue)
                        isValid = isValid && languageModel.Backend == _filterBackendType.Value;
                    if (_filterPipeline != String_AllPipelines)
                        isValid = isValid && pipelineName == _filterPipeline;
                    if (!string.IsNullOrEmpty(_filterText))
                        isValid = isValid && (model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                                         || pipelineName.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
                }
                else if (model is ComponentModel componentModel)
                {
                    if (_filterMediaType.HasValue || _filterPipeline != String_AllPipelines)
                        isValid = false;

                    if (_filterCategoryType.HasValue)
                        isValid = isValid && _filterCategoryType == ModelCategoryType.Component;
                    if (_filterBackendType.HasValue)
                        isValid = isValid && _filterBackendType != BackendType.OnnxRuntime;
                    if (!string.IsNullOrEmpty(_filterText))
                        isValid = isValid && model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
                }
                return isValid;
            };
        }


        private void Populate()
        {
            FilterPipelines.Clear();
            FilterPipelines.Add(String_AllPipelines);
            foreach (var pipelineName in Enum.GetValues<PipelineType>().Select(x => x.GetDisplayName()).OrderBy(x => x))
            {
                if (pipelineName.Equals(PipelineType.Kandinsky5Pipeline.GetDisplayName()))
                    continue;

                FilterPipelines.Add(pipelineName);
            }
            FilterPipeline = FilterPipelines[0];
        }


        private Task RemoveFilters()
        {
            _filterText = null;
            _filterMediaType = null;
            _filterBackendType = null;
            _filterCategoryType = null;
            _filterPipeline = String_AllPipelines;
            ModelCollection.Refresh();
            NotifyPropertyChanged(nameof(FilterText));
            NotifyPropertyChanged(nameof(FilterPipeline));
            NotifyPropertyChanged(nameof(FilterMediaType));
            NotifyPropertyChanged(nameof(FilterBackendType));
            NotifyPropertyChanged(nameof(FilterCategoryType));
            return Task.CompletedTask;
        }


        private bool CanRemoveFilters()
        {
            return !string.IsNullOrWhiteSpace(_filterText)
                || _filterPipeline?.Equals(String_AllPipelines) == false
                || _filterMediaType.HasValue
                || _filterBackendType.HasValue
                || _filterCategoryType.HasValue;
        }


        private async Task DownloadAsync()
        {
            await DownloadService.QueueAsync(_selectedItem);
        }


        private bool CanDownload()
        {
            return _selectedItem?.Status == ModelStatusType.Unknown
                || _selectedItem?.Status == ModelStatusType.Available;
        }


        private async Task DownloadRetryAsync()
        {
            await DownloadService.QueueAsync(_selectedDownload.DownloadModel);
        }


        private bool CanDownloadRetry()
        {
            return _selectedDownload?.Status == ModelStatusType.DownloadFailed;
        }


        private async Task DownloadCancelAsync()
        {
            await base.CancelAsync();
            await _downloadService.CancelAsync(SelectedDownload);
            SelectedDownload = _downloadService.Queue.FirstOrDefault();
        }


        private bool CanDownloadCancel()
        {
            return base.CanCancel() || SelectedDownload != null;
        }


        private async Task DownloadCancelAllAsync()
        {
            await _downloadService.CancelAllAsync();
            SelectedDownload = default;
        }


        private bool CanDownloadCancelAll()
        {
            return _downloadService.CanCancel;
        }


        private async Task DownloadInformationAsync()
        {
            var dialog = DialogService.GetDialog<ModelInformationDialog>();
            await dialog.ShowDialogAsync(_selectedItem);
        }


        private bool CanDownloadInformation()
        {
            return _selectedItem != null;
        }


        private async Task SaveAsync()
        {
            await SettingsManager.SaveAsync(Settings);
            Settings.ScanModels();
        }


        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }


    }
}