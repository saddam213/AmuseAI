using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.WPF.Controls;

namespace Amuse.App.Tools
{
    public sealed class FileSearchTool : ToolCallBase
    {
        private const int MaxCount = 100;
        private static readonly TimeSpan FileSystemTimeout = TimeSpan.FromSeconds(15);
        private static readonly HashSet<string> SkipDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules", ".git", ".svn", ".hg", ".vs", ".vscode", ".idea", "bin", "obj", "__pycache__",
            ".venv", "venv", ".cache", ".nuget", "AppData", "$RECYCLE.BIN", "System Volume Information"
        };
        private static readonly Dictionary<string, string[]> FileTypes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "image", [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".heic", ".svg", ".ico"] },
            { "video", [".mp4", ".mkv", ".mov", ".avi", ".wmv", ".webm", ".m4v", ".flv"] },
            { "audio", [".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma", ".opus"] },
            { "document", [".pdf", ".doc", ".docx", ".txt", ".md", ".rtf", ".odt", ".xls", ".xlsx", ".csv", ".ppt", ".pptx"] },
            { "code", [".cs", ".py", ".js", ".ts", ".json", ".xml", ".xaml", ".html", ".css", ".cpp", ".h", ".java", ".go", ".rs", ".ps1", ".sh"] },
            { "archive", [".zip", ".7z", ".rar", ".tar", ".gz"] },
            { "model", [".onnx", ".safetensors", ".gguf", ".ckpt", ".pt", ".pth", ".bin"] }
        };

        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public override string Name => "file_search";

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Search for files on the local computer by name, type, or text content.";

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
        ///  Gets the tool icon style.
        /// </summary>
        public override FontAwesomeIconStyle DisplayIconStyle { get; } = FontAwesomeIconStyle.SharpLight;

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 10;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool to find files the user mentions. Returns full paths that can be passed to the file_read tool, or to the application tool to open or reveal them. Results are ranked best match first, then most recently modified.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "query": {
                            "type": "string",
                            "description": "Words that must all appear in the file name (e.g. 'holiday invoice'), or a wildcard pattern (e.g. '*.pdf', 'report_202?*'). Use an empty string to match any name when filtering by type or extension."
                        },
                        "type": {
                            "type": "string",
                            "enum": ["any", "image", "video", "audio", "document", "code", "archive", "model"],
                            "description": "Restrict results to a file type.",
                            "default": "any"
                        },
                        "extensions": {
                            "type": "string",
                            "description": "Optional comma separated list of file extensions to include, e.g. 'pdf,docx'."
                        },
                        "content": {
                            "type": "boolean",
                            "description": "Also match the query against the text inside files (uses the Windows Search index).",
                            "default": false
                        },
                        "path": {
                            "type": "string",
                            "description": "The directory to search. If omitted, the user's folders (Desktop, Documents, Downloads, Pictures, Videos, Music, OneDrive) are searched."
                        },
                        "count": {
                            "type": "integer",
                            "description": "The maximum number of results to return.",
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
                // Arguments
                var query = GetArgumentOrDefault("query", string.Empty)?.Trim() ?? string.Empty;
                var count = Math.Clamp(GetArgumentOrDefault("count", 10), 1, MaxCount);
                var path = GetArgumentOrDefault<string>("path");
                var content = GetArgumentOrDefault("content", false);
                var extensions = GetExtensions(GetArgumentOrDefault("type", "any"), GetArgumentOrDefault<string>("extensions"));
                if (query.Length == 0 && extensions.Length == 0)
                    throw new ArgumentException("A query, type or extensions filter is required.");

                // Roots
                var roots = GetRoots(path);
                if (roots.Length == 0)
                    throw new DirectoryNotFoundException($"Directory '{path}' not found.");

                var request = new SearchRequest(query, GetTerms(query), extensions, content, roots, count);
                var result = await Task.Run(() =>
                {
                    // Windows Search index
                    var indexResults = SearchIndex(request, cancellationToken);
                    if (indexResults != null && (indexResults.Count >= request.Count || request.Content))
                        return new FileSearchResult("WindowsSearch", indexResults);

                    // File system, merged with any index results (folders may not be indexed)
                    var fileResults = SearchFileSystem(request, cancellationToken);
                    if (indexResults == null || indexResults.Count == 0)
                        return new FileSearchResult("FileSystem", fileResults);

                    var merged = Rank([.. indexResults, .. fileResults], request).Take(request.Count).ToList();
                    return new FileSearchResult("WindowsSearch+FileSystem", merged);
                }, cancellationToken);

                return SuccessResult(result);
            }
            catch (Exception ex)
            {
                return ErrorResult(ex);
            }
        }


        /// <summary>
        /// Searches the Windows Search index.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The results, or null if the index is unavailable.</returns>
        private static List<FileItem> SearchIndex(SearchRequest request, CancellationToken cancellationToken)
        {
            dynamic connection = null;
            dynamic recordset = null;
            try
            {
                var connectionType = Type.GetTypeFromProgID("ADODB.Connection");
                if (connectionType == null)
                    return null;

                connection = Activator.CreateInstance(connectionType);
                connection.Open("Provider=Search.CollatorDSO;Extended Properties='Application=Windows';");
                recordset = connection.Execute(BuildIndexQuery(request));

                var results = new List<FileItem>();
                while (!(bool)recordset.EOF)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var filePath = GetFieldValue(recordset, "System.ItemPathDisplay") as string;
                    if (!string.IsNullOrEmpty(filePath) && !IsSkipped(filePath))
                    {
                        var size = GetFieldValue(recordset, "System.Size");
                        var modified = GetFieldValue(recordset, "System.DateModified");
                        results.Add(new FileItem
                        (
                            Path.GetFileName(filePath),
                            filePath,
                            size == null ? 0 : Convert.ToInt64(size),
                            modified is DateTime dateTime ? dateTime.ToLocalTime() : DateTime.MinValue
                        ));
                    }
                    recordset.MoveNext();
                }

                // Rank name matches above content matches
                return Rank(results, request).Take(request.Count).ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileSearch] Windows Search unavailable: {ex.Message}");
                return null;
            }
            finally
            {
                try
                {
                    if (recordset != null)
                    {
                        recordset.Close();
                        Marshal.FinalReleaseComObject(recordset);
                    }
                    if (connection != null)
                    {
                        connection.Close();
                        Marshal.FinalReleaseComObject(connection);
                    }
                }
                catch
                {
                    // Ignore COM clean-up errors
                }
            }
        }


        /// <summary>
        /// Builds the Windows Search SQL query.
        /// </summary>
        /// <param name="request">The request.</param>
        private static string BuildIndexQuery(SearchRequest request)
        {
            var conditions = new List<string>();

            // Scope
            conditions.Add($"({string.Join(" OR ", request.Roots.Select(x => $"SCOPE='file:{EscapeSql(x)}'"))})");
            conditions.Add("System.ItemType <> 'Directory'");

            // Extensions (Windows Search SQL does not support IN)
            if (request.Extensions.Length > 0)
                conditions.Add($"({string.Join(" OR ", request.Extensions.Select(x => $"System.FileExtension = '{EscapeSql(x)}'"))})");

            // Name and content
            var isWildcard = IsWildcard(request.Query);
            if (isWildcard)
            {
                conditions.Add($"System.FileName LIKE '{EscapeSql(EscapeLike(request.Query).Replace('*', '%').Replace('?', '_'))}'");
            }
            else if (request.Terms.Length > 0)
            {
                var nameMatch = string.Join(" AND ", request.Terms.Select(x => $"System.FileName LIKE '%{EscapeSql(EscapeLike(x))}%'"));
                if (request.Content)
                {
                    var contentMatch = string.Join(" AND ", request.Terms.Select(x => $"CONTAINS(*, '\"{EscapeSql(x.Replace("\"", string.Empty))}*\"')"));
                    conditions.Add($"(({nameMatch}) OR ({contentMatch}))");
                }
                else
                {
                    conditions.Add($"({nameMatch})");
                }
            }

            var orderBy = request.Content ? "System.Search.Rank DESC" : "System.DateModified DESC";
            var top = Math.Min(request.Count * 5, 500);
            return $"SELECT TOP {top} System.ItemPathDisplay, System.Size, System.DateModified FROM SYSTEMINDEX WHERE {string.Join(" AND ", conditions)} ORDER BY {orderBy}";
        }


        /// <summary>
        /// Searches the file system.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private static List<FileItem> SearchFileSystem(SearchRequest request, CancellationToken cancellationToken)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var maxCandidates = Math.Max(request.Count * 10, 200);
            var isWildcard = IsWildcard(request.Query);
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
            };

            var results = new List<FileItem>();
            foreach (var root in request.Roots)
            {
                var enumerable = new FileSystemEnumerable<FileItem>(root, (ref FileSystemEntry entry) => new FileItem(entry.FileName.ToString(), entry.ToFullPath(), entry.Length, entry.LastWriteTimeUtc.LocalDateTime), options)
                {
                    ShouldRecursePredicate = (ref FileSystemEntry entry) => !SkipDirectories.Contains(entry.FileName.ToString()),
                    ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory && IsMatch(entry.FileName, request, isWildcard)
                };

                foreach (var item in enumerable)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    results.Add(item);
                    if (results.Count >= maxCandidates || Stopwatch.GetElapsedTime(timestamp) > FileSystemTimeout)
                        break;
                }

                if (results.Count >= maxCandidates || Stopwatch.GetElapsedTime(timestamp) > FileSystemTimeout)
                    break;
            }
            return Rank(results, request).Take(request.Count).ToList();
        }


        /// <summary>
        /// Determines whether the file name matches the request.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="request">The request.</param>
        /// <param name="isWildcard">if set to <c>true</c> the query is a wildcard pattern.</param>
        private static bool IsMatch(ReadOnlySpan<char> fileName, SearchRequest request, bool isWildcard)
        {
            if (request.Extensions.Length > 0)
            {
                var extension = Path.GetExtension(fileName);
                var hasExtension = false;
                foreach (var item in request.Extensions)
                {
                    if (extension.Equals(item, StringComparison.OrdinalIgnoreCase))
                    {
                        hasExtension = true;
                        break;
                    }
                }
                if (!hasExtension)
                    return false;
            }

            if (isWildcard)
                return FileSystemName.MatchesSimpleExpression(request.Query, fileName, true);

            foreach (var term in request.Terms)
            {
                if (!fileName.Contains(term, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }


        /// <summary>
        /// Ranks the results by match quality then modified date.
        /// </summary>
        /// <param name="results">The results.</param>
        /// <param name="request">The request.</param>
        private static IEnumerable<FileItem> Rank(List<FileItem> results, SearchRequest request)
        {
            var query = NormalizeName(request.Query);
            return results
                .DistinctBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => Score(x.Name, query, request.Terms))
                .ThenByDescending(x => x.LastModified);
        }


        /// <summary>
        /// Scores the file name against the query.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="query">The normalized query.</param>
        /// <param name="terms">The terms.</param>
        private static int Score(string fileName, string query, string[] terms)
        {
            if (query.Length == 0)
                return 0;

            var name = NormalizeName(Path.GetFileNameWithoutExtension(fileName));
            if (name.Equals(query, StringComparison.OrdinalIgnoreCase))
                return 4;
            if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return 3;
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                return 2;
            if (terms.All(x => fileName.Contains(x, StringComparison.OrdinalIgnoreCase)))
                return 1;
            return 0;
        }


        /// <summary>
        /// Gets the search terms.
        /// </summary>
        /// <param name="query">The query.</param>
        private static string[] GetTerms(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || IsWildcard(query))
                return [];

            return query
                .Split([' ', '_', '-', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }


        /// <summary>
        /// Gets the extension filter.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="extensions">The extensions.</param>
        private static string[] GetExtensions(string type, string extensions)
        {
            var results = new List<string>();
            if (!string.IsNullOrWhiteSpace(type) && FileTypes.TryGetValue(type, out var typeExtensions))
                results.AddRange(typeExtensions);

            if (!string.IsNullOrWhiteSpace(extensions))
            {
                results.AddRange(extensions
                    .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => x.TrimStart('*'))
                    .Select(x => x.StartsWith('.') ? x : $".{x}"));
            }
            return results.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }


        /// <summary>
        /// Gets the root directories to search.
        /// </summary>
        /// <param name="path">The path.</param>
        private static string[] GetRoots(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var directory = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
                return Directory.Exists(directory) ? [Path.GetFullPath(directory)] : [];
            }

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var directories = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(userProfile, "Downloads"),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Environment.GetEnvironmentVariable("OneDrive")
            };

            // Remove missing and nested folders (e.g. Documents redirected into OneDrive)
            var existing = directories
                .Where(x => !string.IsNullOrEmpty(x) && Directory.Exists(x))
                .Select(x => Path.TrimEndingDirectorySeparator(Path.GetFullPath(x)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return existing
                .Where(x => !existing.Any(parent => !parent.Equals(x, StringComparison.OrdinalIgnoreCase) && x.StartsWith($"{parent}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }


        /// <summary>
        /// Gets a recordset field value.
        /// </summary>
        /// <param name="recordset">The recordset.</param>
        /// <param name="name">The field name.</param>
        private static object GetFieldValue(dynamic recordset, string name)
        {
            object value = recordset.Fields.Item(name).Value;
            return value is DBNull ? null : value;
        }


        /// <summary>
        /// Determines whether the path is inside a skipped directory.
        /// </summary>
        /// <param name="path">The path.</param>
        private static bool IsSkipped(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                return false;

            return directory
                .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
                .Any(SkipDirectories.Contains);
        }


        /// <summary>
        /// Determines whether the query is a wildcard pattern.
        /// </summary>
        /// <param name="query">The query.</param>
        private static bool IsWildcard(string query)
        {
            return !string.IsNullOrEmpty(query) && (query.Contains('*') || query.Contains('?'));
        }


        /// <summary>
        /// Normalizes a name for comparison.
        /// </summary>
        /// <param name="name">The name.</param>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            var builder = new StringBuilder(name.Length);
            foreach (var character in name)
                builder.Append(character == '_' || character == '-' || character == '.' ? ' ' : character);
            return builder.ToString().Trim();
        }


        /// <summary>
        /// Escapes a value for a Windows Search SQL string literal.
        /// </summary>
        /// <param name="value">The value.</param>
        private static string EscapeSql(string value)
        {
            return value.Replace("'", "''");
        }


        /// <summary>
        /// Escapes LIKE wildcard characters.
        /// </summary>
        /// <param name="value">The value.</param>
        private static string EscapeLike(string value)
        {
            return value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        }


        /// <summary>
        /// SearchRequest structure.
        /// </summary>
        private sealed record SearchRequest(string Query, string[] Terms, string[] Extensions, bool Content, string[] Roots, int Count);


        /// <summary>
        /// FileItem structure.
        /// </summary>
        private sealed record FileItem(string Name, string Path, long Size, DateTime LastModified);


        /// <summary>
        /// FileSearchResult structure.
        /// </summary>
        private sealed record FileSearchResult(string Source, List<FileItem> Files);
    }
}
