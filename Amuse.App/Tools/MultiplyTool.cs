using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amuse.App.Tools
{
    public sealed class MultiplyTool : ToolCallBase
    {
        /// <summary>
        /// Gets the tool name.
        /// </summary>
        /// <value>The name.</value>
        public override string Name => "multiply";

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => """
        {
            "type": "function",
            "function": {
                "name": "multiply",
                "description": "A function that multiplies two numbers",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "a": {
                            "type": "number",
                            "description": "The first number to multiply"
                        },
                        "b": {
                            "type": "number", 
                            "description": "The second number to multiply"
                        }
                    },
                    "required": ["a", "b"]
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
                var a = Arguments["a"].GetDouble();
                var b = Arguments["b"].GetDouble();
                return Task.FromResult($"{a * b}");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"[Error] {Name} tool failed to execute: {ex.Message}");
            }
        }
    }
}
