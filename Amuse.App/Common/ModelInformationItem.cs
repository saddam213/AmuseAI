// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
namespace Amuse.App.Common
{
    /// <summary>
    /// A row of the model information dialog, a quality set heading or one part of the model and its files.
    /// </summary>
    public sealed class ModelInformationItem
    {
        public string Heading { get; init; }
        public string Name { get; init; }
        public string Source { get; init; }
        public ModelStatusType? Status { get; init; }
        public string[] Files { get; init; }
        public bool IsHeading => Heading != null;
    }
}
