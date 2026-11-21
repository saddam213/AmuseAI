using Amuse.App.Resources;
using Amuse.App.Services;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.WPF.Controls;

namespace Amuse.App.Tools
{
    public sealed class FileReadTool : ToolCallBase
    {
        private const long MaxTextFileSize = 20 * 1024 * 1024;
        private const long MaxDocumentFileSize = 100 * 1024 * 1024;
        private const int BinaryCheckSize = 8192;

        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public override string Name => "file_read";

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Read the text content of a local file, including text, code, data, PDF and Word (.docx) documents.";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => "File Read Tool";

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "f15b";

        /// <summary>
        ///  Gets the tool icon style.
        /// </summary>
        public override FontAwesomeIconStyle DisplayIconStyle { get; } = FontAwesomeIconStyle.SharpLight;

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 11;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool when you need to read, summarize or answer questions about a file. Use file_search first if you do not know the full path. For long files, read further by passing the returned NextOffset as offset.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "path": {
                            "type": "string",
                            "description": "The full path of the file to read."
                        },
                        "offset": {
                            "type": "integer",
                            "description": "The character offset to start reading from.",
                            "default": 0
                        },
                        "max_chars": {
                            "type": "integer",
                            "description": "The maximum number of characters to return.",
                            "default": 16000
                        }
                    },
                    "required": ["path"]
                }
            }
        }
        """;

        /// <summary>
        /// Executes the tool.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public override async Task<string> ExecuteAsync(Settings settings, CancellationToken cancellationToken = default)
        {
            try
            {
                // Arguments
                var path = Environment.ExpandEnvironmentVariables(GetArgument<string>("path").Trim().Trim('"'));
                var offset = Math.Max(0, GetArgumentOrDefault("offset", 0));
                var maxChars = Math.Clamp(GetArgumentOrDefault("max_chars", 16000), 500, 100000);

                var fileInfo = new FileInfo(path);
                if (!fileInfo.Exists)
                    throw new FileNotFoundException($"File '{path}' not found.");

                // Access, file contents can be sent on by other tools
                await CheckDangerousAccess(settings, string.Format(AppDefault.ToolActionReadFile, fileInfo.FullName));

                // Read
                var text = await ReadTextAsync(fileInfo, cancellationToken);
                if (offset > text.Length)
                    offset = text.Length;

                // Result
                var content = ToolHelper.Truncate(text[offset..], maxChars, out var isTruncated);
                var nextOffset = isTruncated ? offset + content.Length : (int?)null;
                var result = new FileReadResult(fileInfo.FullName, fileInfo.Length, fileInfo.LastWriteTime, text.Length, offset, nextOffset, content);
                return SuccessResult(result);
            }
            catch (Exception ex)
            {
                return ErrorResult(ex);
            }
        }


        /// <summary>
        /// Reads the file as text.
        /// </summary>
        /// <param name="fileInfo">The file information.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private static async Task<string> ReadTextAsync(FileInfo fileInfo, CancellationToken cancellationToken)
        {
            // Documents
            var extension = fileInfo.Extension.ToLowerInvariant();
            if (extension == ".pdf" || extension == ".docx")
            {
                if (fileInfo.Length > MaxDocumentFileSize)
                    throw new NotSupportedException("The document is too large to read.");

                IDocumentParser parser = extension == ".pdf" ? new PdfDocumentPParser() : new DocxDocumentParser();
                var documentText = await parser.ParseAsync(fileInfo.FullName);
                if (string.IsNullOrWhiteSpace(documentText))
                    throw new Exception("The document contains no readable text (it may be scanned images).");
                return documentText;
            }

            // Text
            if (fileInfo.Length > MaxTextFileSize)
                throw new NotSupportedException("The file is too large to read.");

            using (var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, true))
            {
                if (IsBinary(stream))
                    throw new NotSupportedException($"'{fileInfo.Name}' is a binary file and cannot be read as text.");

                stream.Position = 0;
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    return await reader.ReadToEndAsync(cancellationToken);
                }
            }
        }


        /// <summary>
        /// Determines whether the stream looks like binary data.
        /// </summary>
        /// <param name="stream">The stream.</param>
        private static bool IsBinary(Stream stream)
        {
            var buffer = new byte[BinaryCheckSize];
            var length = stream.Read(buffer, 0, buffer.Length);
            if (length >= 2 && ((buffer[0] == 0xFF && buffer[1] == 0xFE) || (buffer[0] == 0xFE && buffer[1] == 0xFF)))
                return false; // UTF-16 BOM

            for (int i = 0; i < length; i++)
            {
                if (buffer[i] == 0)
                    return true;
            }
            return false;
        }


        /// <summary>
        /// FileReadResult structure.
        /// </summary>
        private sealed record FileReadResult(string Path, long Size, DateTime LastModified, int TotalChars, int Offset, int? NextOffset, string Content);
    }
}
