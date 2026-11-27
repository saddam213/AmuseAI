using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Runtime;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Pipeline;
using TensorStack.Common.Tensor;
using TensorStack.Common.Video;
using TensorStack.Media;
using TensorStack.Media.Video;

namespace Amuse.App.Services
{
    public sealed class ExtractService : ServiceBase, IExtractService
    {
        private readonly ILogger _logger;
        private readonly Settings _settings;
        private readonly IToolService _toolService;
        private readonly IMediaService _mediaService;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private PipelineModel _currentPipeline;
        private BackendClient _backendClient;
        private CancellationTokenSource _cancellationTokenSource;
        private IProgress<RunProgress> _executeProgress;
        private bool _isLoaded;
        private bool _isLoading;
        private bool _isExecuting;
        private ExtractInputOptions _defaultOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtractService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="toolService">The tool service.</param>
        /// <param name="mediaService">The media service.</param>
        /// <param name="logger">The logger.</param>
        public ExtractService(Settings settings, IToolService toolService, IMediaService mediaService, ILogger<ExtractService> logger)
        {
            _logger = logger;
            _settings = settings;
            _toolService = toolService;
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
        /// Load the extract pipeline into its backend host.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        public async Task LoadAsync(PipelineModel pipeline)
        {
            var timestamp = Stopwatch.GetTimestamp();
            await _operationLock.WaitAsync();
            try
            {
                IsLoaded = false;
                IsLoading = true;
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    await LoadBackendAsync(pipeline, _cancellationTokenSource.Token);
                    _logger.LogInformation("[ExtractService] [Load] Extract model loaded, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[ExtractService] [Load] Loading extract model cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExtractService] [Load] An exception occurred loading extract model, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                throw;
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }


        /// <summary>
        /// Execute the extractor on an image.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="progressCallback">The progress callback.</param>
        public async Task<ImageTensor> ExecuteAsync(ExtractImageRequest request, IProgress<RunProgress> progressCallback)
        {
            await _operationLock.WaitAsync();
            try
            {
                IsExecuting = true;
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    // Extract
                    EnsureLoaded();
                    _executeProgress = progressCallback;
                    return await RunOnBackendAsync(() => _backendClient.ExtractImageAsync(request.Image, request.Options));
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[ExtractService] [Execute] Image extract cancelled.");
                throw;
            }
            finally
            {
                _executeProgress = null;
                IsExecuting = false;
                _operationLock.Release();
            }
        }


        /// <summary>
        /// Execute the extractor on a video, the frames are streamed through the host.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="progressCallback">The progress callback.</param>
        public async Task<VideoInputStream> ExecuteAsync(ExtractVideoRequest request, IProgress<RunProgress> progressCallback)
        {
            await _operationLock.WaitAsync();
            try
            {
                IsExecuting = true;
                var videoFileName = _mediaService.GetTempFile(MediaType.Video);
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    var cancellationToken = _cancellationTokenSource.Token;
                    var model = EnsureLoaded();

                    // Video frames have no alpha channel
                    var isTransparentSupported = model.Type != ExtractorType.Background;

                    // Extract, the frames are streamed through the host
                    var inputFrames = request.VideoStream.GetAsync(cancellationToken: cancellationToken);
                    var extractedFrames = _backendClient.ExtractVideoStreamAsync(inputFrames, request.Options, isTransparentSupported, cancellationToken);
                    var resultFrames = ReportProgressAsync(extractedFrames, request.VideoStream.FrameCount, progressCallback, cancellationToken);
                    var audioSourceFile = request.VideoStream.HasAudio ? request.VideoStream.SourceFile : null;
                    return await RunOnBackendAsync(async () =>
                    {
                        await resultFrames.SaveAsync(videoFileName, audioSourceFile: audioSourceFile, cancellationToken: cancellationToken);
                        return await VideoInputStream.CreateAsync(videoFileName);
                    });
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[ExtractService] [Execute] Video extract cancelled.");
                throw;
            }
            finally
            {
                _executeProgress = null;
                IsExecuting = false;
                _operationLock.Release();
            }
        }


        /// <summary>
        /// Cancel the running task (Load or Execute)
        /// </summary>
        public async Task CancelAsync()
        {
            await _cancellationTokenSource.SafeCancelAsync();
            var backendClient = _backendClient;
            if (backendClient is not null)
                await backendClient.CancelAsync();
        }


        /// <summary>
        /// Unload the pipeline
        /// </summary>
        public async Task UnloadAsync()
        {
            // Interrupt a running load or execute, then wait for it to finish
            await CancelAsync();
            await _operationLock.WaitAsync();
            try
            {
                await UnloadBackendAsync();
                IsLoaded = false;
                IsLoading = false;
                IsExecuting = false;
            }
            finally
            {
                _operationLock.Release();
            }
        }


        /// <summary>
        /// Loads the extract model into a new backend host, the pipeline is set before the host loads so its owner and model are known while it is loading.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task LoadBackendAsync(PipelineModel pipeline, CancellationToken cancellationToken)
        {
            // Unload
            await UnloadBackendAsync();
            cancellationToken.ThrowIfCancellationRequested();
            _currentPipeline = pipeline;

            // Load, an extract-only pipeline, the host ignores the view's generate models
            var model = pipeline.ExtractModel;
            _logger.LogInformation("[ExtractService] [Load] Loading extract model {Name}, Backend: {Backend}, Type: {Type}...", model.Name, model.Backend, model.Type);
            var extractPipeline = new PipelineModel
            {
                Device = pipeline.Device,
                ExtractModel = model,
                ViewType = pipeline.ViewType,
                MemoryMode = pipeline.MemoryMode
            };
            _backendClient = CreateBackendClient(model.Backend);
            try
            {
                await _backendClient.LoadAsync(extractPipeline, new Progress<Amuse.Common.PipelineProgress>(OnHostProgress));
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception)
            {
                // Never leave a disconnected client behind
                await UnloadBackendAsync();
                throw;
            }

            // Result
            _defaultOptions = model.DefaultOptions;
            IsLoaded = true;
        }


        /// <summary>
        /// Runs a call on the backend host, stopping the host when the call fails in a way that requires it.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="backendCall">The backend call.</param>
        private async Task<T> RunOnBackendAsync<T>(Func<Task<T>> backendCall)
        {
            try
            {
                return await backendCall();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (IsHostFailed())
            {
                _logger.LogError(ex, "[ExtractService] [Execute] Extract failed, stopping the extract host.");
                await UnloadBackendOnErrorAsync();
                throw;
            }
        }


        /// <summary>
        /// Reports the progress of a frame stream as each frame is returned.
        /// </summary>
        /// <param name="frames">The frames.</param>
        /// <param name="frameCount">The frame count.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private static async IAsyncEnumerable<VideoFrame> ReportProgressAsync(IAsyncEnumerable<VideoFrame> frames, int frameCount, IProgress<RunProgress> progressCallback, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var frame in frames.WithCancellation(cancellationToken))
            {
                progressCallback?.Report(new RunProgress(frame.Index, frameCount));
                yield return frame;
            }
        }


        /// <summary>
        /// Gets the loaded extract model.
        /// </summary>
        /// <exception cref="InvalidOperationException">No extract model is loaded</exception>
        private ExtractModel EnsureLoaded()
        {
            if (!_isLoaded || _backendClient is null || _currentPipeline?.ExtractModel is null)
                throw new InvalidOperationException(AppErrors.ExtractModelNotLoaded);

            return _currentPipeline.ExtractModel;
        }


        /// <summary>
        /// Relays the host step progress to the current execute progress callback.
        /// </summary>
        /// <param name="progress">The host progress.</param>
        private void OnHostProgress(Amuse.Common.PipelineProgress progress)
        {
            if (_executeProgress is null || progress.Maximum <= 0)
                return;

            _executeProgress.Report(new RunProgress(progress.Value, progress.Maximum));
        }


        /// <summary>
        /// Creates the backend client for the extract model backend, the extract host runs separately from the generate and upscale hosts.
        /// </summary>
        /// <param name="backend">The backend.</param>
        private BackendClient CreateBackendClient(BackendType backend)
        {
            return backend switch
            {
                BackendType.OnnxRuntime => new OnnxBackendClient(_settings, _toolService, _mediaService, _logger, ServerType.OnnxRuntimeExtract),
                _ => throw new NotSupportedException(string.Format(AppErrors.ExtractBackendNotSupported, backend))
            };
        }


        /// <summary>
        /// Unloads the backend client and stops its host.
        /// </summary>
        private async Task UnloadBackendAsync()
        {
            try
            {
                if (_backendClient is not null)
                    await _backendClient.UnloadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExtractService] [Unload] An exception occurred unloading extract model.");
            }
            finally
            {
                _backendClient?.Dispose();
                _backendClient = null;
                _currentPipeline = null;
                _defaultOptions = null;
                IsLoaded = false;
            }
        }


        /// <summary>
        /// Determines whether an execution error requires the extract host to be stopped,
        /// the backend stops on any error, or the host connection was lost.
        /// </summary>
        private bool IsHostFailed()
        {
            return _backendClient is not null
                && (_backendClient.StopHostOnException || !_backendClient.IsConnected);
        }


        /// <summary>
        /// Stops the backend host after an execution error.
        /// </summary>
        private async Task UnloadBackendOnErrorAsync()
        {
            try
            {
                await _backendClient.StopAsync();
            }
            catch (Exception) { }
            finally
            {
                _backendClient?.Dispose();
                _backendClient = null;
                _currentPipeline = null;
                _defaultOptions = null;
                IsLoaded = false;
            }
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
