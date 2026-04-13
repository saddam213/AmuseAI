using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class MemoryProfileModel : BaseModel
    {
        private int _memoryGB;
        private MemoryMode _memoryMode;
        private MemoryMode _detectedMode;

        public MemoryMode MemoryMode
        {
            get { return _memoryMode; }
            set { SetProperty(ref _memoryMode, value); }
        }

        public int MemoryGB
        {
            get { return _memoryGB; }
            set { SetProperty(ref _memoryGB, value); }
        }

        public MemoryMode DetectedMode
        {
            get { return _detectedMode; }
            set { SetProperty(ref _detectedMode, value); }
        }

    }
}
