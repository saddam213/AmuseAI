using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class MemoryProfile : BaseModel
    {
        public MemoryProfile() { }
        public MemoryProfile(QualityMode qualityMode, int[] memoryModes)
        {
            QualityMode = qualityMode;
            MemoryModes = memoryModes;
        }

        public QualityMode QualityMode { get; set; }
        public int[] MemoryModes { get; set; }
    }
}
