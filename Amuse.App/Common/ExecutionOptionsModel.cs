using Amuse.Common;
using System.Text.Json.Serialization;
using TensorStack.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class ExecutionOptionsModel : BaseModel
    {
        private bool _isPrefetchEnabled = true;
        private bool _isSegmentedComputeEnabled = true;
        private int _memoryReserve = 1;
        private int _gpuLayers = -1;
        private int _contextSize = 16384;
        private int _threads;
        private bool _isCacheOffloadEnabled = true;
        private bool _isControlNetCpuEnabled;
        private bool _isTextEncoderCpuEnabled;
        private bool _isAutoEncoderCpuEnabled;
        private AutoOption _diffusionConvDirect;
        private AutoOption _controlNetConvDirect;
        private AutoOption _autoEncoderConvDirect;
        private AutoOption _textEncoderConvDirect;
        private AutoOption _diffusionFlashAttention;
        private AutoOption _controlNetFlashAttention;
        private AutoOption _autoEncoderFlashAttention;
        private AutoOption _textEncoderFlashAttention;
        private bool _isSageAttentionEnabled;
        private bool _isOptimizeDeviceEnabled;
        private bool _isOptimizeChannelsEnabled;
        private bool _isDeviceQuantizationEnabled;
        private DataType _weightType;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsPrefetchEnabled
        {
            get { return _isPrefetchEnabled; }
            set { SetProperty(ref _isPrefetchEnabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsSegmentedComputeEnabled
        {
            get { return _isSegmentedComputeEnabled; }
            set { SetProperty(ref _isSegmentedComputeEnabled, value); }
        }

        public int MemoryReserve
        {
            get { return _memoryReserve; }
            set { SetProperty(ref _memoryReserve, value); }
        }

        public int GpuLayers
        {
            get { return _gpuLayers; }
            set { SetProperty(ref _gpuLayers, value); }
        }

        public int ContextSize
        {
            get { return _contextSize; }
            set { SetProperty(ref _contextSize, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Threads
        {
            get { return _threads; }
            set { SetProperty(ref _threads, value); }
        }

        public bool IsCacheOffloadEnabled
        {
            get { return _isCacheOffloadEnabled; }
            set { SetProperty(ref _isCacheOffloadEnabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsControlNetCpuEnabled
        {
            get { return _isControlNetCpuEnabled; }
            set { SetProperty(ref _isControlNetCpuEnabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsTextEncoderCpuEnabled
        {
            get { return _isTextEncoderCpuEnabled; }
            set { SetProperty(ref _isTextEncoderCpuEnabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsAutoEncoderCpuEnabled
        {
            get { return _isAutoEncoderCpuEnabled; }
            set { SetProperty(ref _isAutoEncoderCpuEnabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AutoOption DiffusionConvDirect
        {
            get { return _diffusionConvDirect; }
            set { SetProperty(ref _diffusionConvDirect, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AutoOption ControlNetConvDirect
        {
            get { return _controlNetConvDirect; }
            set { SetProperty(ref _controlNetConvDirect, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AutoOption AutoEncoderConvDirect
        {
            get { return _autoEncoderConvDirect; }
            set { SetProperty(ref _autoEncoderConvDirect, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AutoOption TextEncoderConvDirect
        {
            get { return _textEncoderConvDirect; }
            set { SetProperty(ref _textEncoderConvDirect, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AutoOption DiffusionFlashAttention
        {
            get { return _diffusionFlashAttention; }
            set { SetProperty(ref _diffusionFlashAttention, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AutoOption ControlNetFlashAttention
        {
            get { return _controlNetFlashAttention; }
            set { SetProperty(ref _controlNetFlashAttention, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AutoOption AutoEncoderFlashAttention
        {
            get { return _autoEncoderFlashAttention; }
            set { SetProperty(ref _autoEncoderFlashAttention, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public AutoOption TextEncoderFlashAttention
        {
            get { return _textEncoderFlashAttention; }
            set { SetProperty(ref _textEncoderFlashAttention, value); }
        }


        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsSageAttentionEnabled
        {
            get { return _isSageAttentionEnabled; }
            set { SetProperty(ref _isSageAttentionEnabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsOptimizeDeviceEnabled
        {
            get { return _isOptimizeDeviceEnabled; }
            set { SetProperty(ref _isOptimizeDeviceEnabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsOptimizeChannelsEnabled
        {
            get { return _isOptimizeChannelsEnabled; }
            set { SetProperty(ref _isOptimizeChannelsEnabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsDeviceQuantizationEnabled
        {
            get { return _isDeviceQuantizationEnabled; }
            set { SetProperty(ref _isDeviceQuantizationEnabled, value); }
        }

        /// <summary>
        /// Gets or sets the type the model weights are converted to when loading, Default keeps the model's own type.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DataType WeightType
        {
            get { return _weightType; }
            set { SetProperty(ref _weightType, value); }
        }


        public ExecutionOptionsModel DeepClone()
        {
            return new ExecutionOptionsModel
            {
                IsPrefetchEnabled = _isPrefetchEnabled,
                IsSegmentedComputeEnabled = _isSegmentedComputeEnabled,
                MemoryReserve = _memoryReserve,
                GpuLayers = _gpuLayers,
                ContextSize = _contextSize,
                Threads = _threads,
                IsCacheOffloadEnabled = _isCacheOffloadEnabled,
                IsAutoEncoderCpuEnabled = _isAutoEncoderCpuEnabled,
                IsControlNetCpuEnabled = _isControlNetCpuEnabled,
                IsTextEncoderCpuEnabled = _isTextEncoderCpuEnabled,
                AutoEncoderConvDirect = _autoEncoderConvDirect,
                DiffusionConvDirect = _diffusionConvDirect,
                ControlNetConvDirect = _controlNetConvDirect,
                TextEncoderConvDirect = _textEncoderConvDirect,
                AutoEncoderFlashAttention = _autoEncoderFlashAttention,
                DiffusionFlashAttention = _diffusionFlashAttention,
                ControlNetFlashAttention = _controlNetFlashAttention,
                TextEncoderFlashAttention = _textEncoderFlashAttention,
                IsSageAttentionEnabled = _isSageAttentionEnabled,
                IsOptimizeChannelsEnabled = _isOptimizeChannelsEnabled,
                IsOptimizeDeviceEnabled = _isOptimizeDeviceEnabled,
                IsDeviceQuantizationEnabled = _isDeviceQuantizationEnabled,
                WeightType = _weightType
            };
        }
    }
}
