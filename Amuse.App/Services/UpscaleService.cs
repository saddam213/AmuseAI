using System;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Pipeline;
using TensorStack.Common.Tensor;
using TensorStack.Common.Video;
using Amuse.App.Common;
using TensorStack.Providers;
using TensorStack.Upscaler.Common;
using TensorStack.Upscaler.Pipelines;
using TensorStack.Video;

namespace Amuse.App.Services
{
    public class UpscaleService : ServiceBase, IUpscaleService
    {
        private readonly Settings _settings;
        private readonly IMediaService _mediaService;
        private PipelineModel _currentPipeline;
        private UpscalePipeline _upscalePipeline;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isLoaded;
        private bool _isLoading;
        private bool _isExecuting;
        private UpscalerConfig _currentConfig;
        private UpscaleInputOptions _defaultOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpscaleService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public UpscaleService(Settings settings, IMediaService mediaService)
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
        public UpscaleInputOptions DefaultOptions => _defaultOptions;

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
                    if (_upscalePipeline != null)
                    {
                        if (_currentConfig.Path == pipeline.UpscaleModel.Path)
                            return; // Already loaded

                        await _upscalePipeline.UnloadAsync(cancellationToken);
                    }

                    _currentPipeline = pipeline;
                    var device = _currentPipeline.Device;
                    var model = _currentPipeline.UpscaleModel;
                    _defaultOptions = model.DefaultOptions;
                    _currentConfig = new UpscalerConfig
                    {
                        Channels = model.Channels,
                        Normalization = model.Normalization,
                        OutputNormalization = model.OutputNormalization,
                        SampleSize = model.SampleSize,
                        ScaleFactor = model.ScaleFactor,
                        Path = model.Path
                    };
                    _currentConfig.SetProvider(device.GetProvider());
                    _upscalePipeline = UpscalePipeline.Create(_currentConfig);
                    await Task.Run(() => _upscalePipeline.LoadAsync(cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _upscalePipeline?.Dispose();
                _upscalePipeline = null;
                _currentConfig = null;
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
        /// Execute the upscaler
        /// </summary>
        /// <param name="request">The request.</param>
        public async Task<ImageTensor> ExecuteAsync(UpscaleImageRequest request, IProgress<RunProgress> progressCallback)
        {
            try
            {
                IsExecuting = true;
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    var imageOptions = new UpscaleImageOptions
                    {
                        Image = request.Image,
                        MaxTileSize = request.Options.TileSize,
                        IsTileEnabled = request.Options.IsTileEnabled,
                        TileOverlap = request.Options.TileOverlap
                    };

                    return await Task.Run(() => _upscalePipeline.RunAsync(imageOptions, progressCallback, cancellationToken: _cancellationTokenSource.Token));
                }
            }
            finally
            {
                IsExecuting = false;
            }
        }


        /// <summary>
        /// Execute as an asynchronous operation.
        /// </summary>
        /// <param name="request">The options.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <returns>A Task&lt;VideoInputStream&gt; representing the asynchronous operation.</returns>
        public async Task<VideoInputStream> ExecuteAsync(UpscaleVideoRequest request, IProgress<RunProgress> progressCallback)
        {
            try
            {
                IsExecuting = true;
                var videoFileName = _mediaService.GetTempFile(MediaType.Video);
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    var frameCount = request.VideoStream.FrameCount;
                    var cancellationToken = _cancellationTokenSource.Token;

                    async Task<VideoFrame> FrameProcessor(VideoFrame frame)
                    {
                        var processedFrame = await _upscalePipeline.RunAsync(new UpscaleImageOptions
                        {
                            Image = frame.Frame,
                            MaxTileSize = request.Options.TileSize,
                            IsTileEnabled = request.Options.IsTileEnabled,
                            TileOverlap = request.Options.TileOverlap
                        }, cancellationToken: cancellationToken);

                        progressCallback.Report(new RunProgress(frame.Index, frameCount));
                        return new VideoFrame(frame.Index, processedFrame, frame.SourceFrameRate, frame.AuxFrame);
                    }

                    return await _mediaService.SaveWithAudioAsync(request.VideoStream, videoFileName, FrameProcessor, cancellationToken);
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
            if (_upscalePipeline != null)
            {
                await _cancellationTokenSource.SafeCancelAsync();
                await _upscalePipeline.UnloadAsync();
                _upscalePipeline?.Dispose();
                _upscalePipeline = null;
                _currentConfig = null;
                _currentPipeline = null;
            }

            IsLoaded = false;
            IsLoading = false;
            IsExecuting = false;
        }
    }


    public interface IUpscaleService
    {
        PipelineModel Pipeline { get; }
        UpscaleInputOptions DefaultOptions { get; }
        bool IsLoaded { get; }
        bool IsLoading { get; }
        bool IsExecuting { get; }
        bool CanCancel { get; }
        Task LoadAsync(PipelineModel pipeline);
        Task UnloadAsync();
        Task CancelAsync();
        Task<ImageTensor> ExecuteAsync(UpscaleImageRequest options, IProgress<RunProgress> progressCallback);
        Task<VideoInputStream> ExecuteAsync(UpscaleVideoRequest options, IProgress<RunProgress> progressCallback);
    }


    public record UpscaleImageRequest
    {
        public ImageTensor Image { get; set; }
        public UpscaleInputOptions Options { get; set; }
    }


    public record UpscaleVideoRequest
    {
        public VideoInputStream VideoStream { get; set; }
        public UpscaleInputOptions Options { get; set; }
    }

}
