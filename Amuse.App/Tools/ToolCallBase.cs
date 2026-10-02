using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Amuse.App.Tools
{
    public abstract class ToolCallBase
    {
        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public abstract string Schema { get; }

        /// <summary>
        /// Gets the arguments.
        /// </summary>
        public Dictionary<string, JsonElement> Arguments { get; init; }

        /// <summary>
        /// Executes the tool.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public abstract Task<string> ExecuteAsync(Settings settings, CancellationToken cancellationToken = default);
    }
}
