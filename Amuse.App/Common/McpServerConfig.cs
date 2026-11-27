using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    /// <summary>
    /// The connection settings of an MCP server, a local command started over stdio or a remote server reached by URL.
    /// </summary>
    public sealed class McpServerConfig : BaseModel
    {
        public const string DefaultIcon = "f1e6";
        private string _name;
        private bool _isEnabled;
        private McpTransportType _transport;

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { SetProperty(ref _isEnabled, value); }
        }

        public McpTransportType Transport
        {
            get { return _transport; }
            set { SetProperty(ref _transport, value); }
        }

        public string Description { get; set; }
        public string Icon { get; set; }
        public string Command { get; set; }
        public List<string> Arguments { get; set; } = [];
        public string WorkingDirectory { get; set; }
        public Dictionary<string, string> Environment { get; set; } = [];
        public string Url { get; set; }
        public Dictionary<string, string> Headers { get; set; } = [];
        public List<string> IncludeTools { get; set; } = [];


        /// <summary>
        /// Creates a copy of the server settings.
        /// </summary>
        public McpServerConfig DeepClone()
        {
            return new McpServerConfig
            {
                Name = Name,
                IsEnabled = IsEnabled,
                Transport = Transport,
                Description = Description,
                Icon = Icon,
                Command = Command,
                Arguments = [.. Arguments ?? []],
                WorkingDirectory = WorkingDirectory,
                Environment = new Dictionary<string, string>(Environment ?? []),
                Url = Url,
                Headers = new Dictionary<string, string>(Headers ?? []),
                IncludeTools = [.. IncludeTools ?? []]
            };
        }


        /// <summary>
        /// Determines whether the text is the code of a Font Awesome icon, a hexadecimal character code such as f1e6.
        /// </summary>
        /// <param name="icon">The icon code.</param>
        public static bool IsValidIcon(string icon)
        {
            if (string.IsNullOrEmpty(icon) || icon.Length < 2 || icon.Length > 5)
                return false;

            return int.TryParse(icon, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code) && code > 0x20 && (code < 0xD800 || code > 0xDFFF);
        }


        /// <summary>
        /// Reads the servers of an MCP configuration, the mcpServers or servers map other MCP applications use, a bare map of servers or a single server.
        /// </summary>
        /// <param name="json">The configuration JSON, the outer braces can be left out.</param>
        /// <exception cref="JsonException">The text is not valid JSON.</exception>
        public static List<McpServerConfig> ParseServers(string json)
        {
            var options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
            var text = (json ?? string.Empty).Trim().TrimEnd(',');
            if (!text.StartsWith('{'))
                text = $"{{{text}}}";

            var servers = new List<McpServerConfig>();
            using (var document = JsonDocument.Parse(text, options))
            {
                var root = document.RootElement;
                if (TryGetObject(root, "mcpServers", out var serverMap) || TryGetObject(root, "servers", out serverMap))
                    root = serverMap;

                if (IsServer(root))
                {
                    servers.Add(ParseServer("MCP Server", root));
                    return servers;
                }

                foreach (var property in root.EnumerateObject().Where(x => IsServer(x.Value)))
                    servers.Add(ParseServer(property.Name, property.Value));
            }
            return servers;
        }


        /// <summary>
        /// Reads a server from its configuration object.
        /// </summary>
        /// <param name="name">The name of the server.</param>
        /// <param name="element">The configuration object.</param>
        private static McpServerConfig ParseServer(string name, JsonElement element)
        {
            var url = GetString(element, "url");
            var config = new McpServerConfig
            {
                Name = name,
                IsEnabled = true,
                Transport = string.IsNullOrEmpty(url) ? McpTransportType.Stdio : McpTransportType.Http,
                Command = GetString(element, "command"),
                WorkingDirectory = GetString(element, "cwd"),
                Url = url
            };

            if (element.TryGetProperty("args", out var arguments) && arguments.ValueKind == JsonValueKind.Array)
                config.Arguments = [.. arguments.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText())];
            if (TryGetObject(element, "env", out var environment))
                config.Environment = GetStrings(environment);
            if (TryGetObject(element, "headers", out var headers))
                config.Headers = GetStrings(headers);

            return config;
        }


        /// <summary>
        /// Determines whether a configuration object is a server, it has a command or a URL.
        /// </summary>
        /// <param name="element">The configuration object.</param>
        private static bool IsServer(JsonElement element)
        {
            return !string.IsNullOrEmpty(GetString(element, "command")) || !string.IsNullOrEmpty(GetString(element, "url"));
        }


        /// <summary>
        /// Gets a string property of a configuration object, null when it is missing or not a string.
        /// </summary>
        /// <param name="element">The configuration object.</param>
        /// <param name="name">The property name.</param>
        private static string GetString(JsonElement element, string name)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();

            return null;
        }


        /// <summary>
        /// Gets the properties of a configuration object as strings.
        /// </summary>
        /// <param name="element">The configuration object.</param>
        private static Dictionary<string, string> GetStrings(JsonElement element)
        {
            return element.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.ValueKind == JsonValueKind.String ? x.Value.GetString() : x.Value.GetRawText());
        }


        /// <summary>
        /// Gets an object property of a configuration object.
        /// </summary>
        /// <param name="element">The configuration object.</param>
        /// <param name="name">The property name.</param>
        /// <param name="value">The object.</param>
        private static bool TryGetObject(JsonElement element, string name, out JsonElement value)
        {
            value = default;
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object;
        }
    }
}
