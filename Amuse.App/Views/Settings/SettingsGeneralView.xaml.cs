using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Dialogs;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for SettingsGeneralView.xaml
    /// </summary>
    public partial class SettingsGeneralView : ViewBase
    {
        private readonly IMigrationService _migrationService;

        public SettingsGeneralView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IHistoryService historyService, IMigrationService migrationService, ILogger<SettingsGeneralView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, historyService, logger)
        {
            _migrationService = migrationService;
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            MoveModelDirectoryCommand = new AsyncRelayCommand(MoveModelDirectoryAsync);
            ModelSearchFoldersCommand = new AsyncRelayCommand(ModelSearchFoldersAsync);
            MoveHistoryDirectoryCommand = new AsyncRelayCommand(MoveHistoryDirectoryAsync);
            MoveTempDirectoryCommand = new AsyncRelayCommand(MoveTempDirectoryAsync);
            OpenDirectoryCommand = new AsyncRelayCommand<string>(OpenDirectoryAsync);
            ResetDialogsCommand = new AsyncRelayCommand(ResetDialogsAsync);
            RescanModelsCommand = new AsyncRelayCommand(RescanModelsAsync);
            InitializeComponent();
        }

        public override View View => View.SettingsGeneral;
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand MoveModelDirectoryCommand { get; }
        public AsyncRelayCommand ModelSearchFoldersCommand { get; }
        public AsyncRelayCommand MoveHistoryDirectoryCommand { get; }
        public AsyncRelayCommand MoveTempDirectoryCommand { get; }
        public AsyncRelayCommand<string> OpenDirectoryCommand { get; }
        public AsyncRelayCommand ResetDialogsCommand { get; }
        public AsyncRelayCommand RescanModelsCommand { get; }
        public IReadOnlyList<ScaleOption> ScaleOptions => ScaleOption.Options;


        private Task OpenDirectoryAsync(string directory)
        {
            URL.NavigateToUrl(directory);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Moves the model folder, downloads write to it so it cannot move while any are queued or running, and every loaded model is unloaded first.
        /// </summary>
        private async Task MoveModelDirectoryAsync()
        {
            if (DownloadService.Queue.Any(x => !x.IsFailed))
            {
                await DialogService.ShowMessageAsync(AppDefault.ModelDownloadingTitle, AppDefault.FolderDownloadingMessage, MessageDialogType.Ok, MessageBoxIconType.Info, MessageBoxStyleType.Info);
                return;
            }

            if (!await ModelAccessService.RequestReleaseAllAsync())
                return;

            var moveDialog = DialogService.GetDialog<MoveFolderDialog>();
            if (await moveDialog.ShowDialogAsync(Settings.DirectoryModel, "Models", AppDefault.Models))
            {
                Settings.SetModelDirectory(moveDialog.DestinationDirectory);
                Settings.NotifyPropertyChanged(nameof(Settings.DirectoryModel));
                await SaveAsync();
                await _migrationService.RunMigrationsAsync();
            }
        }


        /// <summary>
        /// Scans the model folders again, picking up model files that were copied in by hand.
        /// </summary>
        private async Task RescanModelsAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            try
            {
                Logger.LogInformation("[SettingsGeneral] [RescanModels] Rescanning model files...");
                Settings.ScanModels();
                await Settings.SaveAsync();
                var models = Settings.DiffusionModels.Cast<IDownloadModel>()
                    .Concat(Settings.LanguageModels)
                    .Concat(Settings.LoraAdapterModels)
                    .Concat(Settings.ControlNetModels)
                    .Concat(Settings.UpscaleModels)
                    .Concat(Settings.ExtractModels);
                var installed = models.Count(x => x.Status == ModelStatusType.Installed);
                Logger.LogInformation("[SettingsGeneral] [RescanModels] Rescanning model files complete, Installed: {Installed}, Elapsed: {Elapsed:c}", installed, Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowMessageAsync(AppDefault.RescanModels, string.Format(AppDefault.RescanModelsComplete, installed));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[SettingsGeneral] [RescanModels] Rescanning model files failed");
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
            }
        }


        /// <summary>
        /// Edits the model search folders, the models are scanned again when the folders are saved.
        /// </summary>
        private async Task ModelSearchFoldersAsync()
        {
            try
            {
                var searchFoldersDialog = DialogService.GetDialog<ModelSearchFoldersDialog>();
                if (await searchFoldersDialog.ShowDialogAsync())
                    await SaveAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[SettingsGeneral] [ModelSearchFolders] Failed to update the model search folders");
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
            }
        }


        private async Task MoveHistoryDirectoryAsync()
        {
            var moveDialog = DialogService.GetDialog<MoveFolderDialog>();
            if (await moveDialog.ShowDialogAsync(Settings.DirectoryHistory, "History", AppDefault.History))
            {
                Settings.SetHistoryDirectory(moveDialog.DestinationDirectory);
                await HistoryService.InitializeAsync();
                Settings.NotifyPropertyChanged(nameof(Settings.DirectoryHistory));
                await SaveAsync();
            }
        }


        private async Task MoveTempDirectoryAsync()
        {
            var moveDialog = DialogService.GetDialog<MoveFolderDialog>();
            if (await moveDialog.ShowDialogAsync(Settings.DirectoryTemp, "Temp", AppDefault.Temp))
            {
                Settings.SetTempDirectory(moveDialog.DestinationDirectory);
                Settings.NotifyPropertyChanged(nameof(Settings.DirectoryTemp));
                await SaveAsync();
            }
        }


        private async Task ResetDialogsAsync()
        {
            Settings.IsExternalLinksAcknowledged = false;
            Settings.IsExternalToolsAcknowledged = false;
            Settings.IsDangerousToolsAcknowledged = false;
            Settings.IsExecuteToolsAcknowledged = false;
            Settings.IsAppSetupAcknowledged = false;
            await SaveAsync();
        }


        private async Task SaveAsync()
        {
            Settings.ScanModels();
            await Settings.SaveAsync();
        }
    }
}