using System.Collections.Generic;
using System.Text.Json.Serialization;
using TensorStack.Common.Tensor;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public record DiffusionInputOptions : BaseRecord
    {
        private int _width;
        private int _height;
        private int _seed;
        private float _guidanceScale = 0;
        private float _guidanceScale2 = 0;
        private string _prompt;
        private string _negativePrompt;
        private int _steps;
        private int _steps2;
        private float _strength = 0;
        private float _controlNetStrength = 0;
        private int _inputImageCount = 0;
        private SchedulerInputOptions _schedulerOptions;
        private List<LoraOptionModel> _loraOptions;
        private int _frames;
        private float _frameRate;
        private int _noiseCondition;
        private int _frameChunkOverlap;
        private int _frameChunk;
        private bool _isVaeTilingEnabled;
        private bool _isVaeSlicingEnabled;

        public int Width
        {
            get { return _width; }
            set { SetProperty(ref _width, value); }
        }

        public int Height
        {
            get { return _height; }
            set { SetProperty(ref _height, value); }
        }

        public int Seed
        {
            get { return _seed; }
            set { SetProperty(ref _seed, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float GuidanceScale
        {
            get { return _guidanceScale; }
            set { SetProperty(ref _guidanceScale, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float GuidanceScale2
        {
            get { return _guidanceScale2; }
            set { SetProperty(ref _guidanceScale2, value); }
        }

        public string Prompt
        {
            get { return _prompt; }
            set { SetProperty(ref _prompt, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string NegativePrompt
        {
            get { return _negativePrompt; }
            set { SetProperty(ref _negativePrompt, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Steps
        {
            get { return _steps; }
            set { SetProperty(ref _steps, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Steps2
        {
            get { return _steps2; }
            set { SetProperty(ref _steps2, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Strength
        {
            get { return _strength; }
            set { SetProperty(ref _strength, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float ControlNetStrength
        {
            get { return _controlNetStrength; }
            set { SetProperty(ref _controlNetStrength, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int InputImageCount
        {
            get { return _inputImageCount; }
            set { SetProperty(ref _inputImageCount, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Frames
        {
            get { return _frames; }
            set { SetProperty(ref _frames, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float FrameRate
        {
            get { return _frameRate; }
            set { SetProperty(ref _frameRate, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int FrameChunk
        {
            get { return _frameChunk; }
            set { SetProperty(ref _frameChunk, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int FrameChunkOverlap
        {
            get { return _frameChunkOverlap; }
            set { SetProperty(ref _frameChunkOverlap, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int NoiseCondition
        {
            get { return _noiseCondition; }
            set { SetProperty(ref _noiseCondition, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsVaeTilingEnabled
        {
            get { return _isVaeTilingEnabled; }
            set { SetProperty(ref _isVaeTilingEnabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsVaeSlicingEnabled
        {
            get { return _isVaeSlicingEnabled; }
            set { SetProperty(ref _isVaeSlicingEnabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SchedulerInputOptions SchedulerOptions
        {
            get { return _schedulerOptions; }
            set { SetProperty(ref _schedulerOptions, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<LoraOptionModel> LoraOptions
        {
            get { return _loraOptions; }
            set { SetProperty(ref _loraOptions, value); }
        }


        [JsonIgnore]
        public List<ImageTensor> InputImages { get; set; } = [];

        [JsonIgnore]
        public List<ImageTensor> InputControlImages { get; set; } = [];
    }
}
