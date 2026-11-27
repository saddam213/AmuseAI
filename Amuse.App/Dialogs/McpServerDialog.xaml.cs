// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for McpServerDialog.xaml
    /// </summary>
    public partial class McpServerDialog : DialogControl
    {
        private static readonly string[] _icons =
        [
            "f1e6", "f0ac", "f002", "f1c0", "f233", "f6ff", "f0c2", "f07b", "f15b", "f15c", "f02d", "f1ea", "f121", "f120", "f126", "f188",
            "f2db", "f544", "f5dc", "f0eb", "f0c3", "f1b2", "f013", "f0ad", "f1ec", "f080", "f201", "f0ce", "f03a", "f133", "f017", "f0e0",
            "f075", "f0c0", "f007", "f03e", "f030", "f03d", "f001", "f279", "f3c5", "f6c4", "f07a", "f09d", "f0b1", "f19c", "f023", "f084",
            "f0c1", "f019", "f02b", "f005", "f0e7", "f11b"
        ];

        private readonly IMcpService _mcpService;
        private readonly ObservableCollection<McpVariableModel> _environmentVariables;
        private readonly ObservableCollection<McpVariableModel> _headerVariables;
        private McpServerConfig _originalServer;
        private string _serverName;
        private string _icon;
        private McpTransportType _transport;
        private string _command;
        private string _arguments;
        private string _url;
        private string _testMessage;
        private bool _isTesting;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpServerDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="mcpService">The MCP service.</param>
        public McpServerDialog(Settings settings, IMcpService mcpService)
        {
            _mcpService = mcpService;
            Settings = settings;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            TestCommand = new AsyncRelayCommand(TestAsync, CanExecuteTest);
            AddVariableCommand = new AsyncRelayCommand(AddVariableAsync);
            RemoveVariableCommand = new AsyncRelayCommand<McpVariableModel>(RemoveVariableAsync);
            _environmentVariables = new ObservableCollection<McpVariableModel>();
            _headerVariables = new ObservableCollection<McpVariableModel>();
            Tools = new ObservableCollection<McpToolOption>();
            Errors = new ObservableCollection<string>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand TestCommand { get; }
        public AsyncRelayCommand AddVariableCommand { get; }
        public AsyncRelayCommand<McpVariableModel> RemoveVariableCommand { get; }
        public ObservableCollection<McpVariableModel> Variables => IsStdio ? _environmentVariables : _headerVariables;
        public ObservableCollection<McpToolOption> Tools { get; }
        public ObservableCollection<string> Errors { get; }
        public McpServerConfig Server { get; private set; }
        public IReadOnlyList<string> Icons => _icons;
        public string IconPreview => McpServerConfig.IsValidIcon(_icon) ? _icon : McpServerConfig.DefaultIcon;
        public bool IsStdio => _transport == McpTransportType.Stdio;
        public string VariablesTitle => IsStdio ? AppDefault.EnvironmentVariables : AppDefault.McpHeaders;

        public string ServerName
        {
            get { return _serverName; }
            set { SetProperty(ref _serverName, value); }
        }

        public string ServerIcon
        {
            get { return _icon; }
            set
            {
                SetProperty(ref _icon, value);
                NotifyPropertyChanged(nameof(IconPreview));
            }
        }

        public McpTransportType Transport
        {
            get { return _transport; }
            set
            {
                SetProperty(ref _transport, value);
                NotifyPropertyChanged(nameof(IsStdio));
                NotifyPropertyChanged(nameof(Variables));
                NotifyPropertyChanged(nameof(VariablesTitle));
            }
        }

        public string Command
        {
            get { return _command; }
            set { SetProperty(ref _command, value); }
        }

        public string Arguments
        {
            get { return _arguments; }
            set { SetProperty(ref _arguments, value); }
        }

        public string Url
        {
            get { return _url; }
            set { SetProperty(ref _url, value); }
        }

        public string TestMessage
        {
            get { return _testMessage; }
            set { SetProperty(ref _testMessage, value); }
        }


        /// <summary>
        /// Shows the dialog to add a server.
        /// </summary>
        public Task<bool> AddAsync()
        {
            return ShowDialogAsync(null, new McpServerConfig { Name = AppDefault.McpServerNew, IsEnabled = true });
        }


        /// <summary>
        /// Shows the dialog to change a server, the server is only changed when the dialog is saved.
        /// </summary>
        /// <param name="server">The server settings.</param>
        public Task<bool> UpdateAsync(McpServerConfig server)
        {
            return ShowDialogAsync(server, server);
        }


        /// <summary>
        /// Shows the dialog with the fields of a server.
        /// </summary>
        /// <param name="originalServer">The server being changed, null when a server is added.</param>
        /// <param name="server">The server settings shown.</param>
        private Task<bool> ShowDialogAsync(McpServerConfig originalServer, McpServerConfig server)
        {
            _originalServer = originalServer;
            Server = server.DeepClone();
            ServerName = server.Name;
            ServerIcon = McpServerConfig.IsValidIcon(server.Icon) ? server.Icon : McpServerConfig.DefaultIcon;
            Transport = server.Transport;
            Command = server.Command;
            Arguments = string.Join(Environment.NewLine, server.Arguments ?? []);
            Url = server.Url;
            TestMessage = null;
            Tools.Clear();
            _environmentVariables.Clear();
            foreach (var variable in server.Environment ?? [])
                _environmentVariables.Add(new McpVariableModel { Name = variable.Key, Value = variable.Value });

            _headerVariables.Clear();
            foreach (var variable in server.Headers ?? [])
                _headerVariables.Add(new McpVariableModel { Name = variable.Key, Value = variable.Value });

            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Connects to the server once and lists its tools, the tools already included stay ticked.
        /// </summary>
        private async Task TestAsync()
        {
            try
            {
                _isTesting = true;
                Tools.Clear();
                TestMessage = AppDefault.McpStatusConnecting;
                var config = CreateServer();
                var result = await _mcpService.TestAsync(config);
                TestMessage = result.Message;
                foreach (var tool in result.Tools)
                {
                    var isIncluded = config.IncludeTools.Count == 0 || config.IncludeTools.Contains(tool.Name, StringComparer.OrdinalIgnoreCase);
                    Tools.Add(new McpToolOption { Name = tool.Name, Description = tool.Description, IsIncluded = isIncluded });
                }
            }
            catch (Exception ex)
            {
                TestMessage = ex.Message;
            }
            finally
            {
                _isTesting = false;
            }
        }


        /// <summary>
        /// Determines whether the server can be tested.
        /// </summary>
        private bool CanExecuteTest()
        {
            if (_isTesting)
                return false;

            return IsStdio ? !string.IsNullOrWhiteSpace(_command) : !string.IsNullOrWhiteSpace(_url);
        }


        /// <summary>
        /// Adds an empty variable row.
        /// </summary>
        private Task AddVariableAsync()
        {
            Variables.Add(new McpVariableModel());
            return Task.CompletedTask;
        }


        /// <summary>
        /// Removes a variable row.
        /// </summary>
        /// <param name="variable">The variable.</param>
        private Task RemoveVariableAsync(McpVariableModel variable)
        {
            Variables.Remove(variable);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Saves the fields to <see cref="Server"/>.
        /// </summary>
        protected override Task SaveAsync()
        {
            Server = CreateServer();
            return base.SaveAsync();
        }


        /// <summary>
        /// Determines whether the server can be saved, and updates the validation errors.
        /// </summary>
        protected override bool CanExecuteSave()
        {
            Errors.Clear();
            foreach (var validationError in GetValidationErrors())
                Errors.Add(validationError);

            return Errors.Count == 0 && !_isTesting && base.CanExecuteSave();
        }


        /// <summary>
        /// Creates the server settings from the fields, the tools are only limited when some of the listed tools are unticked.
        /// </summary>
        private McpServerConfig CreateServer()
        {
            var variables = Variables
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Name.Trim())
                .ToDictionary(x => x.Key, x => x.Last().Value ?? string.Empty);
            var server = Server.DeepClone();
            server.Name = (_serverName ?? string.Empty).Trim();
            server.Icon = (_icon ?? string.Empty).Trim().ToLowerInvariant();
            server.Transport = _transport;
            server.Command = IsStdio ? (_command ?? string.Empty).Trim() : null;
            server.Arguments = IsStdio ? GetArguments() : [];
            server.Environment = IsStdio ? variables : [];
            server.Url = IsStdio ? null : (_url ?? string.Empty).Trim();
            server.Headers = IsStdio ? [] : variables;
            if (Tools.Count > 0)
                server.IncludeTools = Tools.All(x => x.IsIncluded) ? [] : [.. Tools.Where(x => x.IsIncluded).Select(x => x.Name)];

            return server;
        }


        /// <summary>
        /// Gets the arguments, one per line.
        /// </summary>
        private List<string> GetArguments()
        {
            return [.. (_arguments ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        }


        /// <summary>
        /// Gets the validation errors.
        /// </summary>
        private IEnumerable<string> GetValidationErrors()
        {
            var name = (_serverName ?? string.Empty).Trim();
            if (name.Length == 0)
                yield return AppErrors.ValidationMcpNameEmpty;
            else if (Settings.McpServers.Any(x => x != _originalServer && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                yield return AppErrors.ValidationMcpNameExists;

            if (!McpServerConfig.IsValidIcon((_icon ?? string.Empty).Trim()))
                yield return AppErrors.ValidationMcpIconInvalid;
            if (IsStdio && string.IsNullOrWhiteSpace(_command))
                yield return AppErrors.ValidationMcpCommandEmpty;
            if (!IsStdio && (!Uri.TryCreate((_url ?? string.Empty).Trim(), UriKind.Absolute, out var endpoint) || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps)))
                yield return AppErrors.ValidationMcpUrlInvalid;
            if (Tools.Count > 0 && !Tools.Any(x => x.IsIncluded))
                yield return AppErrors.ValidationMcpToolsEmpty;
        }


        /// <summary>
        /// An environment variable or header of a server.
        /// </summary>
        public sealed class McpVariableModel : BaseModel
        {
            private string _name;
            private string _value;

            public string Name
            {
                get { return _name; }
                set { SetProperty(ref _name, value); }
            }

            public string Value
            {
                get { return _value; }
                set { SetProperty(ref _value, value); }
            }
        }


        /// <summary>
        /// A tool of a server that can be included or left out.
        /// </summary>
        public sealed class McpToolOption : BaseModel
        {
            private bool _isIncluded;

            public string Name { get; set; }
            public string Description { get; set; }

            public bool IsIncluded
            {
                get { return _isIncluded; }
                set { SetProperty(ref _isIncluded, value); }
            }
        }
    }
}
