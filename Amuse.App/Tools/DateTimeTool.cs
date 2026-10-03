using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amuse.App.Tools
{
    public sealed class DateTimeTool : ToolCallBase
    {
        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public override string Name => "datetime";

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => """
        {
            "type": "function",
            "function": {
                "name": "datetime",
                "description": "Get the current local date and time, including the day of the week, local timezone, UTC time, and UTC offset. Use this tool when you need to know the current date or time or need accurate local time information.",
                "parameters": {
                    "type": "object",
                    "properties": {}
                }
            }
        }
        """;

        /// <summary>
        /// Executes the tool.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public override Task<string> ExecuteAsync(Settings settings, CancellationToken cancellationToken)
        {
            try
            {
                var now = DateTimeOffset.Now;
                var utc = now.ToUniversalTime();
                var timezone = TimeZoneInfo.Local;
                return Task.FromResult($$"""
                {
                    "local": "{{now:yyyy-MM-ddTHH:mm:sszzz}}",
                    "date": "{{now:yyyy-MM-dd}}",
                    "day": "{{now:dddd}}",
                    "time": "{{now:HH:mm:ss}}",
                    "timezone": "{{timezone.Id}}",
                    "timezone_display": "{{timezone.DisplayName}}",
                    "utc": "{{utc:yyyy-MM-ddTHH:mm:ssZ}}"
                }
                """);
            }
            catch (Exception ex)
            {
                return Task.FromResult($"[Error] {Name} tool failed to execute: {ex.Message}");
            }
        }
    }
}
