using Amuse.App.Common;
using Amuse.Common;
using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Pipeline;
using TensorStack.Common.Video;
using TensorStack.Media.Video;
using PipelineProgress = Amuse.Common.PipelineProgress;

namespace Amuse.App.Services
{
    public sealed class InterpolationService : ServiceBase, IInterpolationService
    {
        private readonly Settings _settings;
        private readonly IMediaService _mediaService;
        private readonly ILogger _logger;
        private PipelineClient _pipelineClient;
        private IProgress<RunProgress> _executeProgress;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isLoaded;
        private bool _isLoading;
        private bool _isExecuting;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpscaleService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="mediaService">The media service.</param>
        /// <param name="logger">The logger.</param>
        public InterpolationService(Settings settings, IMediaService mediaService, ILogger<InterpolationService> logger)
        {
            _settings = settings;
            _mediaService = mediaService;
            _logger = logger;
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
                    await UnloadClientAsync();

                    _pipelineClient = new PipelineClient(new ClientConfig
                    {
                        ServerPath = App.DirectoryServer,
                        ServerType = ServerType.OnnxRuntimeInterpolate,
                        IsDebugMode = _settings.IsServerDebugEnabled
                    }, new Progress<PipelineProgress>(OnHostProgress), _logger);

                    await _pipelineClient.CreateAsync(new PipelineCreateOptions(), cancellationToken);
                    await _pipelineClient.LoadAsync(new PipelineLoadOptions
                    {
                        Pipeline = PipelineType.InterpolationPipeline,
                        Device = device.Name,
                        DeviceId = device.DeviceId,
                        InterpolateConfig = new InterpolateConfig
                        {
                            DeviceType = device.Type
                        }
                    }, cancellationToken);
                    IsLoaded = true;
                }
            }
            catch (Exception)
            {
                _pipelineClient?.Dispose();
                _pipelineClient = null;
                throw;
            }
            finally
            {
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
                    var runOptions = new PipelineRunOptions
                    {
                        InterpolateVideoOptions = new InterpolateVideoRunOptions
                        {
                            Multiplier = options.Multiplier,
                            FrameCount = options.Frames,
                            FrameRate = options.FrameRate
                        }
                    };

                    _executeProgress = progressCallback;
                    var inputStream = CreateStreamItemsAsync(options.VideoStream.GetAsync(), cancellationToken);
                    var processedVideo = CreateVideoFramesAsync(_pipelineClient.RunStreamAsync(runOptions, inputStream, cancellationToken), cancellationToken);
                    await processedVideo.SaveAsync(videoFileName, audioSourceFile: options.VideoStream.SourceFile, cancellationToken: cancellationToken);
                    return await VideoInputStream.CreateAsync(videoFileName);
                }
            }
            finally
            {
                _executeProgress = null;
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
            if (_pipelineClient != null)
            {
                await _cancellationTokenSource.SafeCancelAsync();
                await UnloadClientAsync();
            }

            IsLoaded = false;
            IsLoading = false;
            IsExecuting = false;
        }


        /// <summary>
        /// Unloads the pipeline and stops the host.
        /// </summary>
        private async Task UnloadClientAsync()
        {
            if (_pipelineClient is null)
                return;

            try
            {
                await _pipelineClient.UnloadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[InterpolationService] [Unload] Host did not unload, Reason: {Reason}", ex.Message);
            }
            finally
            {
                _pipelineClient.Dispose();
                _pipelineClient = null;
            }
        }


        /// <summary>
        /// Reports the host progress to the running execute.
        /// </summary>
        /// <param name="progress">The progress.</param>
        private void OnHostProgress(PipelineProgress progress)
        {
            if (_executeProgress is null || progress.Maximum <= 0)
                return;

            _executeProgress.Report(new RunProgress(progress.Value, progress.Maximum));
        }


        /// <summary>
        /// Converts the video frames to stream items as the host takes them.
        /// </summary>
        /// <param name="frames">The frames.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private static async IAsyncEnumerable<PipelineStreamItem> CreateStreamItemsAsync(IAsyncEnumerable<VideoFrame> frames, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var frame in frames.WithCancellation(cancellationToken))
            {
                yield return frame.ToStreamItem();
            }
        }


        /// <summary>
        /// Converts the stream items to video frames as the host produces them.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private static async IAsyncEnumerable<VideoFrame> CreateVideoFramesAsync(IAsyncEnumerable<PipelineStreamItem> items, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in items.WithCancellation(cancellationToken))
            {
                yield return item.ToVideoFrame();
            }
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
