using Amuse.App.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.WPF.Controls;

namespace Amuse.App.Tools
{
    public sealed class ApplicationTool : ToolCallBase
    {
        private static ApplicationName[] _applications;
        private static readonly HashSet<string> DocumentExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".log", ".csv", ".json", ".pdf",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff",
            ".mp4", ".mkv", ".mov", ".avi", ".webm", ".mp3", ".wav", ".flac", ".ogg", ".m4a"
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationTool"/> class.
        /// </summary>
        public ApplicationTool()
        {
            _applications ??= [.. GetApplications()];
        }

        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public override string Name => "application";

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Start or list installed Windows applications, open a web page in the default browser, open a file in its default application, or show a file or folder in File Explorer.";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => AppDefault.ToolApplication;

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "f40e";

        /// <summary>
        ///  Gets the tool icon style.
        /// </summary>
        public override FontAwesomeIconStyle DisplayIconStyle { get; } = FontAwesomeIconStyle.SharpLight;

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 12;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool only when the user asks to start an application, or to open, view or show a link, file or folder.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "action": {
                            "type": "string",
                            "enum": ["start", "list", "open", "reveal"],
                            "description": "'start' starts an installed application by name, 'list' finds installed applications matching name, 'open' opens a url, file or folder, 'reveal' shows a file or folder selected in File Explorer.",
                            "default": "start"
                        },
                        "name": {
                            "type": "string",
                            "description": "For 'start' and 'list': the application name, or a search term to filter the application list."
                        },
                        "target": {
                            "type": "string",
                            "description": "For 'open' and 'reveal': an http or https url, or the full path of a file or folder."
                        },
                        "arg1": {
                            "type": "string",
                            "description": "For 'start': the first optional argument to pass to the application."
                        },
                        "arg2": {
                            "type": "string",
                            "description": "For 'start': the second optional argument to pass to the application."
                        }
                    },
                    "required": ["action"]
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
                var action = GetArgumentOrDefault("action", "start")?.ToLowerInvariant();
                object result = action switch
                {
                    "list" => ListApplications(GetArgumentOrDefault("name", string.Empty)),
                    "open" => await OpenAsync(settings, GetTarget(), false),
                    "reveal" => await OpenAsync(settings, GetTarget(), true),
                    _ => await StartApplicationAsync(settings, GetArgument<string>("name"), GetArgumentOrDefault<string>("arg1"), GetArgumentOrDefault<string>("arg2"))
                };
                return SuccessResult(result);
            }
            catch (Exception ex)
            {
                return ErrorResult(ex);
            }
        }


        /// <summary>
        /// Lists the applications matching the name.
        /// </summary>
        /// <param name="name">The name filter.</param>
        private static string[] ListApplications(string name)
        {
            return _applications
                .Where(x => string.IsNullOrWhiteSpace(name) || x.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }


        /// <summary>
        /// Starts the application.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="name">The application name.</param>
        /// <param name="arg1">The first argument.</param>
        /// <param name="arg2">The second argument.</param>
        private async Task<ApplicationName> StartApplicationAsync(Settings settings, string name, string arg1, string arg2)
        {
            var application = _applications.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                            ?? _applications.FirstOrDefault(x => x.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                            ?? _applications.FirstOrDefault(x => x.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                            ?? throw new Exception($"Application '{name}' not found. Use the 'list' action to find installed applications.");

            var arguments = string.Join(" ", new[] { arg1, arg2 }.Where(x => !string.IsNullOrEmpty(x)));
            await CheckExecuteAccess(settings, string.Format(AppDefault.ToolActionRunProgram, $"{application.Name} {arguments}".Trim()));

            var processInfo = new ProcessStartInfo
            {
                FileName = application.Shortcut,
                UseShellExecute = true
            };
            if (!string.IsNullOrEmpty(arg1))
                processInfo.ArgumentList.Add(arg1);
            if (!string.IsNullOrEmpty(arg2))
                processInfo.ArgumentList.Add(arg2);

            Process.Start(processInfo);
            return application;
        }


        /// <summary>
        /// Opens a url, file or folder, or reveals a file or folder in File Explorer.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="target">The target.</param>
        /// <param name="isReveal">if set to <c>true</c> reveal in File Explorer.</param>
        private async Task<OpenResult> OpenAsync(Settings settings, string target, bool isReveal)
        {
            // Url
            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = ToolHelper.GetWebUri(target);
                await CheckExecuteAccess(settings, string.Format(AppDefault.ToolActionOpenLink, uri.AbsoluteUri));
                Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
                return new OpenResult(uri.AbsoluteUri, "open", "Url");
            }

            // Path
            var path = ToolHelper.GetLocalPath(target);
            var isDirectory = Directory.Exists(path);
            if (!isDirectory && !File.Exists(path))
                throw new FileNotFoundException($"'{target}' is not a valid url, file or folder.");

            // Windows paths cannot contain quotes, so quoting is safe here
            if (isReveal)
            {
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{path}\"", UseShellExecute = false });
                return new OpenResult(path, "reveal", isDirectory ? "Folder" : "File");
            }

            if (isDirectory)
            {
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{path}\"", UseShellExecute = false });
                return new OpenResult(path, "open", "Folder");
            }

            if (!DocumentExtensions.Contains(Path.GetExtension(path)))
                await CheckExecuteAccess(settings, string.Format(AppDefault.ToolActionRunProgram, path));

            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            return new OpenResult(path, "open", "File");
        }


        /// <summary>
        /// Gets the open or reveal target.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">target</exception>
        private string GetTarget()
        {
            var target = GetArgumentOrDefault<string>("target") ?? GetArgumentOrDefault<string>("name");
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentNullException("target");
            return target.Trim().Trim('"');
        }


        /// <summary>
        /// Gets the application list.
        /// </summary>
        private static IEnumerable<ApplicationName> GetApplications()
        {
            var folders = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            };

            foreach (var folder in folders.Distinct())
            {
                var programs = Path.Combine(folder, "Programs");
                if (!Directory.Exists(programs))
                    continue;

                foreach (var shortcut in Directory.EnumerateFiles(programs, "*.lnk", SearchOption.AllDirectories))
                    yield return new ApplicationName(Path.GetFileNameWithoutExtension(shortcut), shortcut);
            }
        }


        /// <summary>
        /// ApplicationName structure.
        /// </summary>
        private sealed record ApplicationName(string Name, string Shortcut);


        /// <summary>
        /// OpenResult structure.
        /// </summary>
        private sealed record OpenResult(string Target, string Action, string Type);
    }
}
