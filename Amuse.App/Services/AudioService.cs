using Amuse.App.Common;
using Microsoft.ML.OnnxRuntime;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Audio;
using TensorStack.Common;
using TensorStack.Common.Pipeline;
using TensorStack.Common.Tensor;
using TensorStack.Providers;
using TensorStack.TextGeneration.Common;
using TensorStack.TextGeneration.Pipelines.Supertonic;
using TensorStack.TextGeneration.Pipelines.Whisper;

namespace Amuse.App.Services
{
    public class AudioService : ServiceBase, IAudioService
    {
        private readonly Settings _settings;
        private readonly IMediaService _mediaService;
        private PipelineModel _currentPipeline;
        private IPipeline _audioPipeline;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isLoaded;
        private bool _isLoading;
        private bool _isExecuting;
        private AudioInputOptions _defaultOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public AudioService(Settings settings, IMediaService mediaService)
        {
            _settings = settings;
            _mediaService = mediaService;
        }

        /// <summary>
        /// Gets the model.
        /// </summary>
        public PipelineModel Pipeline => _currentPipeline;

        /// <summary>
        /// Gets the default options.
        /// </summary>
        public AudioInputOptions DefaultOptions => _defaultOptions;

        /// <summary>
        /// Gets a value indicating whether this instance is loaded.
        /// </summary>
        public bool IsLoaded
        {
            get { return _isLoaded; }
            private set { SetProperty(ref _isLoaded, value); }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is loading.
        /// </summary>
        public bool IsLoading
        {
            get { return _isLoading; }
            private set { SetProperty(ref _isLoading, value); NotifyPropertyChanged(nameof(CanCancel)); }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is executing.
        /// </summary>
        public bool IsExecuting
        {
            get { return _isExecuting; }
            private set { SetProperty(ref _isExecuting, value); NotifyPropertyChanged(nameof(CanCancel)); }
        }

        /// <summary>
        /// Gets a value indicating whether this instance can cancel.
        /// </summary>
        public bool CanCancel => _isLoading || _isExecuting;


        /// <summary>
        /// Load the upscale pipeline
        /// </summary>
        /// <param name="config">The configuration.</param>
        public async Task LoadAsync(PipelineModel pipeline)
        {
            try
            {
                IsLoaded = false;
                IsLoading = true;
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    var cancellationToken = _cancellationTokenSource.Token;
                    if (_currentPipeline != null)
                        await _audioPipeline.UnloadAsync(cancellationToken);

                    _currentPipeline = pipeline;
                    var device = _currentPipeline.Device;
                    var model = _currentPipeline.AudioModel;
                    _defaultOptions = new AudioInputOptions(); // TODO: Model defaults

                    var provider = device.GetProvider(GraphOptimizationLevel.ORT_ENABLE_ALL);
                    var providerCPU = Provider.GetProvider(DeviceType.CPU, GraphOptimizationLevel.ORT_ENABLE_ALL); // TODO: DirectML not working with decoder


                    if (model.Type == AudioModelType.Supertonic)
                    {
                        _audioPipeline = SupertonicPipeline.Create(model.Path, provider);
                    }
                    else if (model.Type == AudioModelType.Whisper)
                    {
                        if (!Enum.TryParse<WhisperType>(model.Version, true, out var whisperType))
                            throw new ArgumentException("Invalid Whisper Version");

                        _audioPipeline = WhisperPipeline.Create(provider, providerCPU, model.Path, whisperType);
                    }
                    else
                    {
                        throw new NotImplementedException($"{model.Type} not Implemented");
                    }

                    await Task.Run(() => _audioPipeline.LoadAsync(cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _audioPipeline?.Dispose();
                _audioPipeline = null;
                //_currentConfig = null;
                _currentPipeline = null;
                throw;
            }
            finally
            {
                IsLoaded = true;
                IsLoading = false;
            }
        }


        public async Task<TextInput[]> ExecuteAsync(WhisperRequest options, IProgress<GenerateProgress> progressCallback)
        {
            try
            {
                IsExecuting = true;
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    var pipelineOptions = new WhisperOptions
                    {
                        Seed = options.Seed,
                        Beams = options.Beams,
                        TopK = options.TopK,
                        TopP = options.TopP,
                        Temperature = options.Temperature,
                        MaxLength = options.MaxLength,
                        MinLength = options.MinLength,
                        NoRepeatNgramSize = options.NoRepeatNgramSize,
                        LengthPenalty = options.LengthPenalty,
                        DiversityLength = options.DiversityLength,
                        EarlyStopping = options.EarlyStopping,
                        AudioInput = await options.AudioInput.GetAsync(16000, 1),
                        Language = options.Language,
                        Task = options.Task,
                        ChunkSize = options.ChunkSize
                    };

                    var pipelineResult = await Task.Run(async () =>
                    {
                        if (options.Beams == 0)
                        {
                            // Greedy Search
                            var greedyPipeline = _audioPipeline as IPipeline<GenerateResult, WhisperOptions, GenerateProgress>;
                            return [await greedyPipeline.RunAsync(pipelineOptions, progressCallback, _cancellationTokenSource.Token)];
                        }

                        // Beam Search
                        var beamSearchPipeline = _audioPipeline as IPipeline<GenerateResult[], WhisperSearchOptions, GenerateProgress>;
                        return await beamSearchPipeline.RunAsync(new WhisperSearchOptions(pipelineOptions), progressCallback, _cancellationTokenSource.Token);
                    });

                    return [.. pipelineResult.Select(x => new TextInput(x.Result)
                    {
                        Beam = x.Beam,
                        PenaltyScore = x.PenaltyScore,
                        Score = x.Score,
                    })];
                }
            }
            finally
            {
                IsExecuting = false;
            }
        }


        public async Task<AudioInputStream> ExecuteAsync(SupertonicRequest options, IProgress<RunProgress> progressCallback)
        {
            try
            {
                IsExecuting = true;
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    var audioFileName = _mediaService.GetTempFile(MediaType.Audio);
                    var pipeline = _audioPipeline as IPipeline<AudioTensor, SupertonicOptions, RunProgress>;
                    var pipelineOptions = new SupertonicOptions
                    {
                        TextInput = options.InputText,
                        VoiceStyle = options.VoiceStyle,
                        Steps = options.Steps,
                        Speed = options.Speed,
                        SilenceDuration = options.SilenceDuration,
                        Seed = options.Seed,
                    };

                    var tensorResult = await pipeline.RunAsync(pipelineOptions, progressCallback, _cancellationTokenSource.Token);
                    var audioInput = new AudioInput(tensorResult);
                    await audioInput.SaveAsync(audioFileName);
                    return await AudioInputStream.CreateAsync(audioFileName);
                }
            }
            finally
            {
                IsExecuting = false;
            }
        }


        /// <summary>
        /// Cancel the running task (Load or Execute)
        /// </summary>
        public async Task CancelAsync()
        {
            await _cancellationTokenSource.SafeCancelAsync();
        }


        /// <summary>
        /// Unload the pipeline
        /// </summary>
        public async Task UnloadAsync()
        {
            if (_audioPipeline != null)
            {
                await _cancellationTokenSource.SafeCancelAsync();
                await _audioPipeline.UnloadAsync();
                _audioPipeline?.Dispose();
                _audioPipeline = null;
                _currentPipeline = null;
            }

            IsLoaded = false;
            IsLoading = false;
            IsExecuting = false;
        }
    }


    public interface IAudioService
    {
        PipelineModel Pipeline { get; }
        AudioInputOptions DefaultOptions { get; }
        bool IsLoaded { get; }
        bool IsLoading { get; }
        bool IsExecuting { get; }
        bool CanCancel { get; }
        Task LoadAsync(PipelineModel pipeline);
        Task UnloadAsync();
        Task CancelAsync();

        Task<TextInput[]> ExecuteAsync(WhisperRequest options, IProgress<GenerateProgress> progressCallback);
        Task<AudioInputStream> ExecuteAsync(SupertonicRequest options, IProgress<RunProgress> progressCallback);
    }

    public record WhisperRequest
    {
        public AudioInputStream AudioInput { get; set; }
        public LanguageType Language { get; set; } = LanguageType.EN;
        public TaskType Task { get; set; } = TaskType.Transcribe;

        public int MinLength { get; set; } = 20;
        public int MaxLength { get; set; } = 512;
        public int NoRepeatNgramSize { get; set; } = 3;
        public int Seed { get; set; }
        public int Beams { get; set; } = 3;
        public int TopK { get; set; } = 50;
        public float TopP { get; set; } = 0.9f;
        public float Temperature { get; set; } = 1.0f;
        public float LengthPenalty { get; set; } = 1.0f;
        public EarlyStopping EarlyStopping { get; set; }
        public int DiversityLength { get; set; } = 515;
        public int ChunkSize { get; set; } = 20;
    }


    public record SupertonicRequest
    {
        public string InputText { get; set; }
        public string VoiceStyle { get; set; }
        public int Steps { get; set; } = 5;
        public float Speed { get; set; } = 1f;
        public float SilenceDuration { get; set; } = 0.3f;
        public int Seed { get; set; }
    }



}
