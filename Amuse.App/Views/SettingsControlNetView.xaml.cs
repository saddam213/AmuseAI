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
    /// Interaction logic for SettingsControlNetView.xaml
    /// </summary>
    public partial class SettingsControlNetView : ViewBase
    {
        private ControlNetModel _selectedControlNetModel;
        private string _filterText;

        public SettingsControlNetView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IDownloadService downloadService, IHistoryService historyService, ILogger<SettingsControlNetView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            AddControlNetModelCommand = new AsyncRelayCommand(AddControlNetModelAsync);
            AddControlNetModelWizardCommand = new AsyncRelayCommand(AddControlNetModelWizardAsync);
            CopyControlNetModelCommand = new AsyncRelayCommand(CopyControlNetModelAsync, () => SelectedControlNetModel is not null);
            UpdateControlNetModelCommand = new AsyncRelayCommand(UpdateControlNetModelAsync, () => SelectedControlNetModel?.Id > Utils.FixedIdRange);
            RemoveControlNetModelCommand = new AsyncRelayCommand(RemoveControlNetModelAsync, () => SelectedControlNetModel?.Id > Utils.FixedIdRange);
            ImportControlNetModelCommand = new AsyncRelayCommand(ImportControlNetModelAsync);
            ExportControlNetModelCommand = new AsyncRelayCommand(ExportControlNetModelAsync, () => SelectedControlNetModel is not null);
            FilterClearCommand = new AsyncRelayCommand(FilterClearAsync, CanClearFilter);
            ModelCollection = new ListCollectionView(settings.ControlNetModels) { Filter = CollectionFilter() };
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(ControlNetModel.Name), ListSortDirection.Ascending));
            SelectedControlNetModel = settings.ControlNetModels.FirstOrDefault();
            InitializeComponent();
        }

        public override View View => View.ControlNet;
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand AddControlNetModelCommand { get; }
        public AsyncRelayCommand AddControlNetModelWizardCommand { get; }
        public AsyncRelayCommand CopyControlNetModelCommand { get; }
        public AsyncRelayCommand UpdateControlNetModelCommand { get; }
        public AsyncRelayCommand RemoveControlNetModelCommand { get; }
        public AsyncRelayCommand ImportControlNetModelCommand { get; }
        public AsyncRelayCommand ExportControlNetModelCommand { get; }
        public AsyncRelayCommand FilterClearCommand { get; }
        public ListCollectionView ModelCollection { get; }

        public ControlNetModel SelectedControlNetModel
        {
            get { return _selectedControlNetModel; }
            set { SetProperty(ref _selectedControlNetModel, value); }
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
                if (obj is not ControlNetModel model)
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


        private async Task AddControlNetModelAsync()
        {
            var dialog = DialogService.GetDialog<ControlNetModelDialog>();
            if (await dialog.AddAsync())
            {
                await SaveAsync();
            }
        }


        private Task AddControlNetModelWizardAsync()
        {
            return Task.CompletedTask; //TODO: Model Wizard
        }


        private async Task CopyControlNetModelAsync()
        {
            var dialog = DialogService.GetDialog<ControlNetModelDialog>();
            if (await dialog.CopyAsync(SelectedControlNetModel))
            {
                await SaveAsync();
            }
        }


        private async Task UpdateControlNetModelAsync()
        {
            var dialog = DialogService.GetDialog<ControlNetModelDialog>();
            if (await dialog.UpdateAsync(SelectedControlNetModel))
            {
                await SaveAsync();
            }
        }


        private async Task RemoveControlNetModelAsync()
        {
            if (await DialogService.ShowMessageAsync("Delete Model", $"Are you sure you want to delete this model?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                Settings.ControlNetModels.Remove(SelectedControlNetModel);
                SelectedControlNetModel = default;
                await SaveAsync();
            }
        }


        private async Task ImportControlNetModelAsync()
        {
            var importPath = await DialogService.OpenFileAsync("Import Model", filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(importPath))
            {
                var modelJson = await Json.LoadAsync<ControlNetModel>(importPath);
                if (modelJson == null)
                {
                    await DialogService.ShowMessageAsync("Import Error", "Failed to import model file.");
                    return;
                }

                var dialog = DialogService.GetDialog<ControlNetModelDialog>();
                if (await dialog.ImportAsync(modelJson))
                {
                    await SaveAsync();
                }
            }
        }


        private async Task ExportControlNetModelAsync()
        {
            var existingId = _selectedControlNetModel.Id;
            try
            {
                _selectedControlNetModel.Id = 0;
                var exportPath = await DialogService.SaveFileAsync("Export Model", $"{_selectedControlNetModel.Name}.json", filter: "JSON |*.json;", defualtExt: "json");
                if (!string.IsNullOrEmpty(exportPath))
                {
                    await Json.SaveAsync<ControlNetModel>(exportPath, _selectedControlNetModel);
                }
            }
            finally
            {
                _selectedControlNetModel.Id = existingId;
            }
        }


        private async Task SaveAsync()
        {
            await SettingsManager.SaveAsync(Settings);
            Settings.ScanModels();
        }
    }
}