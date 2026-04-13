using Amuse.App.Common;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Pipeline;
using TensorStack.Providers;
using TensorStack.Video;
using TensorStack.Video.Common;
using TensorStack.Video.Pipelines;

namespace Amuse.App.Services
{
    public class InterpolationService : ServiceBase, IInterpolationService
    {
        private readonly Settings _settings;
        private readonly IMediaService _mediaService;
        private InterpolationPipeline _currentPipeline;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isLoaded;
        private bool _isLoading;
        private bool _isExecuting;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpscaleService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public InterpolationService(Settings settings, IMediaService mediaService)
        {
            _settings = settings;
            _mediaService = mediaService;
        }

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
        public async Task LoadAsync(DeviceModel device)
        {
            try
            {
                IsLoaded = false;
                IsLoading = true;
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    var cancellationToken = _cancellationTokenSource.Token;
                    if (_currentPipeline != null)
                        await _currentPipeline.UnloadAsync(cancellationToken);

                    _currentPipeline = InterpolationPipeline.Create(new ModelConfig
                    {
                        ExecutionProvider = device.GetProvider(),
                        Path = Path.Combine(App.DirectoryBase, "Interpolation.onnx")
                    });
                    await Task.Run(() => _currentPipeline.LoadAsync(cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _currentPipeline?.Dispose();
                _currentPipeline = null;
                throw;
            }
            finally
            {
                IsLoaded = true;
                IsLoading = false;
            }
        }


        /// <summary>
        /// Execute as an asynchronous operation.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <returns>A Task&lt;VideoInputStream&gt; representing the asynchronous operation.</returns>
        public async Task<VideoInputStream> ExecuteAsync(InterpolationRequest options, IProgress<RunProgress> progressCallback)
        {
            try
            {
                IsExecuting = true;
                var videoFileName = _mediaService.GetTempFile(MediaType.Video);
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    var cancellationToken = _cancellationTokenSource.Token;
                    var processedVideo = _currentPipeline.RunAsync(new InterpolationStreamOptions
                    {
                        Multiplier = options.Multiplier,
                        FrameCount = options.Frames,
                        FrameRate = options.FrameRate,
                        Stream = options.VideoStream.GetAsync()
                    }, progressCallback, cancellationToken: cancellationToken);


                    return await _mediaService.SaveWithAudioAsync(processedVideo, options.VideoStream.SourceFile, videoFileName, cancellationToken);
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
            if (_currentPipeline != null)
            {
                await _cancellationTokenSource.SafeCancelAsync();
                await _currentPipeline.UnloadAsync();
                _currentPipeline.Dispose();
                _currentPipeline = null;
            }

            IsLoaded = false;
            IsLoading = false;
            IsExecuting = false;
        }
    }


    public interface IInterpolationService
    {
        bool IsLoaded { get; }
        bool IsLoading { get; }
        bool IsExecuting { get; }
        bool CanCancel { get; }
        Task LoadAsync(DeviceModel device);
        Task UnloadAsync();
        Task CancelAsync();
        Task<VideoInputStream> ExecuteAsync(InterpolationRequest options, IProgress<RunProgress> progressCallback);
    }


    public record InterpolationRequest
    {
        public int Multiplier { get; set; }
        public int Frames { get; init; }
        public float FrameRate { get; init; }
        public VideoInputStream VideoStream { get; set; }
    }

}
