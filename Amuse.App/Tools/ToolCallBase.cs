using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
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
        /// Gets the tool icon.
        /// </summary>
        public abstract string DisplayIcon { get; }

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public abstract bool IsDefault { get; }

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public abstract int DisplayOrder { get; }

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public abstract string Schema { get; }

        /// <summary>
        /// Gets the arguments.
        /// </summary>
        public Dictionary<string, JsonElement> Arguments { get; init; }

        /// <summary>
        /// Gets or sets the HTTP client.
        /// </summary>
        public HttpClient HttpClient { get; set; }

        /// <summary>
        /// Executes the tool.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public abstract Task<string> ExecuteAsync(Settings settings, CancellationToken cancellationToken = default);


        /// <summary>
        /// Gets the result.
        /// </summary>
        /// <param name="result">The result.</param>
        protected string SerializeResult(object result)
        {
            var content = JsonSerializer.Serialize(result, Json.DefaultOptions);
            Debug.WriteLine(content);
            return content;
        }


        /// <summary>
        /// Gets the argument.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        protected T GetArgument<T>(string name)
        {
            if (Arguments.TryGetValue(name, out var argument))
                return argument.Deserialize<T>();
            throw new ArgumentNullException(name);
        }


        /// <summary>
        /// Gets the argument or default.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="defaultValue">The default value.</param>
        protected T GetArgumentOrDefault<T>(string name, T defaultValue = default)
        {
            if (Arguments.TryGetValue(name, out var argument))
            {
                return argument.Deserialize<T>();
            }
            return defaultValue;
        }
    }
}
