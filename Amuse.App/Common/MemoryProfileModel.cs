using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class MemoryProfileModel : BaseModel
    {
        private MemoryMode _memoryMode;
        private MemoryMode _detectedMode;
        private string _description;

        /// <summary>
        /// Gets or sets the memory mode.
        /// </summary>
        public MemoryMode MemoryMode
        {
            get { return _memoryMode; }
            set { SetProperty(ref _memoryMode, value); }
        }

        /// <summary>
        /// Gets or sets the mode Auto resolves to.
        /// </summary>
        public MemoryMode DetectedMode
        {
            get { return _detectedMode; }
            set { SetProperty(ref _detectedMode, value); }
        }

        /// <summary>
        /// Gets or sets the description of what this mode does on the selected backend.
        /// </summary>
        public string Description
        {
            get { return _description; }
            set { SetProperty(ref _description, value); }
        }
    }
}
