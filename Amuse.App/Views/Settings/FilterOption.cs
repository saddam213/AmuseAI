using Amuse.App.Common;
using Amuse.App.Resources;
using System;
using TensorStack.Common;

namespace Amuse.App.Views
{
    /// <summary>
    /// A settings list filter option with a display label and the value it filters on, a null value matches every item.
    /// </summary>
    /// <param name="Label">The display label.</param>
    /// <param name="Value">The filter value, or null to match every item.</param>
    public sealed record FilterOption(string Label, string Value)
    {
        /// <summary>
        /// Creates the option that matches every item.
        /// </summary>
        public static FilterOption ShowAll()
        {
            return new FilterOption(AppDefault.ShowAll, null);
        }


        /// <summary>
        /// Creates an option that uses the value as its label.
        /// </summary>
        /// <param name="value">The filter value.</param>
        public static FilterOption FromValue(string value)
        {
            return new FilterOption(value, value);
        }


        /// <summary>
        /// Creates an option for the enum value, using its display name as the label.
        /// </summary>
        /// <param name="value">The enum value.</param>
        public static FilterOption FromEnum(Enum value)
        {
            return new FilterOption(value.GetDisplayName(), value.ToString());
        }


        /// <summary>
        /// Creates the model status options, show all, installed and unknown.
        /// </summary>
        public static FilterOption[] ModelStatuses()
        {
            return
            [
                ShowAll(),
                new FilterOption(AppDefault.Installed, nameof(ModelStatusType.Installed)),
                new FilterOption(AppEnums.ModelStatusType1, nameof(ModelStatusType.Unknown))
            ];
        }


        /// <summary>
        /// Returns the display label.
        /// </summary>
        public override string ToString()
        {
            return Label;
        }
    }
}
