using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Runtime;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
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
    public sealed class UpscaleService : ServiceBase, IUpscaleService
    {
        private readonly ILogger _logger;
        private readonly Settings _settings;
        private readonly IToolService _toolService;
        private readonly IMediaService _mediaService;
        private readonly IEnvironmentService _environmentService;
        private readonly IGenerateService _generateService;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private PipelineModel _currentPipeline;
        private BackendClient _backendClient;
        private CancellationTokenSource _cancellationTokenSource;
        private IProgress<RunProgress> _executeProgress;
        private int _loadedTileSize;
        private bool _isLoadedUntiled;
        private bool _isLoadDeferred;
        private bool _isLoaded;
        private bool _isLoading;
        private bool _isExecuting;
        private UpscaleInputOptions _defaultOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpscaleService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="environmentService">The environment service.</param>
        /// <param name="toolService">The tool service.</param>
        /// <param name="mediaService">The media service.</param>
        /// <param name="generateService">The generate service, its loaded model shares the device memory.</param>
        /// <param name="logger">The logger.</param>
        public UpscaleService(Settings settings, IEnvironmentService environmentService, IToolService toolService, IMediaService mediaService, IGenerateService generateService, ILogger<UpscaleService> logger)
        {
            _logger = logger;
            _settings = settings;
            _toolService = toolService;
            _mediaService = mediaService;
            _environmentService = environmentService;
            _generateService = generateService;
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
        /// Load the upscale pipeline into its backend host.
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
                    // Deferred, a disabled tile option resolves to the input size, the first execute loads the host
                    var defaultOptions = pipeline.UpscaleModel.DefaultOptions;
                    if (IsLoadTileSize(pipeline.UpscaleModel) && !defaultOptions.IsTileEnabled)
                    {
                        await DeferLoadAsync(pipeline);
                        _logger.LogInformation("[UpscaleService] [Load] Upscale model load deferred until the input size is known, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                        return;
                    }

                    // Load
                    var tileSize = defaultOptions?.IsTileEnabled == true ? defaultOptions.TileSize : 0;
                    await LoadBackendAsync(pipeline, tileSize, false, _cancellationTokenSource.Token);
                    _logger.LogInformation("[UpscaleService] [Load] Upscale model loaded, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[UpscaleService] [Load] Loading upscale model cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UpscaleService] [Load] An exception occurred loading upscale model, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                throw;
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }


        /// <summary>
        /// Execute the upscaler on an image.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="progressCallback">The progress callback.</param>
        public async Task<ImageTensor> ExecuteAsync(UpscaleImageRequest request, IProgress<RunProgress> progressCallback)
        {
            await _operationLock.WaitAsync();
            try
            {
                IsExecuting = true;
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    var cancellationToken = _cancellationTokenSource.Token;
                    var model = GetLoadedModel();
                    if (model.IsVideoUpscaler)
                        throw new NotSupportedException(string.Format(AppErrors.UpscaleVideoOnly, model.Name));

                    // Tile size
                    await EnsureTileSizeAsync(request.Options, request.Image.Width, request.Image.Height, cancellationToken);

                    // Upscale
                    _executeProgress = progressCallback;
                    return await RunOnBackendAsync(() => _backendClient.UpscaleImageAsync(request.Image, request.Options));
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[UpscaleService] [Execute] Image upscale cancelled.");
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
        /// Execute the upscaler on a video.
        /// </summary>
        /// <param name="request">The options.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <returns>A Task&lt;VideoInputStream&gt; representing the asynchronous operation.</returns>
        public async Task<VideoInputStream> ExecuteAsync(UpscaleVideoRequest request, IProgress<RunProgress> progressCallback)
        {
            await _operationLock.WaitAsync();
            try
            {
                IsExecuting = true;
                var videoFileName = _mediaService.GetTempFile(MediaType.Video);
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    var cancellationToken = _cancellationTokenSource.Token;
                    var model = GetLoadedModel();
                    if (model.IsVideoUpscaler)
                    {
                        // Video upscaler, the whole video in one pass
                        var videoSequence = await request.VideoStream.GetSequenceAsync(includeAudio: true, cancellationToken: cancellationToken);
                        _executeProgress = progressCallback;
                        var resultVideo = await RunOnBackendAsync(() => _backendClient.UpscaleVideoAsync(videoSequence, request.Options));
                        await resultVideo.SaveAsync(videoFileName, cancellationToken);
                        return new VideoInputStream(videoFileName);
                    }

                    // Tile size
                    await EnsureTileSizeAsync(request.Options, request.VideoStream.Width, request.VideoStream.Height, cancellationToken);

                    // Image upscaler, frame by frame
                    var frameCount = request.VideoStream.FrameCount;
                    async Task<VideoFrame> FrameProcessor(VideoFrame frame)
                    {
                        var processedFrame = await RunOnBackendAsync(() => _backendClient.UpscaleImageAsync(frame.Frame, request.Options));
                        progressCallback?.Report(new RunProgress(frame.Index, frameCount));
                        return new VideoFrame(frame.Index, processedFrame, frame.SourceFrameRate, frame.AuxFrame);
                    }

                    return await request.VideoStream.ProcessAsync(videoFileName, FrameProcessor, true, _settings.ReadBuffer, _settings.WriteBuffer, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[UpscaleService] [Execute] Video upscale cancelled.");
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
        /// Loads the upscale model into a new backend host.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="tileSize">The tile size, StableDiffusionCpp sets it when the model loads.</param>
        /// <param name="isUntiled">Whether the tile size is a single tile covering the input, tiling disabled.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task LoadBackendAsync(PipelineModel pipeline, int tileSize, bool isUntiled, CancellationToken cancellationToken)
        {
            // Unload
            await UnloadBackendAsync();
            cancellationToken.ThrowIfCancellationRequested();

            // Tile size is a load option for StableDiffusionCpp image upscalers
            var model = pipeline.UpscaleModel;
            var loadModel = model;
            if (IsLoadTileSize(model))
            {
                loadModel = model.DeepClone(model.Id);
                loadModel.DefaultOptions.TileSize = tileSize;
            }

            // Load, upscalers use the backend defaults rather than the view's execution options
            var memoryMode = GetMemoryMode(pipeline);
            _logger.LogInformation("[UpscaleService] [Load] Loading upscale model {Name}, Backend: {Backend}, MemoryMode: {MemoryMode}, TileSize: {TileSize}...", model.Name, model.Backend, memoryMode, tileSize);
            var upscalePipeline = new PipelineModel
            {
                Device = pipeline.Device,
                UpscaleModel = loadModel,
                ViewType = pipeline.ViewType,
                MemoryMode = memoryMode
            };
            _backendClient = CreateBackendClient(model.Backend);
            try
            {
                await _backendClient.LoadAsync(upscalePipeline, new Progress<Amuse.Common.PipelineProgress>(OnHostProgress));
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception)
            {
                // Never leave a disconnected client behind
                await UnloadBackendAsync();
                throw;
            }

            // Result
            _currentPipeline = pipeline;
            _defaultOptions = model.DefaultOptions;
            _loadedTileSize = tileSize;
            _isLoadedUntiled = isUntiled;
            IsLoaded = true;
        }


        /// <summary>
        /// Selects a StableDiffusionCpp image upscaler without starting its host, the untiled tile size
        /// is the input size, so the first execute loads it once at that size.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        private async Task DeferLoadAsync(PipelineModel pipeline)
        {
            // Unload
            await UnloadBackendAsync();

            // Result, an untiled zero tile size is reloaded by the first execute
            _currentPipeline = pipeline;
            _defaultOptions = pipeline.UpscaleModel.DefaultOptions;
            _loadedTileSize = 0;
            _isLoadedUntiled = true;
            _isLoadDeferred = true;
            IsLoaded = true;
        }


        /// <summary>
        /// Reloads a StableDiffusionCpp image upscaler when the requested tile size cannot use the loaded one.
        /// </summary>
        /// <param name="options">The upscale options.</param>
        /// <param name="width">The input width.</param>
        /// <param name="height">The input height.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task EnsureTileSizeAsync(UpscaleInputOptions options, int width, int height, CancellationToken cancellationToken)
        {
            var pipeline = _currentPipeline;
            if (options is null || !IsLoadTileSize(pipeline.UpscaleModel))
                return;

            int tileSize;
            bool isUntiled;
            if (options.IsTileEnabled)
            {
                // Tiling enabled, the requested tile size
                tileSize = Math.Max(0, options.TileSize);
                isUntiled = false;
                if (!_isLoadedUntiled && tileSize == _loadedTileSize)
                    return;
            }
            else
            {
                // Tiling disabled, a single tile covering the input, a loaded tile at least as large is reused
                var inputSize = Math.Max(width, height);
                if (_isLoadedUntiled && _loadedTileSize >= inputSize)
                    return;

                tileSize = _isLoadedUntiled ? Math.Max(inputSize, _loadedTileSize) : inputSize;
                isUntiled = true;
            }

            var timestamp = Stopwatch.GetTimestamp();
            _logger.LogInformation("[UpscaleService] [Execute] Tile size changed from {OldTileSize} to {TileSize}, reloading upscale model...", _loadedTileSize, tileSize);
            await LoadBackendAsync(pipeline, tileSize, isUntiled, cancellationToken);
            _logger.LogInformation("[UpscaleService] [Execute] Upscale model reloaded, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
                _logger.LogError(ex, "[UpscaleService] [Execute] Upscale failed, stopping the upscale host.");
                await UnloadBackendOnErrorAsync();
                throw;
            }
        }


        /// <summary>
        /// Gets the loaded upscale model.
        /// </summary>
        /// <exception cref="InvalidOperationException">No upscale model is loaded</exception>
        private UpscaleModel GetLoadedModel()
        {
            if (!_isLoaded || (_backendClient is null && !_isLoadDeferred) || _currentPipeline?.UpscaleModel is null)
                throw new InvalidOperationException(AppErrors.UpscaleModelNotLoaded);

            return _currentPipeline.UpscaleModel;
        }


        /// <summary>
        /// Gets the memory mode for the upscaler, from the device memory left by the generate model loaded on the same device.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        private MemoryMode GetMemoryMode(PipelineModel pipeline)
        {
            var device = pipeline.Device;
            var generatePipeline = _generateService.Pipeline;
            var reservedMemory = generatePipeline?.Device == device
                ? generatePipeline.GenerateModel?.MemorySize ?? 0
                : 0;
            var availableMemory = Math.Max(0, device.MemoryGB - (int)Math.Ceiling(reservedMemory));
            return Extensions.ResolveMemoryMode(pipeline.UpscaleModel.MemorySize, availableMemory);
        }


        /// <summary>
        /// Determines whether the tile size is set when the model loads, StableDiffusionCpp image upscalers.
        /// </summary>
        /// <param name="model">The model.</param>
        private static bool IsLoadTileSize(UpscaleModel model)
        {
            return model is not null
                && model.DefaultOptions is not null
                && model.Backend == BackendType.StableDiffusionCpp
                && !model.IsVideoUpscaler;
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
        /// Creates the backend client for the upscale model backend, upscale hosts run separately from the generate hosts.
        /// </summary>
        /// <param name="backend">The backend.</param>
        private BackendClient CreateBackendClient(BackendType backend)
        {
            return backend switch
            {
                BackendType.OnnxRuntime => new OnnxBackendClient(_settings, _toolService, _mediaService, _logger, ServerType.OnnxRuntimeUpscale),
                BackendType.StableDiffusionCpp => new StableDiffusionCppClient(_settings, _toolService, _mediaService, _environmentService, _logger, ServerType.StableDiffusionCppUpscale),
                _ => throw new NotSupportedException(string.Format(AppErrors.UpscaleBackendNotSupported, backend))
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
                _logger.LogError(ex, "[UpscaleService] [Unload] An exception occurred unloading upscale model.");
            }
            finally
            {
                _backendClient?.Dispose();
                _backendClient = null;
                _currentPipeline = null;
                _defaultOptions = null;
                _loadedTileSize = 0;
                _isLoadedUntiled = false;
                _isLoadDeferred = false;
                IsLoaded = false;
            }
        }


        /// <summary>
        /// Determines whether an execution error requires the upscale host to be stopped,
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
                _loadedTileSize = 0;
                _isLoadedUntiled = false;
                _isLoadDeferred = false;
                IsLoaded = false;
            }
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
