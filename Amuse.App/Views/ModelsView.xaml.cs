using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Resources;
using Amuse.App.Services;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Dialogs;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for ModelsView.xaml
    /// </summary>
    public partial class ModelsView : ViewBase
    {
        private const string FilterAllPipelines = ""; private IDownloadModel _selectedItem;
        private const int MinItemWidth = 240;
        private const int MaxItemWidth = 300;
        private IReadOnlyList<ModelListRow> _modelRows = [];
        private List<ModelListItem> _modelItems = [];
        private int _columns = 4;
        private ScrollViewer _scrollViewer;
        private readonly IModelDownloadService _downloadService;
        private DownloadQueueItem _selectedDownload;
        private string _filterText;
        private string _filterPipeline;
        private ModelCategoryType? _filterCategoryType;
        private MediaType? _filterMediaType;
        private BackendType? _filterBackendType;
        private bool _filterInstalled = true;
        private bool _filterComponents;
        private readonly ModelGroupDescription _groupDescription = new ModelGroupDescription();

        public ModelsView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IHistoryService historyService, ILogger<ModelsView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, historyService, logger)
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
            QueueAllCommand = new AsyncRelayCommand(QueueAllAsync, CanQueueAll);
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
        public AsyncRelayCommand QueueAllCommand { get; }
        public AsyncRelayCommand RemoveFiltersCommand { get; }
        public ObservableCollection<string> FilterPipelines { get; }
        public ObservableCollection<string> FilterModelTypes { get; }
        public int ModelCount => ModelCollection.SourceCollection.Cast<object>().Count(x => ModelCollection.Filter(x));

        public IReadOnlyList<ModelListRow> ModelRows
        {
            get { return _modelRows; }
            private set { SetProperty(ref _modelRows, value); }
        }

        public IDownloadModel SelectedItem
        {
            get { return _selectedItem; }
            set { SetProperty(ref _selectedItem, value); UpdateSelection(); }
        }

        public DownloadQueueItem SelectedDownload
        {
            get { return _selectedDownload; }
            set { SetProperty(ref _selectedDownload, value); }
        }

        public string FilterText
        {
            get { return _filterText; }
            set { SetProperty(ref _filterText, value); RefreshModels(); }
        }

        public string FilterPipeline
        {
            get { return _filterPipeline; }
            set { SetProperty(ref _filterPipeline, value); RefreshModels(); }
        }

        public ModelCategoryType? FilterCategoryType
        {
            get { return _filterCategoryType; }
            set { SetProperty(ref _filterCategoryType, value); RefreshModels(); }
        }

        public MediaType? FilterMediaType
        {
            get { return _filterMediaType; }
            set { SetProperty(ref _filterMediaType, value); RefreshModels(); }
        }

        public BackendType? FilterBackendType
        {
            get { return _filterBackendType; }
            set { SetProperty(ref _filterBackendType, value); RefreshModels(); }
        }

        public bool FilterInstalled
        {
            get { return _filterInstalled; }
            set { SetProperty(ref _filterInstalled, value); RefreshModels(); }
        }

        public bool FilterComponents
        {
            get { return _filterComponents; }
            set { SetProperty(ref _filterComponents, value); RefreshModels(); }
        }


        public override async Task OpenAsync(OpenViewArgs args = null)
        {
            await base.OpenAsync(args);
            if (args is ModelViewOpenArgs modelViewArgs)
            {
                _filterCategoryType = modelViewArgs.ModelType;
                _filterPipeline = modelViewArgs.PipelineType.HasValue ? modelViewArgs.PipelineType.GetDisplayName() : FilterAllPipelines;
                NotifyPropertyChanged(nameof(FilterPipeline));
                NotifyPropertyChanged(nameof(FilterCategoryType));
                RefreshModels();
            }
            else if (args is ModelOpenArgs modelOpenArgs)
            {
                await SelectModelAsync(modelOpenArgs.Model);
            }
        }


        /// <summary>
        /// Selects the model in the list, clearing the filters only when they hide it.
        /// </summary>
        /// <param name="model">The model.</param>
        private async Task SelectModelAsync(IGenerateModel model)
        {
            if (model is null)
                return;

            // Filters
            RefreshModels();
            if (!ModelCollection.Contains(model))
                await RemoveFilters();

            // Select
            if (!ModelCollection.Contains(model))
                return;

            ModelCollection.MoveCurrentTo(model);
            SelectedItem = model;
            var row = _modelRows.FirstOrDefault(x => x.Items?.Any(i => i.Model == model) == true);
            if (row is not null)
                await Dispatcher.InvokeAsync(() => ListBoxControl.ScrollIntoView(row), DispatcherPriority.Loaded);
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
                    if (_filterPipeline != FilterAllPipelines)
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
                    if (_filterPipeline != FilterAllPipelines)
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
                    if (_filterPipeline != FilterAllPipelines)
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
                    if (_filterPipeline != FilterAllPipelines)
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
                    if (_filterPipeline != FilterAllPipelines)
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
                    if (_filterPipeline != FilterAllPipelines)
                        isValid = isValid && pipelineName == _filterPipeline;
                    if (!string.IsNullOrEmpty(_filterText))
                        isValid = isValid && (model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                                         || pipelineName.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
                }
                else if (model is ComponentModel componentModel)
                {
                    if (!_filterComponents && _filterCategoryType != ModelCategoryType.Component)
                        return false;

                    if (_filterMediaType.HasValue || _filterPipeline != FilterAllPipelines)
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


        /// <summary>
        /// Refreshes the model list, the groups are only shown while no filters are set.
        /// </summary>
        private void RefreshModels()
        {
            if (ModelCollection is null)
                return;

            var isGrouped = ModelCollection.GroupDescriptions.Count > 0;
            if (isGrouped && CanRemoveFilters())
                ModelCollection.GroupDescriptions.Clear();
            else if (!isGrouped && !CanRemoveFilters())
                ModelCollection.GroupDescriptions.Add(_groupDescription);
            else
                ModelCollection.Refresh();

            UpdateRows();
            NotifyPropertyChanged(nameof(ModelCount));
        }


        /// <summary>
        /// Rebuilds the list rows from the filtered models, a header row for each group followed by rows of items, the list only creates the rows on screen.
        /// </summary>
        private void UpdateRows()
        {
            var rows = new List<ModelListRow>();
            var items = new List<ModelListItem>();
            if (ModelCollection.Groups is not null)
            {
                foreach (var group in ModelCollection.Groups.OfType<CollectionViewGroup>())
                {
                    if (group.ItemCount == 0)
                        continue;

                    rows.Add(new ModelListRow(group.Name?.ToString()));
                    AddItemRows(rows, items, group.Items.OfType<IDownloadModel>());
                }
            }
            else
            {
                AddItemRows(rows, items, ModelCollection.OfType<IDownloadModel>());
            }

            _modelItems = items;
            ModelRows = rows;
            UpdateSelection();
        }


        /// <summary>
        /// Adds the models to the rows, filling each row up to the column count.
        /// </summary>
        /// <param name="rows">The rows.</param>
        /// <param name="items">The list items, every item created is added.</param>
        /// <param name="models">The models.</param>
        private void AddItemRows(List<ModelListRow> rows, List<ModelListItem> items, IEnumerable<IDownloadModel> models)
        {
            var rowItems = new List<ModelListItem>(_columns);
            foreach (var model in models)
            {
                var item = new ModelListItem(model);
                items.Add(item);
                rowItems.Add(item);
                if (rowItems.Count < _columns)
                    continue;

                rows.Add(new ModelListRow(rowItems, _columns));
                rowItems = new List<ModelListItem>(_columns);
            }

            if (rowItems.Count > 0)
                rows.Add(new ModelListRow(rowItems, _columns));
        }


        /// <summary>
        /// Marks the list items of the selected model as selected, a model listed in more than one group has an item in each.
        /// </summary>
        private void UpdateSelection()
        {
            foreach (var item in _modelItems)
                item.IsSelected = item.Model == _selectedItem;
        }


        /// <summary>
        /// Gets the number of items per row that fit the width, each item between the minimum and maximum item width.
        /// </summary>
        /// <param name="width">The width of the list.</param>
        private static int GetColumnCount(double width)
        {
            var maxCount = (int)Math.Floor(width / MinItemWidth);
            var minCount = Math.Max(1, (int)Math.Ceiling(width / MaxItemWidth));
            for (var count = maxCount; count >= minCount; count--)
            {
                var itemWidth = width / count;
                if (itemWidth >= MinItemWidth && itemWidth <= MaxItemWidth)
                    return count;
            }
            return Math.Max(1, maxCount);
        }


        private void Populate()
        {
            FilterPipelines.Clear();
            FilterPipelines.Add(FilterAllPipelines);
            foreach (var pipelineName in Enum.GetValues<PipelineType>().Select(x => x.GetDisplayName()).OrderBy(x => x))
            {
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
            _filterPipeline = FilterAllPipelines;
            RefreshModels();
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
                || _filterPipeline?.Equals(FilterAllPipelines) == false
                || _filterMediaType.HasValue
                || _filterBackendType.HasValue
                || _filterCategoryType.HasValue;
        }


        private async Task DownloadAsync()
        {
            await DownloadService.DownloadAsync(_selectedItem);
        }


        private bool CanDownload()
        {
            return _selectedItem?.Status == ModelStatusType.Unknown
                || _selectedItem?.Status == ModelStatusType.Available
                || HasCheckpointDownload();
        }


        /// <summary>
        /// Determines whether the selected model is installed but has another checkpoint set to download.
        /// </summary>
        private bool HasCheckpointDownload()
        {
            return _selectedItem is IGenerateModel generateModel
                && generateModel.Status == ModelStatusType.Installed
                && generateModel.HasCheckpointSets
                && generateModel.GetCheckpoints().Any(x => !x.IsInstalled);
        }


        private async Task DownloadRetryAsync()
        {
            await DownloadService.QueueAsync(_selectedDownload.DownloadModel);
        }


        private bool CanDownloadRetry()
        {
            return _selectedDownload?.IsFailed == true;
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


        /// <summary>
        /// Queues every model in the list that is not installed, after asking, a generate model downloads its current checkpoint set.
        /// </summary>
        private async Task QueueAllAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            try
            {
                var models = GetQueueAllModels();
                if (models.Count == 0)
                    return;

                if (!await DialogService.ShowMessageAsync(AppDefault.QueueAll, string.Format(AppDefault.QueueAllMessage, models.Count), MessageDialogType.YesNo, MessageBoxIconType.Question, MessageBoxStyleType.Info))
                    return;

                timestamp = Stopwatch.GetTimestamp();
                Logger.LogInformation("[Models] [QueueAll] Queueing {Count} models...", models.Count);
                foreach (var model in models)
                {
                    await DownloadService.QueueAsync(model);
                }
                Logger.LogInformation("[Models] [QueueAll] Queueing {Count} models complete, Elapsed: {Elapsed:c}", models.Count, Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[Models] [QueueAll] An exception occurred queueing models, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
            }
            finally
            {
                RefreshModels();
            }
        }


        /// <summary>
        /// Determines whether the list has a model that is not installed.
        /// </summary>
        private bool CanQueueAll()
        {
            return ModelCollection.Cast<object>().Any(IsQueueAllModel);
        }


        /// <summary>
        /// Gets the models in the list that are not installed, a model listed in more than one group is only returned once.
        /// </summary>
        private List<IDownloadModel> GetQueueAllModels()
        {
            return ModelCollection.SourceCollection
                .OfType<IDownloadModel>()
                .Where(x => ModelCollection.Filter(x) && IsQueueAllModel(x))
                .ToList();
        }


        /// <summary>
        /// Determines whether the model is not installed and can be queued, a model that is queued, downloading or failed is skipped.
        /// </summary>
        /// <param name="item">The list item.</param>
        private static bool IsQueueAllModel(object item)
        {
            return item is IDownloadModel model
                && (model.Status == ModelStatusType.Available || model.Status == ModelStatusType.Unknown);
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
            Settings.ScanModels();
            await Settings.SaveAsync();
        }


        /// <summary>
        /// Scrolls the list by the wheel delta in pixels.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseWheelEventArgs"/> instance containing the event data.</param>
        private void ListBoxControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_scrollViewer is null)
                return;

            _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }


        /// <summary>
        /// Rebuilds the rows when the list width changes the number of items per row.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ScrollChangedEventArgs"/> instance containing the event data.</param>
        private void ListBoxControl_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.OriginalSource is not ScrollViewer scrollViewer || scrollViewer.TemplatedParent != ListBoxControl)
                return;

            _scrollViewer = scrollViewer;
            if (e.ViewportWidthChange == 0)
                return;

            var columns = GetColumnCount(e.ViewportWidth);
            if (columns == _columns)
                return;

            _columns = columns;
            UpdateRows();
        }


        /// <summary>
        /// Selects the model of the clicked list item.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void ModelItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: ModelListItem item })
                SelectedItem = item.Model;
        }


        /// <summary>
        /// A row of the model list, either a group header or a row of items.
        /// </summary>
        public sealed class ModelListRow
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ModelListRow"/> class for a group header.
            /// </summary>
            /// <param name="header">The group name.</param>
            public ModelListRow(string header)
            {
                Header = header;
            }


            /// <summary>
            /// Initializes a new instance of the <see cref="ModelListRow"/> class for a row of items.
            /// </summary>
            /// <param name="items">The items.</param>
            /// <param name="columns">The number of items per row, a short last row keeps the item width.</param>
            public ModelListRow(IReadOnlyList<ModelListItem> items, int columns)
            {
                Items = items;
                Columns = columns;
            }

            public string Header { get; }
            public IReadOnlyList<ModelListItem> Items { get; }
            public int Columns { get; }
        }


        /// <summary>
        /// A model in the model list with its selected state.
        /// </summary>
        public sealed class ModelListItem : BaseModel
        {
            private bool _isSelected;

            /// <summary>
            /// Initializes a new instance of the <see cref="ModelListItem"/> class.
            /// </summary>
            /// <param name="model">The model.</param>
            public ModelListItem(IDownloadModel model)
            {
                Model = model;
            }

            public IDownloadModel Model { get; }

            public bool IsSelected
            {
                get { return _isSelected; }
                set { SetProperty(ref _isSelected, value); }
            }
        }


        /// <summary>
        /// Groups the model list into new, recommended and all models, a model is listed in every group it belongs to.
        /// </summary>
        private sealed class ModelGroupDescription : GroupDescription
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ModelGroupDescription"/> class.
            /// </summary>
            public ModelGroupDescription()
            {
                GroupNames.Add(AppDefault.NewModels);
                GroupNames.Add(AppDefault.RecommendedModels);
                GroupNames.Add(AppDefault.AllModels);
            }


            /// <summary>
            /// Gets the group names for the item.
            /// </summary>
            /// <param name="item">The item.</param>
            /// <param name="level">The group level.</param>
            /// <param name="culture">The culture.</param>
            public override object GroupNameFromItem(object item, int level, CultureInfo culture)
            {
                var groupNames = new List<object>();
                if (item is IGenerateModel generateModel)
                {
                    if (generateModel.IsNew)
                        groupNames.Add(AppDefault.NewModels);
                    if (generateModel.IsRecommended)
                        groupNames.Add(AppDefault.RecommendedModels);
                }
                groupNames.Add(AppDefault.AllModels);
                return groupNames;
            }
        }

    }
}