using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Dialogs;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for SettingsToolView.xaml
    /// </summary>
    public partial class SettingsToolView : ViewBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsToolView"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="navigationService">The navigation service.</param>
        /// <param name="downloadService">The download service.</param>
        /// <param name="modelAccessService">The model access service.</param>
        /// <param name="historyService">The history service.</param>
        /// <param name="mcpService">The MCP service.</param>
        /// <param name="logger">The logger.</param>
        public SettingsToolView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IHistoryService historyService, IMcpService mcpService, ILogger<SettingsToolView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, historyService, logger)
        {
            McpService = mcpService;
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            AddServerCommand = new AsyncRelayCommand(AddServerAsync);
            ImportServersCommand = new AsyncRelayCommand(ImportServersAsync);
            EditServerCommand = new AsyncRelayCommand<McpServerModel>(EditServerAsync);
            RemoveServerCommand = new AsyncRelayCommand<McpServerModel>(RemoveServerAsync);
            ReconnectServerCommand = new AsyncRelayCommand<McpServerModel>(ReconnectServerAsync);
            InitializeComponent();
        }

        public override View View => View.SettingsTool;
        public IMcpService McpService { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand AddServerCommand { get; }
        public AsyncRelayCommand ImportServersCommand { get; }
        public AsyncRelayCommand<McpServerModel> EditServerCommand { get; }
        public AsyncRelayCommand<McpServerModel> RemoveServerCommand { get; }
        public AsyncRelayCommand<McpServerModel> ReconnectServerCommand { get; }


        /// <summary>
        /// On View Open, the enabled servers are connected in the background so their status is shown.
        /// </summary>
        /// <param name="args">The arguments.</param>
        public override Task OpenAsync(OpenViewArgs args = null)
        {
            _ = ConnectServersAsync();
            return base.OpenAsync(args);
        }


        /// <summary>
        /// Connects the enabled servers that are not connected yet.
        /// </summary>
        private Task ConnectServersAsync()
        {
            return RunServerOperationAsync("ConnectServers", () => McpService.ConnectAsync());
        }


        /// <summary>
        /// Adds a server and connects it.
        /// </summary>
        private async Task AddServerAsync()
        {
            var serverDialog = DialogService.GetDialog<McpServerDialog>();
            if (!await serverDialog.AddAsync())
                return;

            _ = RunServerOperationAsync("AddServer", () => McpService.AddAsync(serverDialog.Server));
        }


        /// <summary>
        /// Imports the servers of an MCP configuration and connects them.
        /// </summary>
        private async Task ImportServersAsync()
        {
            var importDialog = DialogService.GetDialog<McpImportDialog>();
            if (!await importDialog.ShowDialogAsync())
                return;

            foreach (var server in importDialog.Servers)
                _ = RunServerOperationAsync("ImportServers", () => McpService.AddAsync(server));
        }


        /// <summary>
        /// Changes a server and connects it again.
        /// </summary>
        /// <param name="server">The server.</param>
        private async Task EditServerAsync(McpServerModel server)
        {
            var serverDialog = DialogService.GetDialog<McpServerDialog>();
            if (!await serverDialog.UpdateAsync(server.Config))
                return;

            _ = RunServerOperationAsync("EditServer", () => McpService.UpdateAsync(server, serverDialog.Server));
        }


        /// <summary>
        /// Removes a server after the user confirms.
        /// </summary>
        /// <param name="server">The server.</param>
        private async Task RemoveServerAsync(McpServerModel server)
        {
            if (!await DialogService.ShowMessageAsync(AppDefault.McpRemoveServer, string.Format(AppDefault.McpRemoveServerMessage, server.Config.Name), MessageDialogType.YesNo, MessageBoxIconType.Warning, MessageBoxStyleType.Danger))
                return;

            _ = RunServerOperationAsync("RemoveServer", () => McpService.RemoveAsync(server));
        }


        /// <summary>
        /// Disconnects a server and connects it again.
        /// </summary>
        /// <param name="server">The server.</param>
        private Task ReconnectServerAsync(McpServerModel server)
        {
            _ = RunServerOperationAsync("ReconnectServer", () => McpService.ReconnectAsync(server));
            return Task.CompletedTask;
        }


        /// <summary>
        /// Runs a server operation, the commands do not wait for it as a server can take minutes to start the first time.
        /// </summary>
        /// <param name="name">The operation name.</param>
        /// <param name="operation">The operation.</param>
        private async Task RunServerOperationAsync(string name, Func<Task> operation)
        {
            var timestamp = Stopwatch.GetTimestamp();
            try
            {
                Logger.LogInformation("[SettingsTool] [{Operation}] Running server operation...", name);
                await operation();
                Logger.LogInformation("[SettingsTool] [{Operation}] Server operation complete, Elapsed: {Elapsed:c}", name, Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("[SettingsTool] [{Operation}] Server operation cancelled, Elapsed: {Elapsed:c}", name, Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[SettingsTool] [{Operation}] An exception occurred running the server operation", name);
                await DialogService.ShowErrorAsync(AppErrors.McpServerErrorTitle, ex.Message);
            }
        }


        /// <summary>
        /// Saves the settings.
        /// </summary>
        private async Task SaveAsync()
        {
            await Settings.SaveAsync();
        }
    }
}
