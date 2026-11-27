using System;
using System.Collections.Generic;
using System.Linq;

namespace Amuse.App.Common
{
    /// <summary>
    /// A UI scale choice, from 50% to 300% in 5% steps.
    /// </summary>
    /// <param name="Label">The label shown in the list.</param>
    /// <param name="Value">The scale value.</param>
    public sealed record ScaleOption(string Label, double Value)
    {
        public const double Minimum = 0.5;

        public static IReadOnlyList<ScaleOption> Options { get; } = [.. Enumerable.Range(10, 51).Select(x => new ScaleOption($"{x * 5}%", x / 20.0))];


        /// <summary>
        /// Gets the largest 5% step at or below the value, between the minimum and the maximum.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="maximum">The maximum scale.</param>
        public static double FloorToStep(double value, double maximum)
        {
            var step = Math.Floor(value * 20 + 1e-6) / 20.0;
            return Math.Clamp(step, Minimum, maximum);
        }
    }
}
