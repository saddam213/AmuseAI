using Amuse.Common;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TensorStack.Common.Tensor;
using TensorStack.Media.Audio;
using TensorStack.OnnxRuntime.LLM.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed record GenerateInputOptions : BaseRecord
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
        private SchedulerInputOptions _schedulerOptions;
        private List<LoraOptionModel> _loraOptions;
        private int _frames;
        private float _frameRate;
        private int _noiseCondition;
        private int _frameChunkOverlap;
        private int _frameChunk;
        private bool _isVaeTilingEnabled;
        private bool _isVaeSlicingEnabled;
        private bool _isSource1Enabled = true;
        private bool _isSource2Enabled;
        private bool _isSource3Enabled;
        private bool _isSource4Enabled;
        private int _bpm;
        private string _instruction;
        private string _keyscale;
        private string _prompt2;
        private string _task;
        private string _timeSignature;
        private string _trackName;
        private float _duration;
        private LanguageType _language;
        private float _speed;
        private float _silenceDuration;
        private int _minLength;
        private int _maxLength;
        private int _noRepeatNgramSize;
        private int _beams;
        private int _topK;
        private float _topP;
        private float _temperature;
        private float _lengthPenalty;
        private EarlyStopping _earlyStopping;
        private int _diversityLength;
        private float _topH;
        private float _typicalP;
        private float _repetitionPenalty;
        private int _chunkSize;
        private ObservableCollection<ConversationModel> _conversation;
        private bool _isSamplingEnabled;
        private bool _isThinkingEnabled;
        private CacheType _cacheType;
        private LatentUpscale _latentUpscale;
        private int _latentUpscaleSteps = 4;
        private float _latentUpscaleStrength = 0.7f;
        private int _latentUpscaleTileSize = 128;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Width
        {
            get { return _width; }
            set { SetProperty(ref _width, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
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

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Prompt
        {
            get { return _prompt; }
            set { SetProperty(ref _prompt, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Prompt2
        {
            get { return _prompt2; }
            set { SetProperty(ref _prompt2, value); }
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

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsSource1Enabled
        {
            get { return _isSource1Enabled; }
            set { SetProperty(ref _isSource1Enabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsSource2Enabled
        {
            get { return _isSource2Enabled; }
            set { SetProperty(ref _isSource2Enabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsSource3Enabled
        {
            get { return _isSource3Enabled; }
            set { SetProperty(ref _isSource3Enabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsSource4Enabled
        {
            get { return _isSource4Enabled; }
            set { SetProperty(ref _isSource4Enabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Bpm
        {
            get { return _bpm; }
            set { SetProperty(ref _bpm, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Instruction
        {
            get { return _instruction; }
            set { SetProperty(ref _instruction, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Keyscale
        {
            get { return _keyscale; }
            set { SetProperty(ref _keyscale, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Task
        {
            get { return _task; }
            set { SetProperty(ref _task, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string TimeSignature
        {
            get { return _timeSignature; }
            set { SetProperty(ref _timeSignature, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string TrackName
        {
            get { return _trackName; }
            set { SetProperty(ref _trackName, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Duration
        {
            get { return _duration; }
            set { SetProperty(ref _duration, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public LanguageType Language
        {
            get { return _language; }
            set { SetProperty(ref _language, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Speed
        {
            get { return _speed; }
            set { SetProperty(ref _speed, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float SilenceDuration
        {
            get { return _silenceDuration; }
            set { SetProperty(ref _silenceDuration, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MinLength
        {
            get { return _minLength; }
            set { SetProperty(ref _minLength, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int MaxLength
        {
            get { return _maxLength; }
            set { SetProperty(ref _maxLength, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int NoRepeatNgramSize
        {
            get { return _noRepeatNgramSize; }
            set { SetProperty(ref _noRepeatNgramSize, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsSamplingEnabled
        {
            get { return _isSamplingEnabled; }
            set { SetProperty(ref _isSamplingEnabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Beams
        {
            get { return _beams; }
            set { SetProperty(ref _beams, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int TopK
        {
            get { return _topK; }
            set { SetProperty(ref _topK, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float TopP
        {
            get { return _topP; }
            set { SetProperty(ref _topP, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Temperature
        {
            get { return _temperature; }
            set { SetProperty(ref _temperature, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float LengthPenalty
        {
            get { return _lengthPenalty; }
            set { SetProperty(ref _lengthPenalty, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public EarlyStopping EarlyStopping
        {
            get { return _earlyStopping; }
            set { SetProperty(ref _earlyStopping, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int DiversityLength
        {
            get { return _diversityLength; }
            set { SetProperty(ref _diversityLength, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float TopH
        {
            get { return _topH; }
            set { SetProperty(ref _topH, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float TypicalP
        {
            get { return _typicalP; }
            set { SetProperty(ref _typicalP, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float RepetitionPenalty
        {
            get { return _repetitionPenalty; }
            set { SetProperty(ref _repetitionPenalty, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ChunkSize
        {
            get { return _chunkSize; }
            set { SetProperty(ref _chunkSize, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public LatentUpscale LatentUpscale
        {
            get { return _latentUpscale; }
            set { SetProperty(ref _latentUpscale, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int LatentUpscaleSteps
        {
            get { return _latentUpscaleSteps; }
            set { SetProperty(ref _latentUpscaleSteps, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float LatentUpscaleStrength
        {
            get { return _latentUpscaleStrength; }
            set { SetProperty(ref _latentUpscaleStrength, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int LatentUpscaleTileSize
        {
            get { return _latentUpscaleTileSize; }
            set { SetProperty(ref _latentUpscaleTileSize, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ObservableCollection<ConversationModel> Conversation
        {
            get { return _conversation; }
            set { SetProperty(ref _conversation, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsThinkingEnabled
        {
            get { return _isThinkingEnabled; }
            set { SetProperty(ref _isThinkingEnabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public CacheType CacheType
        {
            get { return _cacheType; }
            set { SetProperty(ref _cacheType, value); }
        }

        [JsonIgnore]
        public List<ImageTensor> InputImages { get; set; } = [];

        [JsonIgnore]
        public List<ImageTensor> InputControlImages { get; set; } = [];

        [JsonIgnore]
        public List<AudioInputStream> InputAudios { get; set; } = [];


        public bool Equals(GenerateInputOptions other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
