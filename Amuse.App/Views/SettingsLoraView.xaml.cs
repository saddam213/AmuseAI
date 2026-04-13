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
    /// Interaction logic for SettingsLoraView.xaml
    /// </summary>
    public partial class SettingsLoraView : ViewBase
    {
        private LoraAdapterModel _selectedLoraModel;
        private string _filterText;

        public SettingsLoraView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IDownloadService downloadService, IHistoryService historyService, ILogger<SettingsLoraView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            AddLoraModelCommand = new AsyncRelayCommand(AddLoraModelAsync);
            AddLoraModelWizardCommand = new AsyncRelayCommand(AddLoraModelWizardAsync);
            CopyLoraModelCommand = new AsyncRelayCommand(CopyLoraModelAsync, () => SelectedLoraModel is not null);
            UpdateLoraModelCommand = new AsyncRelayCommand(UpdateLoraModelAsync, () => SelectedLoraModel?.Id > Utils.FixedIdRange);
            RemoveLoraModelCommand = new AsyncRelayCommand(RemoveLoraModelAsync, () => SelectedLoraModel?.Id > Utils.FixedIdRange);
            ImportLoraModelCommand = new AsyncRelayCommand(ImportLoraModelAsync);
            ExportLoraModelCommand = new AsyncRelayCommand(ExportLoraModelAsync, () => SelectedLoraModel is not null);
            FilterClearCommand = new AsyncRelayCommand(FilterClearAsync, CanClearFilter);
            ModelCollection = new ListCollectionView(settings.LoraAdapterModels) { Filter = CollectionFilter(), IsLiveSorting = true };
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(LoraAdapterModel.Name), ListSortDirection.Ascending));
            SelectedLoraModel = settings.LoraAdapterModels.FirstOrDefault();
            InitializeComponent();
        }

        public override View View => View.LoraAdapter;
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand AddLoraModelCommand { get; }
        public AsyncRelayCommand AddLoraModelWizardCommand { get; }
        public AsyncRelayCommand CopyLoraModelCommand { get; }
        public AsyncRelayCommand UpdateLoraModelCommand { get; }
        public AsyncRelayCommand RemoveLoraModelCommand { get; }
        public AsyncRelayCommand ImportLoraModelCommand { get; }
        public AsyncRelayCommand ExportLoraModelCommand { get; }
        public AsyncRelayCommand FilterClearCommand { get; }
        public ListCollectionView ModelCollection { get; }

        public LoraAdapterModel SelectedLoraModel
        {
            get { return _selectedLoraModel; }
            set { SetProperty(ref _selectedLoraModel, value); }
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
                if (obj is not LoraAdapterModel model)
                    return false;

                if (!string.IsNullOrEmpty(_filterText))
                {
                    return model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                        || model.Pipeline.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
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


        private async Task AddLoraModelAsync()
        {
            var dialog = DialogService.GetDialog<LoraModelDialog>();
            if (await dialog.AddAsync())
            {
                await SaveAsync();
            }
        }


        private Task AddLoraModelWizardAsync()
        {
            return Task.CompletedTask; //TODO: Lora Wizard
        }


        private async Task CopyLoraModelAsync()
        {
            var dialog = DialogService.GetDialog<LoraModelDialog>();
            if (await dialog.CopyAsync(SelectedLoraModel))
            {
                await SaveAsync();
            }
        }


        private async Task UpdateLoraModelAsync()
        {
            var dialog = DialogService.GetDialog<LoraModelDialog>();
            if (await dialog.UpdateAsync(SelectedLoraModel))
            {
                await SaveAsync();
            }
        }


        private async Task RemoveLoraModelAsync()
        {
            if (await DialogService.ShowMessageAsync("Delete Model", $"Are you sure you want to delete this model?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                Settings.LoraAdapterModels.Remove(SelectedLoraModel);
                SelectedLoraModel = default;
                await SaveAsync();
            }
        }


        private async Task ImportLoraModelAsync()
        {
            var importPath = await DialogService.OpenFileAsync("Import Model", filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(importPath))
            {
                var modelJson = await Json.LoadAsync<LoraAdapterModel>(importPath);
                if (modelJson == null)
                {
                    await DialogService.ShowMessageAsync("Import Error", "Failed to import model file.");
                    return;
                }

                var dialog = DialogService.GetDialog<LoraModelDialog>();
                if (await dialog.ImportAsync(modelJson))
                {
                    await SaveAsync();
                }
            }
        }


        private async Task ExportLoraModelAsync()
        {
            var existingId = _selectedLoraModel.Id;
            try
            {
                _selectedLoraModel.Id = 0;
                var exportPath = await DialogService.SaveFileAsync("Export Model", $"{_selectedLoraModel.Name}.json", filter: "JSON |*.json;", defualtExt: "json");
                if (!string.IsNullOrEmpty(exportPath))
                {
                    await Json.SaveAsync<LoraAdapterModel>(exportPath, _selectedLoraModel);
                }
            }
            finally
            {
                _selectedLoraModel.Id = existingId;
            }
        }


        private async Task SaveAsync()
        {
            await SettingsManager.SaveAsync(Settings);
            Settings.ScanModels();
        }
    }
}