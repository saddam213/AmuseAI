using Amuse.App.Resources;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common.Common;
using TensorStack.WPF.Controls;

namespace Amuse.App.Tools
{
    /// <summary>
    /// A tool of an MCP server, the call is forwarded to the server and its result is flattened to text.
    /// </summary>
    public sealed class McpToolCall : ToolCallBase
    {
        private const int MaxResultChars = 16000;
        private static readonly TimeSpan _callTimeout = TimeSpan.FromSeconds(60);
        private readonly McpClientTool _tool;
        private readonly string _name;
        private readonly string _schema;
        private readonly bool _isReadOnly;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolCall"/> class.
        /// </summary>
        /// <param name="serverName">The name of the server.</param>
        /// <param name="serverKey">The server key, the prefix of the tool name the model sees.</param>
        /// <param name="tool">The server tool.</param>
        public McpToolCall(string serverName, string serverKey, McpClientTool tool)
        {
            _tool = tool;
            _name = $"{serverKey}_{SanitizeName(tool.Name)}";
            _isReadOnly = tool.ProtocolTool.Annotations?.ReadOnlyHint == true;
            _schema = CreateSchema(_name, tool);
            ServerName = serverName;
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolCall"/> class, a call of the tool with its arguments.
        /// </summary>
        /// <param name="toolCall">The tool.</param>
        /// <param name="arguments">The arguments.</param>
        private McpToolCall(McpToolCall toolCall, Dictionary<string, JsonElement> arguments)
        {
            _tool = toolCall._tool;
            _name = toolCall._name;
            _schema = toolCall._schema;
            _isReadOnly = toolCall._isReadOnly;
            ServerName = toolCall.ServerName;
            Arguments = arguments;
        }

        /// <summary>
        /// Gets the name of the server the tool belongs to.
        /// </summary>
        public string ServerName { get; }

        /// <summary>
        /// Gets the name of the tool on the server.
        /// </summary>
        public string ToolName => _tool.Name;

        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public override string Name => _name;

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public override string Description => _tool.Description ?? string.Empty;

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => string.IsNullOrEmpty(_tool.Title) ? _tool.Name : _tool.Title;

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "f1e6";

        /// <summary>
        ///  Gets the tool icon style.
        /// </summary>
        public override FontAwesomeIconStyle DisplayIconStyle { get; } = FontAwesomeIconStyle.SharpLight;

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 1000;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => _schema;


        /// <summary>
        /// Creates a call of the tool from the tool call JSON of the model.
        /// </summary>
        /// <param name="toolCall">The tool call, a JSON object with name and arguments.</param>
        public override ToolCallBase CreateCall(string toolCall)
        {
            var arguments = new Dictionary<string, JsonElement>();
            using (var document = JsonDocument.Parse(toolCall))
            {
                if (document.RootElement.TryGetProperty("arguments", out var element) && element.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in element.EnumerateObject())
                        arguments[property.Name] = property.Value.Clone();
                }
            }
            return new McpToolCall(this, arguments);
        }


        /// <summary>
        /// Executes the tool.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public override async Task<string> ExecuteAsync(Settings settings, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_isReadOnly)
                    await CheckDangerousAccess(settings, string.Format(AppDefault.ToolActionMcpTool, ToolName, ServerName));

                using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutSource.CancelAfter(_callTimeout);
                    var arguments = (Arguments ?? []).ToDictionary(x => x.Key, x => (object)x.Value);
                    var result = await _tool.CallAsync(arguments, cancellationToken: timeoutSource.Token);
                    var content = ToolHelper.Truncate(GetContent(result, settings), MaxResultChars, out _);
                    if (result.IsError == true)
                        return $"[Error] {Name} tool failed to execute, Error: {content}";

