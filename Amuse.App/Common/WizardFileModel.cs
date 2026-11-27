// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
namespace Amuse.App.Common
{
    /// <summary>
    /// A checkpoint component shown in the add model wizard, a file the user selects or a shared component Amuse supplies.
    /// </summary>
    public sealed class WizardFileModel
    {
        public string Label { get; init; }
        public CheckpointComponent Component { get; init; }
        public ComponentModel SharedComponent { get; init; }
        public bool IsMain { get; init; }

        public bool IsFolder
        {
            get { return Component.Type == CheckpointType.LocalFolder; }
        }

        public bool IsInstalled
        {
            get { return SharedComponent?.Status == ModelStatusType.Installed; }
        }
    }
}
