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
        /// Gets the tool description.
        /// </summary>
        public override string Description => "A function that multiplies two numbers";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => "DateTime Tool";

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "f1ec";

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => int.MaxValue;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}}",
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
                var a = GetArgument<double>("a");
                var b = GetArgument<double>("b");
                return Task.FromResult(SerializeResult($"{a * b}"));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"[Error] {Name} tool failed to execute: {ex.Message}");
            }
        }
    }
}