                    return content;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ErrorResult(ex);
            }
        }


        /// <summary>
        /// Gets the result of a tool call as text, images and audio are saved to the temp folder and returned as file paths.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="settings">The settings.</param>
        private static string GetContent(CallToolResult result, Settings settings)
        {
            var builder = new StringBuilder();
            foreach (var block in result.Content ?? [])
            {
                var text = GetBlockText(block, settings);
                if (string.IsNullOrEmpty(text))
                    continue;

                if (builder.Length > 0)
                    builder.Append('\n');

                builder.Append(text);
            }

            if (builder.Length == 0 && result.StructuredContent != null)
                builder.Append(JsonSerializer.Serialize(result.StructuredContent));

            return builder.ToString();
        }


        /// <summary>
        /// Gets the text of a content block.
        /// </summary>
        /// <param name="block">The content block.</param>
        /// <param name="settings">The settings.</param>
        private static string GetBlockText(ContentBlock block, Settings settings)
        {
            return block switch
            {
                TextContentBlock textBlock => textBlock.Text,
                ImageContentBlock imageBlock => SaveMedia(imageBlock.DecodedData, imageBlock.MimeType, settings),
                AudioContentBlock audioBlock => SaveMedia(audioBlock.DecodedData, audioBlock.MimeType, settings),
                ResourceLinkBlock linkBlock => $"[Resource: {linkBlock.Name}, Uri: {linkBlock.Uri}]",
                EmbeddedResourceBlock { Resource: TextResourceContents textResource } => textResource.Text,
                EmbeddedResourceBlock { Resource: BlobResourceContents blobResource } => SaveMedia(blobResource.DecodedData, blobResource.MimeType, settings),
                _ => string.Empty
            };
        }


        /// <summary>
        /// Saves media content to the temp folder.
        /// </summary>
        /// <param name="data">The media data.</param>
        /// <param name="mimeType">The MIME type.</param>
        /// <param name="settings">The settings.</param>
        /// <returns>A note with the path of the saved file.</returns>
        private static string SaveMedia(ReadOnlyMemory<byte> data, string mimeType, Settings settings)
        {
            var fileName = FileHelper.RandomFileName(settings.DirectoryTemp, GetExtension(mimeType));
            File.WriteAllBytes(fileName, data.ToArray());
            return $"[File saved: {fileName}, Type: {mimeType}]";
        }


        /// <summary>
        /// Gets the file extension of a MIME type.
        /// </summary>
        /// <param name="mimeType">The MIME type.</param>
        private static string GetExtension(string mimeType)
        {
            return mimeType?.ToLowerInvariant() switch
            {
                "image/png" => "png",
                "image/jpeg" => "jpg",
                "image/webp" => "webp",
                "image/gif" => "gif",
                "audio/wav" or "audio/x-wav" => "wav",
                "audio/mpeg" => "mp3",
                "application/pdf" => "pdf",
                "application/json" => "json",
                "text/plain" => "txt",
                _ => "bin"
            };
        }


        /// <summary>
        /// Creates the OpenAI style function schema of a server tool.
        /// </summary>
        /// <param name="name">The tool name the model sees.</param>
        /// <param name="tool">The server tool.</param>
        private static string CreateSchema(string name, McpClientTool tool)
        {
            var parameters = tool.JsonSchema.ValueKind == JsonValueKind.Object
                ? JsonNode.Parse(tool.JsonSchema.GetRawText()).AsObject()
                : new JsonObject();
            parameters.Remove("$schema");
            if (parameters["type"] is null)
                parameters["type"] = "object";
            if (parameters["properties"] is null)
                parameters["properties"] = new JsonObject();

            var schema = new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = name,
                    ["description"] = tool.Description ?? string.Empty,
                    ["parameters"] = parameters
                }
            };
            return schema.ToJsonString();
        }


        /// <summary>
        /// Replaces the characters a tool name cannot have with underscores.
        /// </summary>
        /// <param name="name">The name.</param>
        public static string SanitizeName(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (var character in name)
            {
                var isValid = char.IsAsciiLetterOrDigit(character) || character == '_' || character == '-';
                builder.Append(isValid ? character : '_');
            }
            return builder.ToString();
        }
    }
}
