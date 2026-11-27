using Amuse.App.Resources;
using Amuse.App.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    /// <summary>
    /// An MCP server with its connection state and the tools it provides.
    /// </summary>
    public sealed class McpServerModel : BaseModel
    {
        private McpServerConfig _config;
        private McpServerStatus _status;
        private string _message;
        private IReadOnlyList<McpToolCall> _tools = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="McpServerModel"/> class.
        /// </summary>
        /// <param name="config">The server settings.</param>
        public McpServerModel(McpServerConfig config)
        {
            _config = config;
            _status = config.IsEnabled ? McpServerStatus.Disconnected : McpServerStatus.Disabled;
        }

        /// <summary>
        /// Occurs when the server is switched on or off.
        /// </summary>
        public event EventHandler IsEnabledChanged;

        public McpServerConfig Config
        {
            get { return _config; }
            set
            {
                SetProperty(ref _config, value);
                NotifyPropertyChanged(nameof(Summary));
                NotifyPropertyChanged(nameof(Icon));
                NotifyPropertyChanged(nameof(IsEnabled));
            }
        }

        public bool IsEnabled
        {
            get { return _config.IsEnabled; }
            set
            {
                if (_config.IsEnabled == value)
                    return;

                _config.IsEnabled = value;
                NotifyPropertyChanged();
                IsEnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public McpServerStatus Status
        {
            get { return _status; }
            set
            {
                SetProperty(ref _status, value);
                NotifyPropertyChanged(nameof(StatusText));
                NotifyPropertyChanged(nameof(IsConnected));
                NotifyPropertyChanged(nameof(IsFailed));
            }
        }

        public string Message
        {
            get { return _message; }
            set
            {
                SetProperty(ref _message, value);
                NotifyPropertyChanged(nameof(StatusText));
            }
        }

        public IReadOnlyList<McpToolCall> Tools
        {
            get { return _tools; }
            set
            {
                SetProperty(ref _tools, value ?? []);
                NotifyPropertyChanged(nameof(StatusText));
                NotifyPropertyChanged(nameof(ToolSummary));
            }
        }

        public bool IsConnected => _status == McpServerStatus.Connected;
        public bool IsFailed => _status == McpServerStatus.Failed;
        public string Icon => McpServerConfig.IsValidIcon(_config.Icon) ? _config.Icon : McpServerConfig.DefaultIcon;
        public string ToolSummary => string.Join(", ", _tools.Select(x => x.ToolName));
        public string Summary => _config.Transport == McpTransportType.Http ? _config.Url : string.Join(' ', (_config.Arguments ?? []).Prepend(_config.Command));

        public string StatusText
        {
            get
            {
                return _status switch
                {
                    McpServerStatus.Disabled => AppDefault.McpStatusDisabled,
                    McpServerStatus.Connecting => AppDefault.McpStatusConnecting,
                    McpServerStatus.Connected => string.Format(AppDefault.McpStatusConnected, _tools.Count),
                    McpServerStatus.Failed => string.IsNullOrEmpty(_message) ? AppDefault.McpStatusFailed : _message,
                    _ => AppDefault.McpStatusDisconnected
                };
            }
        }
    }
}
