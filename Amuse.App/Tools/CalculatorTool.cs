using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amuse.App.Tools
{
    public sealed class CalculatorTool : ToolCallBase
    {
        /// <summary>
        /// Gets the tool name.
        /// </summary>
        /// <value>The name.</value>
        public override string Name => "calculator";
     
        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Perform basic arithmetic on two numbers.";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => "CalculatorT Tool";

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
                "description": "{{Description}} Use this tool when an exact arithmetic calculation is required.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "op": {
                            "type": "string",
                            "enum": ["add", "subtract", "multiply", "divide"],
                            "description": "The arithmetic operation to perform."
                        },
                        "a": {
                            "type": "number",
                            "description": "The first number."
                        },
                        "b": {
                            "type": "number",
                            "description": "The second number."
                        }
                    },
                    "required": ["op", "a", "b"]
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
                var op = GetArgument<string>("op");
                var a = GetArgument<double>("a");
                var b = GetArgument<double>("b");
                var result = op switch
                {
                    "add" => a + b,
                    "subtract" => a - b,
                    "multiply" => a * b,
                    "divide" => a / b,
                    _ => throw new ArgumentException($"Unknown operation: {op}")
                };
                return Task.FromResult(SuccessResult(result));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ErrorResult(ex));
            }
        }
    }
}
