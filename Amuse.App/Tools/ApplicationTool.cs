using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Amuse.App.Tools
{
    public sealed class ApplicationTool : ToolCallBase
    {
        private static ApplicationName[] _applications;

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
        public override string Description => "Manage Windows applications by listing installed applications or starting an application by name.";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => "Application Tool";

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "f40e";

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 20;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool when you need to find available applications or start a Windows application.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "name": {
                            "type": "string",
                            "description": "The application name to start, or a search term to filter the application list."
                        },
                        "action": {
                            "type": "string",
                            "enum": ["start", "list"],
                            "description": "The action to perform: 'list' to find applications or 'start' to start an application.",
                            "default": "start"
                        },
                        "arg1": {
                            "type": "string",
                            "description": "The first optional argument to pass to the application when starting it."
                        },
                        "arg2": {
                            "type": "string",
                            "description": "The second optional argument to pass to the application when starting it."
                        }
                    },
                    "required": ["name"]
                }
            }
        }
        """;

        /// <summary>
        /// Executes the tool.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public override Task<string> ExecuteAsync(Settings settings, CancellationToken cancellationToken = default)
        {
            try
            {
                var name = GetArgument<string>("name");
              
                var action = GetArgumentOrDefault("action", "start");
                                if (action == "list")
                {
                    var applications = _applications
                        .Where(x => x.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.Name)
                        .ToArray();
                    return Task.FromResult(SuccessResult(applications));
                }
                else
                {
                    var application = _applications.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                                    ?? _applications.FirstOrDefault(x => x.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                                    ?? _applications.FirstOrDefault(x => x.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                                    ?? throw new Exception($"Application '{name}' not found.");

                    var arg1 = GetArgumentOrDefault<string>("arg1");
                    var arg2 = GetArgumentOrDefault<string>("arg2");
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
                    return Task.FromResult(SuccessResult(application));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(ErrorResult(ex));
            }
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

        private record ApplicationName(string Name, string Shortcut);
    }
}
