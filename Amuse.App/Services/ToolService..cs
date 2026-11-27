using Amuse.App.Common;
using Amuse.App.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using TensorStack.Common;

namespace Amuse.App.Services
{
    public class ToolService : IToolService
    {
        public const string TagOpen = "<tool_call>";
        public const string TagClose = "</tool_call>";
        private readonly Dictionary<string, ToolCallBase> _toolDefinitions;
        private readonly HttpClient _httpClient;
        private readonly IMcpService _mcpService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="mcpService">The MCP service.</param>
        public ToolService(Settings settings, IMcpService mcpService)
        {
            _mcpService = mcpService;
            _httpClient = ToolHelper.CreateHttpClient(settings.UserAgent);
            _toolDefinitions = RegisterTools();
        }

        /// <summary>
        /// Occurs when the tools of the MCP servers change.
        /// </summary>
        public event EventHandler ToolsChanged
        {
            add { _mcpService.ToolsChanged += value; }
            remove { _mcpService.ToolsChanged -= value; }
        }

        /// <summary>
        /// Gets the tool names.
        /// </summary>
        public IReadOnlyCollection<ToolCallBase> ToolDefinitions => _toolDefinitions.Values;

        /// <summary>
        /// Gets the MCP servers, the tools of a connected server are enabled together.
        /// </summary>
        public ObservableCollection<McpServerModel> McpServers => _mcpService.Servers;


        /// <summary>
        /// Gets the assistant tool calls.
        /// </summary>
        /// <param name="options">The options.</param>
        public string[] GetTools(GenerateInputOptions options)
        {
            return _toolDefinitions.Values
                  .Concat(GetMcpTools())
                  .Where(x => options.SelectedTools.Contains(x.Name))
                  .Select(x => x.Schema)
                  .ToArray();
        }


        /// <summary>
        /// Connects the MCP servers that are enabled and not connected yet.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return _mcpService.ConnectAsync(cancellationToken);
        }


        /// <summary>
        /// Determines whether the response contains tool calls
        /// </summary>
        /// <param name="response">The response.</param>
        public bool IsToolResponse(IReadOnlyList<TextInput> response)
        {
            if (response.Count == 0)
                return false;

            var textResult = response[0];
            return textResult.Text.Contains(TagClose);
        }


        /// <summary>
        /// Parses the response.
        /// </summary>
        /// <param name="response">The response.</param>
        /// <param name="selectedTools">The tools the user enabled, calls to any other tool are rejected.</param>
        public ToolResult ParseResponse(IReadOnlyList<TextInput> response, IReadOnlyCollection<string> selectedTools)
        {
            if (response.Count == 0)
                return null;

            var textResult = response[0];
            var parsedToolCalls = ParseToolCalls(textResult.Text);
            if (parsedToolCalls.Count == 0)
                return null;

            var toolCalls = new string[parsedToolCalls.Count];
            var tools = new ToolCallBase[parsedToolCalls.Count];
            for (int i = 0; i < parsedToolCalls.Count; i++)
            {
                var parsedToolCall = parsedToolCalls[i];
                toolCalls[i] = parsedToolCall;
                tools[i] = DeserializeToolCall(parsedToolCall, selectedTools);
            }
            return new ToolResult(toolCalls, tools);
        }


        /// <summary>
        /// Deserializes the tool call.
        /// </summary>
        /// <param name="toolCall">The tool call.</param>
        /// <param name="selectedTools">The tools the user enabled.</param>
        /// <returns>ToolCallBase.</returns>
        /// <exception cref="System.Exception">Tool is unknown or not enabled.</exception>
        private ToolCallBase DeserializeToolCall(string toolCall, IReadOnlyCollection<string> selectedTools)
        {
            var toolDefinition = JsonSerializer.Deserialize<ToolDefinition>(toolCall, Json.DefaultOptions);
            var toolRegistration = GetToolRegistration(toolDefinition?.Name, selectedTools);
            if (toolRegistration == null)
                throw new Exception($"Tool '{toolDefinition?.Name}' is not a known tool.");

            if (selectedTools == null || !selectedTools.Contains(toolRegistration.Name))
                throw new Exception($"Tool '{toolRegistration.Name}' is not enabled.");

            var toolCallImplementation = toolRegistration.CreateCall(toolCall);
            toolCallImplementation.HttpClient = _httpClient;
            return toolCallImplementation;
        }


        /// <summary>
        /// Gets the registered tool with the name, a built-in tool or a tool of a connected MCP server.
        /// A small model can call an MCP tool by the name it has on its server, without the server prefix, that name is accepted when only one enabled tool has it.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="selectedTools">The tools the user enabled.</param>
        private ToolCallBase GetToolRegistration(string name, IReadOnlyCollection<string> selectedTools)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (_toolDefinitions.TryGetValue(name, out var toolRegistration))
                return toolRegistration;

