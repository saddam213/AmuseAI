// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
namespace Amuse.App.Common
{
    /// <summary>
    /// A kind of shared component shown in the add component wizard, its key prefix and download folder.
    /// </summary>
    public sealed class WizardComponentKind
    {
        public string Name { get; init; }
        public string KeyPrefix { get; init; }
        public string Folder { get; init; }
    }
}
