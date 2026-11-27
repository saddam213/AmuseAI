using Amuse.App.Views;
using Amuse.Common;
using System.Collections.Generic;
using TensorStack.Common;

namespace Amuse.App.Common
{
    public interface IGenerateModel : IDownloadModel
    {
        PipelineType Pipeline { get; set; }
        BackendType Backend { get; set; }
        string ModelType { get; set; }
        string Template { get; set; }
        MediaType MediaType { get; set; }
        ProcessType[] ProcessTypes { get; set; }
        View[] ViewFilter { get; set; }
        VendorType[] Vendor { get; set; }
        float MemorySize { get; }
        bool HasCheckpointSets { get; }
        DataType? CurrentCheckpointType { get; set; }
        GenerateDefaultOptions DefaultOptions { get; set; }
        bool IsDefault { get; set; }
        bool IsNew { get; set; }
        bool IsRecommended { get; set; }

        public IEnumerable<CheckpointComponent> GetComponents();
        public IReadOnlyList<ICheckpointModel> GetCheckpoints();
        public float GetMemorySize(DataType? type);
        public bool IsCheckpointInstalled(DataType? type, Settings settings);
        public void SetCurrentCheckpoint(DataType? type, Settings settings);
        public void ResetCurrentCheckpoint(Settings settings);
    }
}