            var mcpTools = GetMcpTools().ToArray();
            var mcpTool = mcpTools.FirstOrDefault(x => x.Name == name);
            if (mcpTool != null || selectedTools == null)
                return mcpTool;

            var serverTools = mcpTools
                .Where(x => string.Equals(x.ToolName, name, StringComparison.OrdinalIgnoreCase) && selectedTools.Contains(x.Name))
                .ToArray();
            return serverTools.Length == 1 ? serverTools[0] : null;
        }


        /// <summary>
        /// Gets the tools of the connected MCP servers, a tool with the name of a built-in tool is left out.
        /// </summary>
        private IEnumerable<McpToolCall> GetMcpTools()
        {
            return _mcpService.GetTools().Where(x => !_toolDefinitions.ContainsKey(x.Name));
        }


        /// <summary>
        /// Parses the tool calls.
        /// </summary>
        /// <param name="content">The content.</param>
        private static List<string> ParseToolCalls(string content)
        {
            var toolCalls = new List<string>();
            if (string.IsNullOrWhiteSpace(content))
                return toolCalls;

            int position = 0;
            while (true)
            {
                int start = content.IndexOf(TagOpen, position, StringComparison.Ordinal);
                if (start == -1)
                    break;

                start += TagOpen.Length;
                int end = content.IndexOf(TagClose, start, StringComparison.Ordinal);
                if (end == -1)
                    break;

                string toolCall = NormalizeToolCall(content[start..end].Trim());
                if (toolCall != null)
                    toolCalls.Add(toolCall);

                position = end + TagClose.Length;
            }
            return toolCalls;
        }


        /// <summary>
        /// Registers the tools.
        /// </summary>
        private static Dictionary<string, ToolCallBase> RegisterTools()
        {
            return Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ToolCallBase).IsAssignableFrom(t))
                .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (ToolCallBase)Activator.CreateInstance(t))
                .ToDictionary(k => k.Name, v => v);
        }


        /// <summary>
        /// Normalizes the tool call format if required.
        /// </summary>
        /// <param name="toolCall">The tool call.</param>
        private static string NormalizeToolCall(string toolCall)
        {
            if (toolCall.StartsWith("<function"))
                return ParseQwenToolCall(toolCall);
            else if (toolCall.StartsWith("call:"))
                return ParseGemma4ToolCall(toolCall);
            return toolCall;
        }


        /// <summary>
        /// Parses the Qwen tool call.
        /// </summary>
        /// <param name="toolCall">The tool call.</param>
        /// <returns>System.String.</returns>
        private static string ParseQwenToolCall(string toolCall)
        {
            var xml = XElement.Parse(RegexManager.ReplaceUnNamedXmlKeys(toolCall));
            var name = xml.Attribute("name").Value;
            var arguments = new Dictionary<string, JsonElement>();
            foreach (var parameter in xml.Elements("parameter"))
            {
                var parameterName = parameter.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(parameterName))
                    continue;

                arguments[parameterName] = parameter.ToJsonElement();
            }
            return JsonSerializer.Serialize(new { name, arguments }, Json.DefaultOptions);
        }


        /// <summary>
        /// Parses the Gemma4 tool call.
        /// </summary>
        /// <param name="toolCall">The tool call.</param>
        /// <returns>System.String.</returns>
        private static string ParseGemma4ToolCall(string toolCall)
        {
            var match = RegexManager.ParseGemma4ToolCall(toolCall);
            if (!match.Success)
                return toolCall;

            var name = match.Groups["name"].Value;
            var rawArgs = match.Groups["args"].Value;
            var fixedArgs = RegexManager.ReplaceUnquotedKeys(rawArgs);
            var arguments = string.IsNullOrEmpty(fixedArgs) || fixedArgs == "{}"
                ? new object()
                : JsonSerializer.Deserialize<object>(fixedArgs, Json.DefaultOptions);
            return JsonSerializer.Serialize(new { name, arguments }, Json.DefaultOptions);
        }

        private record ToolDefinition(string Name);
    }


    public record ToolResult(string[] ToolCalls, ToolCallBase[] Tools)
    {
        public bool IsEmpty => ToolCalls.IsNullOrEmpty();
    };


    public interface IToolService
    {
        event EventHandler ToolsChanged;
        IReadOnlyCollection<ToolCallBase> ToolDefinitions { get; }
        ObservableCollection<McpServerModel> McpServers { get; }
        string[] GetTools(GenerateInputOptions options);
        Task ConnectAsync(CancellationToken cancellationToken = default);
        bool IsToolResponse(IReadOnlyList<TextInput> response);
        ToolResult ParseResponse(IReadOnlyList<TextInput> response, IReadOnlyCollection<string> selectedTools);
    }
}
