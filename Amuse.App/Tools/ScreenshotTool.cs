using Amuse.App.Services;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common.Common;
using TensorStack.WPF.Controls;

namespace Amuse.App.Tools
{
    public sealed class ScreenshotTool : ToolCallBase
    {
        /// <summary>
        /// Gets the tool name.
        /// </summary>
        /// <value>The name.</value>
        public override string Name => "screenshot_tool";

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Takes a snapshot of the users screen(s).";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => "Screenshot";

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "e0da";

        /// <summary>
        ///  Gets the tool icon style.
        /// </summary>
        public override FontAwesomeIconStyle DisplayIconStyle { get; } = FontAwesomeIconStyle.SharpLight;

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 4;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool when you need a snapshot image of the users screen, Default: All",
                "parameters": {
                    "type": "object",
                    "properties": { 
                        "screen": {
                            "type": "string",
                            "enum": ["All", "Primary", "Secondary"],
                            "description": "The screen to snapshot",
                            "default": "all"
                        }
                    }
                }
            }
        }
        """;

        /// <summary>
        /// Executes the tool.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public override async Task<string> ExecuteAsync(Settings settings, CancellationToken cancellationToken)
        {
            try
            {
                var screen = GetArgumentOrDefault("screen", "all");
                var result = CaptureSnapshot(settings.DirectoryHistory, screen);
                var historyService = App.GetService<IHistoryService>();
                await historyService.AddAsync(result);
                return SuccessResult(result.MediaFile);
            }
            catch (Exception ex)
            {
                return ErrorResult(ex);
            }
        }


        /// <summary>
        /// Captures the desktop.
        /// </summary>
        /// <param name="directoryTemp">The directory temporary.</param>
        /// <param name="mode">The mode.</param>
        private static MediaImportEventArgs CaptureSnapshot(string directoryTemp, string mode)
        {
            var filePath = Path.Combine(directoryTemp, $"Screenshot_{FileHelper.RandomFileName("png")}");
            var screens = System.Windows.Forms.Screen.AllScreens;
            var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            if (mode.Equals("primary", StringComparison.OrdinalIgnoreCase))
            {
                bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            }
            else if (mode.Equals("secondary", StringComparison.OrdinalIgnoreCase))
            {
                var secondaryScreen = screens.FirstOrDefault(s => !s.Primary);
                if (secondaryScreen != null)
                    bounds = secondaryScreen.Bounds;
            }

            var result = new MediaImportEventArgs(TensorStack.Common.MediaType.Image, filePath)
            {
                Width = bounds.Width,
                Height = bounds.Height
            };

            using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
                bitmap.Save(filePath, ImageFormat.Png);
            }
            return result;
        }

    }
}
