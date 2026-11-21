using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.WPF.Controls;

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
        ///  Gets the tool icon style.
        /// </summary>
        public override FontAwesomeIconStyle DisplayIconStyle { get; } = FontAwesomeIconStyle.SharpLight;

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
        public override Task<string> ExecuteAsync(Settings settings, CancellationToken cancellationToken = default)
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
        private sealed record DateTimeResult
        {
            public DateTimeResult(DateTimeOffset localTime, TimeZoneInfo timeZone)
            {
                Date = localTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                Day = localTime.ToString("dddd", CultureInfo.InvariantCulture);
                Time = localTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                TimeZone = timeZone.IsDaylightSavingTime(localTime) ? timeZone.DaylightName : timeZone.StandardName;
                UtcOffset = localTime.ToString("zzz", CultureInfo.InvariantCulture);
                Utc = localTime.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            }

            public string Date { get; }
            public string Day { get; }
            public string Time { get; }
            public string TimeZone { get; }
            public string UtcOffset { get; }
            public string Utc { get; }
        }
    }
}
