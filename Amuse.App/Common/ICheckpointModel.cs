using System.Collections.Generic;
using TensorStack.Common;

namespace Amuse.App.Common
{
    /// <summary>
    /// A checkpoint set of a generate model.
    /// </summary>
    public interface ICheckpointModel
    {
        DataType Type { get; set; }
        float MemorySize { get; set; }
        bool IsInstalled { get; }
        string QualityDescription { get; }

        bool CheckInstall(Settings settings, string modelDirectory);
        IEnumerable<CheckpointComponent> GetComponents();
    }
}
