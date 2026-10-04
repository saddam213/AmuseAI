using Amuse.App.Common;
using Amuse.App.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using TensorStack.Common;
using TensorStack.WPF.Services;

namespace Amuse.App.Services
{
    public class ToolService : IToolService
    {
        public const string TagOpen = "<tool_call>";
        public const string TagClose = "</tool_call>";
        private readonly Dictionary<string, ToolCallBase> _toolDefinitions;
        private readonly IHttpService _httpService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolService"/> class.
        /// </summary>
        public ToolService(IHttpService httpService)
        {
            _httpService = httpService;
            _toolDefinitions = RegisterTools();
        }

        /// <summary>
        /// Gets the tool names.
        /// </summary>
        public IReadOnlyCollection<ToolCallBase> ToolDefinitions => _toolDefinitions.Values;


        /// <summary>
        /// Gets the assistant tool calls.
        /// </summary>
        /// <param name="options">The options.</param>
        public string[] GetTools(GenerateInputOptions options)
        {
            return _toolDefinitions
                  .Where(x => options.SelectedTools.Contains(x.Key))
                  .Select(x => x.Value.Schema)
                  .ToArray();
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
        public ToolResult ParseResponse(IReadOnlyList<TextInput> response)
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
                tools[i] = DeserializeToolCall(parsedToolCall);
            }
            return new ToolResult(toolCalls, tools);
        }


        /// <summary>
        /// Deserializes the tool call.
        /// </summary>
        /// <param name="toolCall">The tool call.</param>
        /// <returns>ToolCallBase.</returns>
        /// <exception cref="System.Exception"></exception>
        private ToolCallBase DeserializeToolCall(string toolCall)
        {
            var toolDefinition = JsonSerializer.Deserialize<ToolDefinition>(toolCall, Json.DefaultOptions);
            if (toolDefinition == null || !_toolDefinitions.TryGetValue(toolDefinition.Name, out var toolRegistration))
                throw new Exception();

            var toolCallImplementation = (ToolCallBase)JsonSerializer.Deserialize(toolCall, toolRegistration.GetType(), Json.DefaultOptions);
            toolCallImplementation.HttpClient = _httpService.Client;
            return toolCallImplementation;
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

                string toolCall = content[start..end].Trim();
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
                .Select(t => (ToolCallBase)Activator.CreateInstance(t))
                .ToDictionary(k => k.Name, v => v);
        }

        private record ToolDefinition(string Name);
    }


    public record ToolResult(string[] ToolCalls, ToolCallBase[] Tools)
    {
        public bool IsEmpty => ToolCalls.IsNullOrEmpty();
    };


    public interface IToolService
    {
        IReadOnlyCollection<ToolCallBase> ToolDefinitions { get; }
        string[] GetTools(GenerateInputOptions options);
        bool IsToolResponse(IReadOnlyList<TextInput> response);
        ToolResult ParseResponse(IReadOnlyList<TextInput> response);
    }
}
