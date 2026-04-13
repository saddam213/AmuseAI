using Amuse.App.Common;
using System;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.Common.Pipeline;
using TensorStack.Common.Tensor;
using TensorStack.Common.Video;
using TensorStack.Extractors.Common;
using TensorStack.Extractors.Pipelines;
using TensorStack.Providers;
using TensorStack.Video;

namespace Amuse.App.Services
{
    public class ExtractService : ServiceBase, IExtractService
    {
        private readonly Settings _settings;
        private readonly IMediaService _mediaService;
        private PipelineModel _currentPipeline;
        private IPipeline _extractPipeline;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isLoaded;
        private bool _isLoading;
        private bool _isExecuting;
        private ExtractorConfig _currentConfig;
        private ExtractInputOptions _defaultOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtractService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public ExtractService(Settings settings, IMediaService mediaService)
        {
            _settings = settings;
            _mediaService = mediaService;
        }


        /// <summary>
        /// Gets the pipeline.
        /// </summary>
        public PipelineModel Pipeline => _currentPipeline;

        /// <summary>
        /// Gets the default options.
        /// </summary>
        public ExtractInputOptions DefaultOptions => _defaultOptions;

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
        /// Load the pipeline
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
                    if (_extractPipeline != null)
                    {
                        if (_currentConfig.Path == pipeline.ExtractModel.Path)
                            return; // Already loaded

                        await _extractPipeline.UnloadAsync(cancellationToken);
                    }

                    _currentPipeline = pipeline;
                    var device = _currentPipeline.Device;
                    var model = _currentPipeline.ExtractModel;
                    _defaultOptions = model.DefaultOptions;
                    _currentConfig = new ExtractorConfig
                    {
                        Path = model.Path,
                        Channels = model.Channels,
                        Normalization = model.Normalization,
                        OutputChannels = model.OutputChannels,
                        OutputNormalization = model.OutputNormalization,
                        IsDynamicOutput = model.IsDynamicOutput,
                        SampleSize = model.SampleSize
                    };

                    _currentConfig.SetProvider(device.GetProvider(Microsoft.ML.OnnxRuntime.GraphOptimizationLevel.ORT_DISABLE_ALL));
                    _extractPipeline = model.Type switch
                    {
                        ExtractorType.Pose => PosePipeline.Create(_currentConfig),
                        ExtractorType.Background => BackgroundPipeline.Create(_currentConfig),
                        _ => ExtractorPipeline.Create(_currentConfig)
                    };

