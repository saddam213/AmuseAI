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
    /// Interaction logic for SettingsUpscaleView.xaml
    /// </summary>
    public partial class SettingsUpscaleView : ViewBase
    {
        private UpscaleModel _selectedUpscaleModel;
        private string _filterText;

        public SettingsUpscaleView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IDownloadService downloadService, IHistoryService historyService, ILogger<SettingsUpscaleView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            AddUpscaleModelCommand = new AsyncRelayCommand(AddUpscaleModel);
            AddUpscaleModelWizardCommand = new AsyncRelayCommand(AddUpscaleModelWizardAsync);
            CopyUpscaleModelCommand = new AsyncRelayCommand(CopyUpscaleModelAsync, () => SelectedUpscaleModel is not null);
            UpdateUpscaleModelCommand = new AsyncRelayCommand(UpdateUpscaleModelAsync, () => SelectedUpscaleModel?.Id > Utils.FixedIdRange);
            RemoveUpscaleModelCommand = new AsyncRelayCommand(RemoveUpscaleModelAsync, () => SelectedUpscaleModel?.Id > Utils.FixedIdRange);
            ImportUpscaleModelCommand = new AsyncRelayCommand(ImportUpscaleModelAsync);
            ExportUpscaleModelCommand = new AsyncRelayCommand(ExportUpscaleModelAsync, () => SelectedUpscaleModel is not null);
            FilterClearCommand = new AsyncRelayCommand(FilterClearAsync, CanClearFilter);
            ModelCollection = new ListCollectionView(settings.UpscaleModels) { Filter = CollectionFilter(), IsLiveSorting = true };
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(UpscaleModel.Name), ListSortDirection.Ascending));
            SelectedUpscaleModel = settings.UpscaleModels.FirstOrDefault();
            InitializeComponent();
        }

        public override View View => View.Upscale;
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand AddUpscaleModelCommand { get; }
        public AsyncRelayCommand AddUpscaleModelWizardCommand { get; }
        public AsyncRelayCommand UpdateUpscaleModelCommand { get; }
        public AsyncRelayCommand CopyUpscaleModelCommand { get; }
        public AsyncRelayCommand RemoveUpscaleModelCommand { get; }
        public AsyncRelayCommand ImportUpscaleModelCommand { get; }
        public AsyncRelayCommand ExportUpscaleModelCommand { get; }
        public AsyncRelayCommand FilterClearCommand { get; }
        public ListCollectionView ModelCollection { get; }

        public UpscaleModel SelectedUpscaleModel
        {
            get { return _selectedUpscaleModel; }
            set { SetProperty(ref _selectedUpscaleModel, value); }
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
                if (obj is not UpscaleModel model)
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


        private async Task AddUpscaleModel()
        {
            var dialog = DialogService.GetDialog<UpscaleModelDialog>();
            if (await dialog.AddAsync())
            {
                await SaveAsync();
            }
        }


        private Task AddUpscaleModelWizardAsync()
        {
            return Task.CompletedTask;  // TODO: Upscale Wizard
        }


        private async Task CopyUpscaleModelAsync()
        {
            var dialog = DialogService.GetDialog<UpscaleModelDialog>();
            if (await dialog.CopyAsync(SelectedUpscaleModel))
            {
                await SaveAsync();
            }
        }


        private async Task UpdateUpscaleModelAsync()
        {
            var dialog = DialogService.GetDialog<UpscaleModelDialog>();
            if (await dialog.UpdateAsync(SelectedUpscaleModel))
            {
                await SaveAsync();
            }
        }


        private async Task RemoveUpscaleModelAsync()
        {
            if (await DialogService.ShowMessageAsync("Delete Model", $"Are you sure you want to delete this model?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                Settings.UpscaleModels.Remove(SelectedUpscaleModel);
                SelectedUpscaleModel = default;
                await SaveAsync();
            }
        }


        private async Task ImportUpscaleModelAsync()
        {
            var importPath = await DialogService.OpenFileAsync("Import Model", filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(importPath))
            {
                var modelJson = await Json.LoadAsync<UpscaleModel>(importPath);
                if (modelJson == null)
                {
                    await DialogService.ShowMessageAsync("Import Error", "Failed to import model file.");
                    return;
                }

                var dialog = DialogService.GetDialog<UpscaleModelDialog>();
                if (await dialog.ImportAsync(modelJson))
                {
                    await SaveAsync();
                }
            }
        }


        private async Task ExportUpscaleModelAsync()
        {
            var existingId = _selectedUpscaleModel.Id;
            try
            {
                _selectedUpscaleModel.Id = 0;
                var exportPath = await DialogService.SaveFileAsync("Export Model", $"{_selectedUpscaleModel.Name}.json", filter: "JSON |*.json;", defualtExt: "json");
                if (!string.IsNullOrEmpty(exportPath))
                {
                    await Json.SaveAsync<UpscaleModel>(exportPath, _selectedUpscaleModel);
                }
            }
            finally
            {
                _selectedUpscaleModel.Id = existingId;
            }
        }


        private async Task SaveAsync()
        {
            await SettingsManager.SaveAsync(Settings);
            Settings.ScanModels();
        }
    }
}