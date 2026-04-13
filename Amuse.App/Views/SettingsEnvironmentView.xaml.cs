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
    /// Interaction logic for SettingsEnvironmentView.xaml
    /// </summary>
    public partial class SettingsEnvironmentView : ViewBase
    {
        private EnvironmentModel _selectedEnvironment;
        private string _filterText;

        public SettingsEnvironmentView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IDownloadService downloadService, IHistoryService historyService, ILogger<SettingsEnvironmentView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            AddEnvironmentCommand = new AsyncRelayCommand(AddEnvironmentAsync);
            AddEnvironmentWizardCommand = new AsyncRelayCommand(AddEnvironmentWizardAsync);
            CopyEnvironmentCommand = new AsyncRelayCommand(CopyEnvironmentAsync, () => SelectedEnvironment is not null);
            UpdateEnvironmentCommand = new AsyncRelayCommand(UpdateEnvironmentAsync);
            RemoveEnvironmentCommand = new AsyncRelayCommand(RemoveEnvironmentAsync, () => SelectedEnvironment?.Id > Utils.FixedIdRange);
            ImportEnvironmentCommand = new AsyncRelayCommand(ImportEnvironmentAsync);
            ExportEnvironmentCommand = new AsyncRelayCommand(ExportEnvironmentAsync, () => SelectedEnvironment is not null);
            EnvironmentServiceCreateCommand = new AsyncRelayCommand(EnvironmentCreateAsync, CanEnvironmentCreate);
            EnvironmentServiceUpdateCommand = new AsyncRelayCommand(EnvironmentUpdateAsync, CanEnvironmentUpdate);
            EnvironmentServiceRebuildCommand = new AsyncRelayCommand(EnvironmentRebuildAsync, CanEnvironmentUpdate);
            EnvironmentServiceDeleteCommand = new AsyncRelayCommand(EnvironmentDeleteAsync, CanEnvironmentUpdate);
            FilterClearCommand = new AsyncRelayCommand(FilterClearAsync, CanClearFilter);
            ModelCollection = new ListCollectionView(settings.Environments) { Filter = CollectionFilter(), IsLiveSorting = true };
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(EnvironmentModel.Id), ListSortDirection.Ascending));
            SelectedEnvironment = settings.Environments.FirstOrDefault();
            InitializeComponent();
        }

        public override View View => View.Environment;
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand AddEnvironmentCommand { get; }
        public AsyncRelayCommand AddEnvironmentWizardCommand { get; }
        public AsyncRelayCommand CopyEnvironmentCommand { get; }
        public AsyncRelayCommand UpdateEnvironmentCommand { get; }
        public AsyncRelayCommand RemoveEnvironmentCommand { get; }
        public AsyncRelayCommand ImportEnvironmentCommand { get; }
        public AsyncRelayCommand ExportEnvironmentCommand { get; }
        public AsyncRelayCommand EnvironmentServiceCreateCommand { get; }
        public AsyncRelayCommand EnvironmentServiceUpdateCommand { get; }
        public AsyncRelayCommand EnvironmentServiceRebuildCommand { get; }
        public AsyncRelayCommand EnvironmentServiceDeleteCommand { get; }
        public AsyncRelayCommand FilterClearCommand { get; }
        public ListCollectionView ModelCollection { get; }

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


        public override Task OpenAsync(OpenViewArgs args = null)
        {
            return base.OpenAsync(args);
        }


        private Predicate<object> CollectionFilter()
        {
            return (obj) =>
            {
                if (obj is not EnvironmentModel model)
                    return false;

                if (!Settings.Vendors.Contains(model.Vendor))
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


        private async Task AddEnvironmentAsync()
        {
            var dialog = DialogService.GetDialog<EnvironmentModelDialog>();
            if (await dialog.AddAsync())
            {
                await SaveAsync();
            }
        }


        private Task AddEnvironmentWizardAsync()
        {
            return Task.CompletedTask; // TODO: Environment Wizard
        }


        private async Task CopyEnvironmentAsync()
        {
            var dialog = DialogService.GetDialog<EnvironmentModelDialog>();
            if (await dialog.CopyAsync(_selectedEnvironment))
            {
                await SaveAsync();
            }
        }


        private async Task UpdateEnvironmentAsync()
        {
            var dialog = DialogService.GetDialog<EnvironmentModelDialog>();
            if (await dialog.UpdateAsync(_selectedEnvironment))
            {
                await SaveAsync();
            }
        }


        private async Task RemoveEnvironmentAsync()
        {
            if (await DialogService.ShowMessageAsync("Delete Environment", $"Are you sure you want to delete this environment?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                await EnvironmentService.DeleteAsync(_selectedEnvironment);
                Settings.Environments.Remove(_selectedEnvironment);
                SelectedEnvironment = Settings.Environments.FirstOrDefault();
                await SaveAsync();
            }
        }


        private async Task ImportEnvironmentAsync()
        {
            var importPath = await DialogService.OpenFileAsync("Import Environment", filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(importPath))
            {
                var environmentJson = await Json.LoadAsync<EnvironmentModel>(importPath);
                if (environmentJson == null)
                {
                    await DialogService.ShowMessageAsync("Import Error", "Failed to import Environment file.");
                    return;
                }

                var dialog = DialogService.GetDialog<EnvironmentModelDialog>();
                if (await dialog.ImportAsync(environmentJson))
                {
                    await SaveAsync();
                }
            }
        }


        private async Task ExportEnvironmentAsync()
        {
            var existingId = _selectedEnvironment.Id;
            try
            {
                _selectedEnvironment.Id = 0;
                var exportPath = await DialogService.SaveFileAsync("Export Environment", $"{_selectedEnvironment.Name}.json", filter: "JSON |*.json;", defualtExt: "json");
                if (!string.IsNullOrEmpty(exportPath))
                {
                    await Json.SaveAsync<EnvironmentModel>(exportPath, _selectedEnvironment);
                }
            }
            finally
            {
                _selectedEnvironment.Id = existingId;
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


        private async Task EnvironmentRebuildAsync()
        {
            var environmentDialog = DialogService.GetDialog<EnvironmentDialog>();
            await environmentDialog.RebuildAsync(SelectedEnvironment);
        }


        private async Task EnvironmentDeleteAsync()
        {
            if (await DialogService.ShowMessageAsync("Delete Environment", $"Are you sure you want to delete this environment?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                await EnvironmentService.DeleteAsync(SelectedEnvironment);
            }
        }


        private async Task SaveAsync()
        {
            await SettingsManager.SaveAsync(Settings);
            Settings.ScanModels();
        }
    }
}