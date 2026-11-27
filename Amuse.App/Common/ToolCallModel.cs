using Amuse.App.Tools;
using System.Collections.Generic;
using System.Linq;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Common
{
    public class ToolCallModel : BaseModel
    {
        private bool _isEnabled;
        public ToolCallModel() { }
        public ToolCallModel(ToolCallBase toolCall)
        {
            Name = toolCall.Name;
            Icon = toolCall.DisplayIcon;
            IconStyle = toolCall.DisplayIconStyle;
            IsEnabled = toolCall.IsDefault;
            Description = toolCall.Description;
            ToolNames = [toolCall.Name];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolCallModel"/> class for an MCP server, its tools are enabled together.
        /// </summary>
        /// <param name="server">The MCP server.</param>
        public ToolCallModel(McpServerModel server)
        {
            Name = server.Config.Name;
            Icon = server.Icon;
            IconStyle = FontAwesomeIconStyle.SharpLight;
            Description = $"{server.Config.Name}: {server.ToolSummary}";
            ToolNames = [.. server.Tools.Select(x => x.Name)];
            IsServer = true;
        }

        public string Name { get; set; }
        public bool IsServer { get; set; }
        public IReadOnlyList<string> ToolNames { get; set; } = [];
        public string Icon { get; set; }
        public FontAwesomeIconStyle IconStyle { get; set; }
        public string Description { get; set; }
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { SetProperty(ref _isEnabled, value); }
        }
    }
}
