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
    /// Interaction logic for SettingsExtractView.xaml
    /// </summary>
    public partial class SettingsExtractView : ViewBase
    {
        private ExtractModel _selectedExtractModel;
        private string _filterText;

        public SettingsExtractView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IDownloadService downloadService, IHistoryService historyService, ILogger<SettingsExtractView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            AddExtractModelCommand = new AsyncRelayCommand(AddExtractModelAsync);
            AddExtractModelWizardCommand = new AsyncRelayCommand(AddExtractModelWizardAsync);
            CopyExtractModelCommand = new AsyncRelayCommand(CopyExtractModelAsync, () => SelectedExtractModel is not null);
            UpdateExtractModelCommand = new AsyncRelayCommand(UpdateExtractModelAsync, () => SelectedExtractModel?.Id > Utils.FixedIdRange);
            RemoveExtractModelCommand = new AsyncRelayCommand(RemoveExtractModelAsync, () => SelectedExtractModel?.Id > Utils.FixedIdRange);
            ImportExtractModelCommand = new AsyncRelayCommand(ImportExtractModelAsync);
            ExportExtractModelCommand = new AsyncRelayCommand(ExportExtractModelAsync, () => SelectedExtractModel is not null);
            FilterClearCommand = new AsyncRelayCommand(FilterClearAsync, CanClearFilter);
            ModelCollection = new ListCollectionView(settings.ExtractModels) { Filter = CollectionFilter(), IsLiveSorting = true };
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(ExtractModel.Name), ListSortDirection.Ascending));
            SelectedExtractModel = settings.ExtractModels.FirstOrDefault();
            InitializeComponent();
        }

        public override View View => View.Extract;
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand AddExtractModelCommand { get; }
        public AsyncRelayCommand AddExtractModelWizardCommand { get; }
        public AsyncRelayCommand CopyExtractModelCommand { get; }
        public AsyncRelayCommand UpdateExtractModelCommand { get; }
        public AsyncRelayCommand RemoveExtractModelCommand { get; }
        public AsyncRelayCommand ImportExtractModelCommand { get; }
        public AsyncRelayCommand ExportExtractModelCommand { get; }
        public AsyncRelayCommand FilterClearCommand { get; }
        public ListCollectionView ModelCollection { get; }

        public ExtractModel SelectedExtractModel
        {
            get { return _selectedExtractModel; }
            set { SetProperty(ref _selectedExtractModel, value); }
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
                if (obj is not ExtractModel model)
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


        private async Task AddExtractModelAsync()
        {
            var dialog = DialogService.GetDialog<ExtractModelDialog>();
            if (await dialog.AddAsync())
            {
                await SaveAsync();
            }
        }


        private Task AddExtractModelWizardAsync()
        {
            return Task.CompletedTask; // TODO: Extract Wizard
        }


        private async Task CopyExtractModelAsync()
        {
            var dialog = DialogService.GetDialog<ExtractModelDialog>();
            if (await dialog.CopyAsync(SelectedExtractModel))
            {
                await SaveAsync();
            }
        }


        private async Task UpdateExtractModelAsync()
        {
            var dialog = DialogService.GetDialog<ExtractModelDialog>();
            if (await dialog.UpdateAsync(SelectedExtractModel))
            {
                await SaveAsync();
            }
        }


        private async Task RemoveExtractModelAsync()
        {
            if (await DialogService.ShowMessageAsync("Delete Model", $"Are you sure you want to delete this model?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                Settings.ExtractModels.Remove(SelectedExtractModel);
                SelectedExtractModel = default;
                await SaveAsync();
            }
        }


        private async Task ImportExtractModelAsync()
        {
            var importPath = await DialogService.OpenFileAsync("Import Model", filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(importPath))
            {
                var modelJson = await Json.LoadAsync<ExtractModel>(importPath);
                if (modelJson == null)
                {
                    await DialogService.ShowMessageAsync("Import Error", "Failed to import model file.");
                    return;
                }

                var dialog = DialogService.GetDialog<ExtractModelDialog>();
                if (await dialog.ImportAsync(modelJson))
                {
                    await SaveAsync();
                }
            }
        }


        private async Task ExportExtractModelAsync()
        {
            var existingId = _selectedExtractModel.Id;
            try
            {
                _selectedExtractModel.Id = 0;
                var exportPath = await DialogService.SaveFileAsync("Export Model", $"{_selectedExtractModel.Name}.json", filter: "JSON |*.json;", defualtExt: "json");
                if (!string.IsNullOrEmpty(exportPath))
                {
                    await Json.SaveAsync<ExtractModel>(exportPath, _selectedExtractModel);
                }
            }
            finally
            {
                _selectedExtractModel.Id = existingId;
            }
        }


        private async Task SaveAsync()
        {
            await SettingsManager.SaveAsync(Settings);
            Settings.ScanModels();
        }
    }
}