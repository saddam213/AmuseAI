using Amuse.App.Dialogs;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for SettingsGeneralView.xaml
    /// </summary>
    public partial class SettingsGeneralView : ViewBase
    {
        public SettingsGeneralView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IHistoryService historyService, ILogger<SettingsGeneralView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            MoveModelDirectoryCommand = new AsyncRelayCommand(MoveModelDirectoryAsync);
            MoveHistoryDirectoryCommand = new AsyncRelayCommand(MoveHistoryDirectoryAsync);
            MoveTempDirectoryCommand = new AsyncRelayCommand(MoveTempDirectoryAsync);
            OpenDirectoryCommand = new AsyncRelayCommand<string>(OpenDirectoryAsync);
            ScaleOptions = [.. Enumerable.Range(5, 26).Select(x => new ScaleOption($"{x * 10}%", x / 10.0))];
            InitializeComponent();
        }

        public override View View => View.General;
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand MoveModelDirectoryCommand { get; }
        public AsyncRelayCommand MoveHistoryDirectoryCommand { get; }
        public AsyncRelayCommand MoveTempDirectoryCommand { get; }
        public AsyncRelayCommand<string> OpenDirectoryCommand { get; }
        public IReadOnlyList<ScaleOption> ScaleOptions { get; }


        private Task OpenDirectoryAsync(string directory)
        {
            URL.NavigateToUrl(directory);
            return Task.CompletedTask;
        }


        private async Task MoveModelDirectoryAsync()
        {
            var moveDialog = DialogService.GetDialog<MoveFolderDialog>();
            if (await moveDialog.ShowDialogAsync(Settings.DirectoryModel, "Models"))
            {
                Settings.DirectoryModel = moveDialog.DestinationDirectory;
                Settings.NotifyPropertyChanged(nameof(Settings.DirectoryModel));
                await SaveAsync();
            }
        }


        private async Task MoveHistoryDirectoryAsync()
        {
            var moveDialog = DialogService.GetDialog<MoveFolderDialog>();
            if (await moveDialog.ShowDialogAsync(Settings.DirectoryHistory, "History"))
            {
                Settings.DirectoryHistory = moveDialog.DestinationDirectory;
                await HistoryService.InitializeAsync();
                Settings.NotifyPropertyChanged(nameof(Settings.DirectoryHistory));
                await SaveAsync();
            }
        }


        private async Task MoveTempDirectoryAsync()
        {
            var moveDialog = DialogService.GetDialog<MoveFolderDialog>();
            if (await moveDialog.ShowDialogAsync(Settings.DirectoryTemp, "Temp"))
            {
                Settings.DirectoryTemp = moveDialog.DestinationDirectory;
                Settings.NotifyPropertyChanged(nameof(Settings.DirectoryTemp));
                await SaveAsync();
            }
        }


        private async Task SaveAsync()
        {
            await SettingsManager.SaveAsync(Settings);
            Settings.ScanModels();
        }

        public record ScaleOption(string Label, double Value);
    }
}