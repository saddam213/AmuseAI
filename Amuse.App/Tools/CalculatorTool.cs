using Amuse.App.Resources;
using System;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.WPF.Controls;

namespace Amuse.App.Tools
{
    public sealed class CalculatorTool : ToolCallBase
    {
        private static readonly Regex _allowedRegex = new Regex(@"^[0-9\.\s\+\-\*/\(\)]+$", RegexOptions.Compiled);
        private static readonly Regex _integerRegex = new Regex(@"(?<![\d\.])(\d+)(?![\d\.])", RegexOptions.Compiled);

        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public override string Name => "calculator";

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Evaluate an arithmetic expression exactly, supporting + - * / and parentheses.";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => AppDefault.ToolCalculator;

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "f1ec";

        /// <summary>
        ///  Gets the tool icon style.
        /// </summary>
        public override FontAwesomeIconStyle DisplayIconStyle { get; } = FontAwesomeIconStyle.SharpLight;

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 2;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool whenever an exact numeric result is required instead of calculating it yourself. Write percentages as multiplication, e.g. 15% of 200 is '0.15 * 200'.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "expression": {
                            "type": "string",
                            "description": "The arithmetic expression to evaluate, using only numbers, + - * / and parentheses, e.g. '(12.5 * 4) / 3'."
                        }
                    },
                    "required": ["expression"]
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
                // Validate
                var expression = GetArgument<string>("expression")?.Replace("×", "*").Replace("÷", "/").Trim();
                if (string.IsNullOrEmpty(expression) || !_allowedRegex.IsMatch(expression))
                    throw new FormatException("The expression may only contain numbers, + - * /, parentheses and spaces.");

                // Evaluate (integers as decimals to avoid Int32 overflow and integer division by zero returning infinity)
                var decimalExpression = _integerRegex.Replace(expression, "$1.0");
                using (var dataTable = new DataTable { Locale = CultureInfo.InvariantCulture })
                {
                    var value = dataTable.Compute(decimalExpression, null);
                    if (value is double number && (double.IsNaN(number) || double.IsInfinity(number)))
                        throw new ArithmeticException("The expression result is undefined.");

                    // Result (G29 drops the trailing zeros added by the decimal conversion)
                    var text = value is decimal decimalValue
                        ? decimalValue.ToString("G29", CultureInfo.InvariantCulture)
                        : Convert.ToString(value, CultureInfo.InvariantCulture);
                    var result = new CalculatorResult(expression, text);
                    return Task.FromResult(SuccessResult(result));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(ErrorResult(ex.InnerException ?? ex));
            }
        }


        /// <summary>
        /// CalculatorResult structure.
        /// </summary>
        private sealed record CalculatorResult(string Expression, string Result);
    }
}
