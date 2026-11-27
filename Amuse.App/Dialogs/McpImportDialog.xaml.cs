// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for McpImportDialog.xaml
    /// </summary>
    public partial class McpImportDialog : DialogControl
    {
        private string _importText;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpImportDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public McpImportDialog(Settings settings)
        {
            Settings = settings;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            OpenFileCommand = new AsyncRelayCommand(OpenFileAsync);
            Errors = new ObservableCollection<string>();
            Servers = new List<McpServerConfig>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand OpenFileCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public List<McpServerConfig> Servers { get; private set; }

        public string ImportText
        {
            get { return _importText; }
            set { SetProperty(ref _importText, value); }
        }


        /// <summary>
        /// Shows the dialog with an empty configuration.
        /// </summary>
        public override Task<bool> ShowDialogAsync()
        {
            ImportText = null;
            Servers = [];
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Loads the configuration from a file, such as the configuration file of another MCP application.
        /// </summary>
        private async Task OpenFileAsync()
        {
            var importPath = await DialogService.OpenFileAsync(AppDefault.Title_McpImport, filter: "JSON |*.json;", defualtExt: "json");
            if (string.IsNullOrEmpty(importPath))
                return;

            try
            {
                ImportText = await File.ReadAllTextAsync(importPath);
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync(AppErrors.ImportErrorTitle, ex.Message);
            }
        }


        /// <summary>
        /// Determines whether the configuration can be imported, and updates the validation errors.
        /// </summary>
        protected override bool CanExecuteSave()
        {
            Errors.Clear();
            Servers = ParseServers();
            if (!string.IsNullOrWhiteSpace(_importText) && Servers.Count == 0)
                Errors.Add(AppErrors.ValidationMcpImportEmpty);

            return Servers.Count > 0 && base.CanExecuteSave();
        }


        /// <summary>
        /// Reads the servers of the configuration, a server with the name of an existing server is given a new name.
        /// </summary>
        private List<McpServerConfig> ParseServers()
        {
            if (string.IsNullOrWhiteSpace(_importText))
                return [];

            try
            {
                var servers = McpServerConfig.ParseServers(_importText);
                var names = Settings.McpServers.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var server in servers)
                {
                    var name = server.Name;
                    for (int index = 2; !names.Add(server.Name); index++)
                        server.Name = $"{name} {index}";
                }
                return servers;
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }
}
