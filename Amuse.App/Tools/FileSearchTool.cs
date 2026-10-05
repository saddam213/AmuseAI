using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Amuse.App.Tools
{
    public sealed class FileSearchTool : ToolCallBase
    {
        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public override string Name => "file_search";

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Search for files on the local computer.";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => "File Search Tool";

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "f865";

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 21;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool when you need to find files by name or search term.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "query": {
                            "type": "string",
                            "description": "The file name or search term to look for."
                        },
                        "path": {
                            "type": "string",
                            "description": "The directory to search. If omitted, search common user folders."
                        },
                        "count": {
                            "type": "integer",
                            "description": "The maximum number of search results to return.",
                            "default": 10
                        }
                    },
                    "required": ["query"]
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
                var query = GetArgument<string>("query");
                var count = GetArgumentOrDefault("count", 10);
                var path = GetArgumentOrDefault<string>("path");
                var results = await SearchFilesAsync(query, path, count, cancellationToken);
                return SuccessResult(results);
            }
            catch (Exception ex)
            {
                return ErrorResult(ex);
            }
        }


        /// <summary>
        /// Search files as an asynchronous operation.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="path">The path.</param>
        /// <param name="maxCount">The maximum count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private static async Task<List<FileSearchResult>> SearchFilesAsync(string query, string path, int maxCount, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var results = new List<FileSearchResult>();
                var directories = string.IsNullOrWhiteSpace(path) ? GetDefaultDirectories() : [path];
                foreach (var directory in directories)
                {
                    if (results.Count >= maxCount)
                        break;
                    SearchDirectory(directory, query, maxCount, results, cancellationToken);
                }
                return results;
            });
        }


        /// <summary>
        /// Searches the directory.
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <param name="query">The query.</param>
        /// <param name="maxCount">The maximum count.</param>
        /// <param name="results">The results.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private static void SearchDirectory(string directory, string query, int maxCount, List<FileSearchResult> results, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(file);
                    if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        var fileInfo = new FileInfo(file);
                        results.Add(new FileSearchResult(name, file, fileInfo.Length, fileInfo.LastWriteTime));
                        if (results.Count >= maxCount)
                            return;
                    }
                }

                foreach (var subdirectory in Directory.EnumerateDirectories(directory))
                {
                    if (results.Count >= maxCount)
                        return;
                    SearchDirectory(subdirectory, query, maxCount, results, cancellationToken);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore directories we cannot access.
            }
            catch (DirectoryNotFoundException)
            {
                // Directory disappeared while searching.
            }
        }


        /// <summary>
        /// Gets the default directories.
        /// </summary>
        private static IEnumerable<string> GetDefaultDirectories()
        {
            yield return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        private record FileSearchResult(string Name, string Path, long Size, DateTime LastModified);
    }
}