                    await Task.Run(() => _extractPipeline.LoadAsync(cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _extractPipeline?.Dispose();
                _extractPipeline = null;
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
        /// Execute the image ExtractorPipeline
        /// </summary>
        /// <param name="request">The request.</param>
        public async Task<ImageTensor> ExecuteAsync(ExtractImageRequest request, IProgress<RunProgress> progressCallback)
        {
            try
            {
                IsExecuting = true;
                var imageTensor = _currentPipeline.ExtractModel.Type switch
                {
                    ExtractorType.Default => await ExecuteDefaultAsync(request, progressCallback),
                    ExtractorType.Background => await ExecuteBackgroundAsync(request, progressCallback),
                    ExtractorType.Pose => await ExecutePoseAsync(request, progressCallback),
                    _ => throw new NotImplementedException()
                };

                return imageTensor;
            }
            finally
            {
                IsExecuting = false;
            }
        }


        public async Task<VideoInputStream> ExecuteAsync(ExtractVideoRequest request, IProgress<RunProgress> progressCallback)
        {
            try
            {
                IsExecuting = true;
                var videoStream = _currentPipeline.ExtractModel.Type switch
                {
                    ExtractorType.Default => await ExecuteDefaultAsync(request, progressCallback),
                    ExtractorType.Background => await ExecuteBackgroundAsync(request, progressCallback),
                    ExtractorType.Pose => await ExecutePoseAsync(request, progressCallback),
                    _ => throw new NotImplementedException()
                };

                return videoStream;
            }
            finally
            {
                IsExecuting = false;
            }
        }




        /// <summary>
        /// Execute the image ExtractorPipeline
        /// </summary>
        /// <param name="request">The request.</param>
        private async Task<ImageTensor> ExecuteDefaultAsync(ExtractImageRequest request, IProgress<RunProgress> progressCallback)
        {
            var pipeline = _extractPipeline as ExtractorPipeline;
            using (_cancellationTokenSource = new CancellationTokenSource())
            {
                return await Task.Run(() => pipeline.RunAsync(new ExtractorImageOptions
                {
                    Image = request.Image,
                    IsInverted = request.Options.IsInverted,
                    MaxTileSize = request.Options.TileSize,
                    IsTileEnabled = request.Options.IsTileEnabled,
                    TileOverlap = request.Options.TileOverlap,
                    MergeInput = request.Options.MergeInput
                }, progressCallback, _cancellationTokenSource.Token));
            }
        }


        /// <summary>
        /// Execute the image BackgroundPipeline
        /// </summary>
        /// <param name="request">The request.</param>
        private async Task<ImageTensor> ExecuteBackgroundAsync(ExtractImageRequest request, IProgress<RunProgress> progressCallback)
        {
            var pipeline = _extractPipeline as BackgroundPipeline;
            using (_cancellationTokenSource = new CancellationTokenSource())
            {
                return await Task.Run(() => pipeline.RunAsync(new BackgroundImageOptions
                {
                    Image = request.Image,
                    Mode = request.Options.Mode
                }, progressCallback, _cancellationTokenSource.Token));
            }
        }


        /// <summary>
        /// Execute the image PosePipeline
        /// </summary>
        /// <param name="request">The request.</param>
        private async Task<ImageTensor> ExecutePoseAsync(ExtractImageRequest request, IProgress<RunProgress> progressCallback)
        {
            var pipeline = _extractPipeline as PosePipeline;
            using (_cancellationTokenSource = new CancellationTokenSource())
            {
                return await Task.Run(() => pipeline.RunAsync(new PoseImageOptions
                {
                    Image = request.Image,
                    BodyConfidence = request.Options.BodyConfidence,
                    BoneRadius = request.Options.BoneRadius,
                    BoneThickness = request.Options.BoneThickness,
                    ColorAlpha = request.Options.ColorAlpha,
                    Detections = request.Options.Detections,
                    IsTransparent = request.Options.IsTransparent,
                    JointConfidence = request.Options.JointConfidence,
                    JointRadius = request.Options.JointRadius,
                }, progressCallback, _cancellationTokenSource.Token));
            }
        }


        /// <summary>
        /// Execute the video ExtractorPipeline
        /// </summary>
        /// <param name="request">The options.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <returns>A Task&lt;VideoInputStream&gt; representing the asynchronous operation.</returns>
        private async Task<VideoInputStream> ExecuteDefaultAsync(ExtractVideoRequest request, IProgress<RunProgress> progressCallback)
        {
            var pipeline = _extractPipeline as ExtractorPipeline;
            var resultVideoFile = FileHelper.RandomFileName(_settings.DirectoryTemp, "mp4");
            using (_cancellationTokenSource = new CancellationTokenSource())
            {
                var frameCount = request.VideoStream.FrameCount;
                var cancellationToken = _cancellationTokenSource.Token;

                async Task<VideoFrame> FrameProcessor(VideoFrame frame)
                {
                    var processedFrame = await pipeline.RunAsync(new ExtractorImageOptions
                    {
                        Image = frame.Frame,
                        IsInverted = request.Options.IsInverted,
                        MaxTileSize = request.Options.TileSize,
                        IsTileEnabled = request.Options.IsTileEnabled,
                        TileOverlap = request.Options.TileOverlap,
                        MergeInput = request.Options.MergeInput
                    }, cancellationToken: cancellationToken);

                    progressCallback.Report(new RunProgress(frame.Index, frameCount));
                    return new VideoFrame(frame.Index, processedFrame, frame.SourceFrameRate, frame.AuxFrame);
                }

                return await _mediaService.SaveWithAudioAsync(request.VideoStream, resultVideoFile, FrameProcessor, cancellationToken);
            }
        }


        /// <summary>
        /// Execute the video BackgroundPipeline
        /// </summary>
        /// <param name="request">The options.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <returns>A Task&lt;VideoInputStream&gt; representing the asynchronous operation.</returns>
        private async Task<VideoInputStream> ExecuteBackgroundAsync(ExtractVideoRequest request, IProgress<RunProgress> progressCallback)
        {
            var pipeline = _extractPipeline as BackgroundPipeline;
            var resultVideoFile = FileHelper.RandomFileName(_settings.DirectoryTemp, "mp4");
            using (_cancellationTokenSource = new CancellationTokenSource())
            {
                var frameCount = request.VideoStream.FrameCount;
                var cancellationToken = _cancellationTokenSource.Token;

                async Task<VideoFrame> FrameProcessor(VideoFrame frame)
                {
                    var processedFrame = await pipeline.RunAsync(new BackgroundImageOptions
                    {
                        Image = frame.Frame,
                        Mode = request.Options.Mode,
                        IsTransparentSupported = false
                    }, cancellationToken: cancellationToken);

                    progressCallback.Report(new RunProgress(frame.Index, frameCount));
                    return new VideoFrame(frame.Index, processedFrame, frame.SourceFrameRate, frame.AuxFrame);
                }

                return await _mediaService.SaveWithAudioAsync(request.VideoStream, resultVideoFile, FrameProcessor, cancellationToken);
            }
        }


        /// <summary>
        /// Execute the video PosePipeline
        /// </summary>
        /// <param name="request">The options.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <returns>A Task&lt;VideoInputStream&gt; representing the asynchronous operation.</returns>
        private async Task<VideoInputStream> ExecutePoseAsync(ExtractVideoRequest request, IProgress<RunProgress> progressCallback)
        {
            var pipeline = _extractPipeline as PosePipeline;
            var resultVideoFile = FileHelper.RandomFileName(_settings.DirectoryTemp, "mp4");
            using (_cancellationTokenSource = new CancellationTokenSource())
            {
                var frameCount = request.VideoStream.FrameCount;
                var cancellationToken = _cancellationTokenSource.Token;

                async Task<VideoFrame> FrameProcessor(VideoFrame frame)
                {
                    var processedFrame = await pipeline.RunAsync(new PoseImageOptions
                    {
                        Image = frame.Frame,
                        BodyConfidence = request.Options.BodyConfidence,
                        BoneRadius = request.Options.BoneRadius,
                        BoneThickness = request.Options.BoneThickness,
                        ColorAlpha = request.Options.ColorAlpha,
                        Detections = request.Options.Detections,
                        IsTransparent = request.Options.IsTransparent,
                        JointConfidence = request.Options.JointConfidence,
                        JointRadius = request.Options.JointRadius,
                    }, cancellationToken: cancellationToken);

                    progressCallback.Report(new RunProgress(frame.Index, frameCount));
                    return new VideoFrame(frame.Index, processedFrame, frame.SourceFrameRate, frame.AuxFrame);
                }

                return await _mediaService.SaveWithAudioAsync(request.VideoStream, resultVideoFile, FrameProcessor, cancellationToken);
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
            if (_extractPipeline != null)
            {
                await _cancellationTokenSource.SafeCancelAsync();
                await _extractPipeline.UnloadAsync();
                _extractPipeline?.Dispose();
                _extractPipeline = null;
                _currentConfig = null;
                _currentPipeline = null;
            }

            IsLoaded = false;
            IsLoaded = false;
            IsExecuting = false;
        }
    }


    public interface IExtractService
    {
        PipelineModel Pipeline { get; }
        ExtractInputOptions DefaultOptions { get; }
        bool IsLoaded { get; }
        bool IsLoading { get; }
        bool IsExecuting { get; }
        bool CanCancel { get; }
        Task LoadAsync(PipelineModel pipeline);
        Task UnloadAsync();
        Task CancelAsync();
        Task<ImageTensor> ExecuteAsync(ExtractImageRequest options, IProgress<RunProgress> progressCallback);
        Task<VideoInputStream> ExecuteAsync(ExtractVideoRequest options, IProgress<RunProgress> progressCallback);
    }


    public record ExtractImageRequest
    {
        public ImageTensor Image { get; init; }
        public ExtractInputOptions Options { get; init; }
    }


    public record ExtractVideoRequest
    {
        public VideoInputStream VideoStream { get; init; }
        public ExtractInputOptions Options { get; init; }
    }

}
