using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for SettingsEnvironmentView.xaml
    /// </summary>
    public partial class SettingsEnvironmentView : ViewBase
    {
        private EnvironmentModel _selectedEnvironment;
        private string _filterText;
        private FilterOption _filterBackend;
        private FilterOption _filterVendor;
        private FilterOption _filterType;
        private bool _isDeviceFilterEnabled = true;

        public SettingsEnvironmentView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IHistoryService historyService, IEnvironmentService environmentService, ILogger<SettingsEnvironmentView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, historyService, logger)
        {
            EnvironmentService = environmentService;
            FilterBackends = [FilterOption.ShowAll(), .. Enum.GetValues<BackendType>().Where(x => x != BackendType.PyTorch && x != BackendType.HuggingFace && x != BackendType.OnnxRuntime).Select(x => FilterOption.FromEnum(x))];
            FilterVendors = [FilterOption.ShowAll(), .. Enum.GetValues<VendorType>().Where(x => x != VendorType.CPU).Select(x => FilterOption.FromEnum(x))];
            FilterTypes = [FilterOption.ShowAll(), .. Enum.GetValues<EnvironmentType>().Select(x => FilterOption.FromEnum(x))];
            _filterBackend = FilterBackends[0];
            _filterVendor = FilterVendors[0];
            _filterType = FilterTypes[0];
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            AddEnvironmentCommand = new AsyncRelayCommand(AddEnvironmentAsync);
            CopyEnvironmentCommand = new AsyncRelayCommand(CopyEnvironmentAsync, () => SelectedEnvironment is not null);
            UpdateEnvironmentCommand = new AsyncRelayCommand(UpdateEnvironmentAsync);
            RemoveEnvironmentCommand = new AsyncRelayCommand(RemoveEnvironmentAsync, () => SelectedEnvironment?.Id > Utils.FixedIdRange);
            ImportEnvironmentCommand = new AsyncRelayCommand(ImportEnvironmentAsync);
            ExportEnvironmentCommand = new AsyncRelayCommand(ExportEnvironmentAsync, () => SelectedEnvironment is not null);
            EnvironmentServiceCreateCommand = new AsyncRelayCommand(EnvironmentCreateAsync, CanEnvironmentCreate);
            EnvironmentServiceUpdateCommand = new AsyncRelayCommand(EnvironmentUpdateAsync, CanEnvironmentUpdate);
            EnvironmentServiceDeleteCommand = new AsyncRelayCommand(EnvironmentDeleteAsync, CanEnvironmentUpdate);
            EnvironmentDefaultCommand = new AsyncRelayCommand(EnvironmentDefaultAsync, CanEnvironmentDefault);
            FilterClearCommand = new AsyncRelayCommand(FilterClearAsync, CanClearFilter);
            ManageEnvironmentsCommand = new AsyncRelayCommand(ManageEnvironmentsAsync);
            ModelCollection = new ListCollectionView(settings.Environments) { Filter = CollectionFilter(), IsLiveSorting = true, IsLiveFiltering = true, IsLiveGrouping = true };
            ModelCollection.GroupDescriptions.Add(new PropertyGroupDescription(nameof(EnvironmentModel.Vendor)));
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(EnvironmentModel.Vendor), ListSortDirection.Ascending));
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(EnvironmentModel.Backend), ListSortDirection.Ascending));
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(EnvironmentModel.Name), ListSortDirection.Ascending));
            ModelCollection.MoveCurrentToFirst();
            SelectedEnvironment = ModelCollection.CurrentItem as EnvironmentModel;
            InitializeComponent();
        }

        public override View View => View.SettingsEnvironment;
        public IEnvironmentService EnvironmentService { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand AddEnvironmentCommand { get; }
        public AsyncRelayCommand CopyEnvironmentCommand { get; }
        public AsyncRelayCommand UpdateEnvironmentCommand { get; }
        public AsyncRelayCommand RemoveEnvironmentCommand { get; }
        public AsyncRelayCommand ImportEnvironmentCommand { get; }
        public AsyncRelayCommand ExportEnvironmentCommand { get; }
        public AsyncRelayCommand EnvironmentServiceCreateCommand { get; }
        public AsyncRelayCommand EnvironmentServiceUpdateCommand { get; }
        public AsyncRelayCommand EnvironmentServiceDeleteCommand { get; }
        public AsyncRelayCommand EnvironmentDefaultCommand { get; }
        public AsyncRelayCommand FilterClearCommand { get; }
        public AsyncRelayCommand ManageEnvironmentsCommand { get; }
        public ListCollectionView ModelCollection { get; }

        /// <summary>
        /// Gets the backend filter options.
        /// </summary>
        public FilterOption[] FilterBackends { get; }

        /// <summary>
        /// Gets the vendor filter options.
        /// </summary>
        public FilterOption[] FilterVendors { get; }

        /// <summary>
        /// Gets the environment type filter options.
        /// </summary>
        public FilterOption[] FilterTypes { get; }

        public EnvironmentModel SelectedEnvironment
        {
            get { return _selectedEnvironment; }
            set { SetProperty(ref _selectedEnvironment, value); }
        }

        public string FilterText
        {
            get { return _filterText; }
            set { SetProperty(ref _filterText, value); ModelCollection?.Refresh(); }
        }

        /// <summary>
        /// Gets or sets the backend filter.
        /// </summary>
        public FilterOption FilterBackend
        {
            get { return _filterBackend; }
            set { SetProperty(ref _filterBackend, value); ModelCollection?.Refresh(); }
        }

        /// <summary>
        /// Gets or sets the vendor filter.
        /// </summary>
        public FilterOption FilterVendor
        {
            get { return _filterVendor; }
            set { SetProperty(ref _filterVendor, value); ModelCollection?.Refresh(); }
        }

        /// <summary>
        /// Gets or sets the environment type filter.
        /// </summary>
        public FilterOption FilterType
        {
            get { return _filterType; }
            set { SetProperty(ref _filterType, value); ModelCollection?.Refresh(); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether only environments usable by the installed devices are shown.
        /// </summary>
        public bool IsDeviceFilterEnabled
        {
            get { return _isDeviceFilterEnabled; }
            set { SetProperty(ref _isDeviceFilterEnabled, value); ModelCollection?.Refresh(); }
        }


        public override Task OpenAsync(OpenViewArgs args = null)
        {
            // Devices may be detected after construction
            ModelCollection.Refresh();
            if (SelectedEnvironment is null || !ModelCollection.Contains(SelectedEnvironment))
            {
                ModelCollection.MoveCurrentToFirst();
                SelectedEnvironment = ModelCollection.CurrentItem as EnvironmentModel;
            }
            return base.OpenAsync(args);
        }


        private Predicate<object> CollectionFilter()
        {
            return (obj) =>
            {
                if (obj is not EnvironmentModel model)
                    return false;

                // Devices
                if (_isDeviceFilterEnabled && !IsDeviceSupported(model))
                    return false;

                // Backend
                if (Enum.TryParse<BackendType>(_filterBackend?.Value, out var backendType) && model.Backend != backendType)
                    return false;

                // Vendor
                if (Enum.TryParse<VendorType>(_filterVendor?.Value, out var vendorType) && model.Vendor != vendorType)
                    return false;

                // Type
                if (Enum.TryParse<EnvironmentType>(_filterType?.Value, out var environmentType) && model.Type != environmentType)
                    return false;

                // Text
                if (!string.IsNullOrEmpty(_filterText))
                    return model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase);

                return true;
            };
        }


        /// <summary>
        /// Determines whether any installed device can use the environment.
        /// </summary>
        /// <param name="model">The environment model.</param>
        private bool IsDeviceSupported(EnvironmentModel model)
        {
            // CPU environments run on any machine
            if (model.Vendor == VendorType.CPU)
                return true;

            return (Settings.Devices ?? [])
                .Where(x => x.Vendor == model.Vendor)
                .Where(x => x.SupportedBackends is null || x.SupportedBackends.Contains(model.Backend))
                .Any(x => model.Type != EnvironmentType.Device || x.HardwareID == model.Device);
        }


        private Task FilterClearAsync()
        {
            FilterText = null;
            FilterBackend = FilterBackends[0];
            FilterVendor = FilterVendors[0];
            FilterType = FilterTypes[0];
            return Task.CompletedTask;
        }


        private bool CanClearFilter()
        {
            return !string.IsNullOrWhiteSpace(_filterText)
                || _filterBackend != FilterBackends[0]
                || _filterVendor != FilterVendors[0]
                || _filterType != FilterTypes[0];
        }


        /// <summary>
        /// Opens the environment setup to install or remove backend environments.
        /// </summary>
        private async Task ManageEnvironmentsAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            try
            {
                Logger.LogInformation("[SettingsEnvironment] [ManageEnvironments] Opening environment setup...");
                var setupDialog = DialogService.GetDialog<AppSetupDialog>();
                await setupDialog.ShowDialogAsync();
                Logger.LogInformation("[SettingsEnvironment] [ManageEnvironments] Environment setup closed, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[SettingsEnvironment] [ManageEnvironments] Environment setup failed");
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
            }
            finally
            {
                ModelCollection.Refresh();
            }
        }


        private async Task AddEnvironmentAsync()
        {
            var dialog = DialogService.GetDialog<EnvironmentModelDialog>();
            if (await dialog.AddAsync())
            {
                await SaveAsync();
                SelectedEnvironment = dialog.EnvironmentModel;
            }
        }


        private async Task CopyEnvironmentAsync()
        {
            var dialog = DialogService.GetDialog<EnvironmentModelDialog>();
            if (await dialog.CopyAsync(_selectedEnvironment))
            {
                await SaveAsync();
                SelectedEnvironment = dialog.EnvironmentModel;
            }
        }


        private async Task UpdateEnvironmentAsync()
        {
            var dialog = DialogService.GetDialog<EnvironmentModelDialog>();
            if (await dialog.UpdateAsync(_selectedEnvironment))
            {
                await SaveAsync();
                SelectedEnvironment = dialog.EnvironmentModel;
            }
        }


        private async Task RemoveEnvironmentAsync()
        {
            if (await DialogService.ShowMessageAsync(AppDefault.RemoveEnvironment, AppDefault.RemoveEnvironmentMessage, TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                if (!await ModelAccessService.RequestEnvironmentReleaseAsync(_selectedEnvironment))
                    return;

                await EnvironmentService.DeleteAsync(_selectedEnvironment);
                Settings.Environments.Remove(_selectedEnvironment);
                SelectedEnvironment = Settings.Environments.FirstOrDefault();
                Settings.Vendors = [.. Settings.Environments.Select(x => x.Vendor).Distinct()];
                await SaveAsync();
                ModelCollection.Refresh();
            }
        }


        private async Task ImportEnvironmentAsync()
        {
            var importPath = await DialogService.OpenFileAsync(AppDefault.ImportEnvironment, filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(importPath))
            {
                var environmentImports = await Json.LoadArrayAsync<EnvironmentModel>(importPath);
                if (environmentImports.IsNullOrEmpty())
                {
                    await DialogService.ShowErrorAsync(AppErrors.ImportErrorTitle, AppErrors.ImportEnvironmentFailed);
                    return;
                }

                var dialog = DialogService.GetDialog<EnvironmentModelDialog>();
                if (await dialog.ImportAsync(environmentImports))
                {
                    Settings.Vendors = [.. Settings.Environments.Select(x => x.Vendor).Distinct()];
                    await SaveAsync();
                    ModelCollection.Refresh();
                }
            }
        }


        private async Task ExportEnvironmentAsync()
        {
            var exportPath = await DialogService.SaveFileAsync(AppDefault.ExportEnvironment, $"{_selectedEnvironment.Name}.json", filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(exportPath))
            {
                await Json.SaveAsync<EnvironmentModel>(exportPath, _selectedEnvironment.DeepClone(0));
            }
        }


        private async Task EnvironmentCreateAsync()
        {
            var environmentDialog = DialogService.GetDialog<EnvironmentDialog>();
            await environmentDialog.CreateAsync(SelectedEnvironment);
        }


        private bool CanEnvironmentCreate()
        {
            if (SelectedEnvironment is null)
                return false;

            return !EnvironmentService.Exists(SelectedEnvironment);
        }


        private async Task EnvironmentUpdateAsync()
        {
            var environmentDialog = DialogService.GetDialog<EnvironmentDialog>();
            await environmentDialog.UpdateAsync(SelectedEnvironment);
        }


        private bool CanEnvironmentUpdate()
        {
            if (SelectedEnvironment is null)
                return false;

            return EnvironmentService.Exists(SelectedEnvironment);
        }


        private async Task EnvironmentDeleteAsync()
        {
            if (await DialogService.ShowMessageAsync(AppDefault.DeleteEnvironment, AppDefault.DeleteEnvironmentMessage, TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                if (!await ModelAccessService.RequestEnvironmentReleaseAsync(SelectedEnvironment))
                    return;

                await EnvironmentService.DeleteAsync(SelectedEnvironment);
            }
        }


        private async Task EnvironmentDefaultAsync()
        {
            if (SelectedEnvironment is null)
                return;

            await EnvironmentService.SetDefaultAsync(SelectedEnvironment);
        }


        private bool CanEnvironmentDefault()
        {
            return SelectedEnvironment is not null && !SelectedEnvironment.IsDefault;
        }



        private async Task SaveAsync()
        {
            Settings.ScanModels();
            await Settings.SaveAsync();
        }
    }
}