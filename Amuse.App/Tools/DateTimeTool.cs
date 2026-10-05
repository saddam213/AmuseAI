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
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Get the current local date and time, including the day of the week, time zone, UTC time, and UTC offset.";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => true;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => "DateTime Tool";
        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "f017";

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 0;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool when you need accurate current date or time information.",
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
                var result = new DateTimeResult(DateTimeOffset.Now, TimeZoneInfo.Local);
                return Task.FromResult(SuccessResult(result));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ErrorResult(ex));
            }
        }


        /// <summary>
        /// DateTimeResult structure.
        /// </summary>
        private record DateTimeResult(DateTimeOffset LocalTime, TimeZoneInfo TimeZoneInfo)
        {
            public string Date => $"{LocalTime:yyyy-MM-dd}";
            public string Day => $"{LocalTime:dddd}";
            public string Time => $"{LocalTime:HH:mm:ss}";
            public string Utc => $"{LocalTime.ToUniversalTime()}";
        }
    }
}
