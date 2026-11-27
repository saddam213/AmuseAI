using Amuse.App.Common;
using Amuse.App.Runtime;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Tensor;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.WPF.Image;

namespace Amuse.App.Services
{
    /// <summary>
    /// Base for the services that own a backend client, each service runs its own host so services of different types can be loaded at the same time.
    /// </summary>
    public abstract class GenerateServiceBase : ServiceBase, IGenerateService
    {
        private readonly IPreviewService _previewService;
        private bool _isLoaded;
        private bool _isLoading;
        private bool _isExecuting;
        private bool _isCanceling;
        private BackendClient _backenClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateServiceBase"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="environmentService">The environment service.</param>
        /// <param name="toolService">The tool service.</param>
        /// <param name="mediaService">The media service.</param>
        /// <param name="previewService">The preview service, null when the service has no diffusion previews.</param>
        /// <param name="logger">The logger.</param>
        protected GenerateServiceBase(Settings settings, IEnvironmentService environmentService, IToolService toolService, IMediaService mediaService, IPreviewService previewService, ILogger logger)
        {
            Logger = logger;
            Settings = settings;
            ToolService = toolService;
            MediaService = mediaService;
            EnvironmentService = environmentService;
            _previewService = previewService;
        }

        protected ILogger Logger { get; }
        protected Settings Settings { get; }
        protected IToolService ToolService { get; }
        protected IMediaService MediaService { get; }
        protected IEnvironmentService EnvironmentService { get; }

        /// <summary>
        /// Gets the pipeline.
        /// </summary>
        public PipelineModel Pipeline => _backenClient?.Pipeline;

        /// <summary>
        /// Gets the default options.
        /// </summary>
        public GenerateDefaultOptions DefaultOptions => _backenClient?.DefaultOptions;

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
        /// Gets a value indicating whether this instance is canceling.
        /// </summary>
        public bool IsCanceling
        {
            get { return _isCanceling; }
            private set { SetProperty(ref _isCanceling, value); NotifyPropertyChanged(nameof(CanCancel)); }
        }

        /// <summary>
        /// Gets a value indicating whether this instance can cancel.
        /// </summary>
        public bool CanCancel => _isLoading || _isExecuting;


        /// <summary>
        /// Load the pipeline
        /// </summary>
        /// <param name="config">The configuration.</param>
        public async Task LoadAsync(PipelineModel pipeline, IProgress<PipelineProgress> progressCallback)
        {
            IsLoaded = false;
            IsLoading = true;
            IsCanceling = false;
            try
            {
                if (_backenClient != null)
                {
                    await UnloadPreviewAsync();
                    await _backenClient.UnloadAsync();
                    DisposeRuntime();
                }

                _backenClient = CreateBackendClient(pipeline.GenerateModel.Backend);
                await _backenClient.LoadAsync(pipeline, progressCallback);
                await LoadPreviewAsync(pipeline);
                IsLoaded = true;
            }
            catch (Exception)
            {
                await UnloadPreviewAsync();
                DisposeRuntime();
                throw;
            }
            finally
            {
                IsLoading = false;
                IsCanceling = false;
            }
        }


        /// <summary>
        /// Reload the pipeline
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        public async Task ReloadAsync(PipelineModel pipeline, IProgress<PipelineProgress> progressCallback)
        {
            IsLoaded = false;
            IsLoading = true;
            IsCanceling = false;
            try
            {
                await _backenClient.ReloadAsync(pipeline, progressCallback);
                IsLoaded = true;
            }
            catch (Exception)
            {
                await UnloadPreviewAsync();
                DisposeRuntime();
                throw;
            }
            finally
            {
                IsLoading = false;
                IsCanceling = false;
            }
        }


        public async Task UpdateAsync(PipelineModel pipeline)
        {
            await _backenClient.UpdateAsync(pipeline);
        }


        /// <summary>
        /// Execute the upscaler
        /// </summary>
        /// <param name="request">The request.</param>
        public Task<ImageTensor> GenerateImageAsync(GenerateInputOptions options)
        {
            return ExecuteAsync(() => _backenClient.GenerateImageAsync(options));
        }


        public Task<VideoInputStream> GenerateVideoAsync(GenerateInputOptions options)
        {
            return ExecuteAsync(() => _backenClient.GenerateVideoAsync(options));
        }


        public Task<AudioInputStream> GenerateAudioAsync(GenerateInputOptions options)
        {
            return ExecuteAsync(() => _backenClient.GenerateAudioAsync(options));
        }


        public Task<TextResult> GenerateTextAsync(GenerateInputOptions options)
        {
            return ExecuteAsync(() => _backenClient.GenerateTextAsync(options));
        }


        /// <summary>
        /// Runs a generate call on the backend, stopping the host when the call fails and the backend requires it.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="generateAsync">The generate call.</param>
        private async Task<T> ExecuteAsync<T>(Func<Task<T>> generateAsync)
        {
            IsExecuting = true;
            IsCanceling = false;
            try
            {
                return await generateAsync();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                if (_backenClient?.StopHostOnException == true)
                    await StopAsync();

                throw;
            }
            finally
            {
                IsExecuting = false;
                IsCanceling = false;
            }
        }


        /// <summary>
        /// Generate text from live audio, the text is returned as it is recognised.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="inputAudio">The audio chunks.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async IAsyncEnumerable<string> GenerateTextStreamAsync(GenerateInputOptions options, IAsyncEnumerable<AudioTensor> inputAudio, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            IsExecuting = true;
            IsCanceling = false;
            var results = _backenClient.GenerateTextStreamAsync(options, inputAudio, cancellationToken).GetAsyncEnumerator(cancellationToken);
            try
            {
                while (true)
                {
                    try
                    {
                        if (!await results.MoveNextAsync())
                            break;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        if (_backenClient?.StopHostOnException == true)
                            await StopAsync();

                        throw;
                    }
                    yield return results.Current;
                }
            }
            finally
            {
                await results.DisposeAsync();
                IsExecuting = false;
                IsCanceling = false;
            }
        }


        public async Task<ImageInput> GeneratePreviewAsync(PipelineProgress progress)
        {
            if (_previewService is null || progress.Tensors.IsNullOrEmpty())
                return default;

            return await _previewService.GenerateAsync(progress.Tensors[0]);
        }


        /// <summary>
        /// Cancel the running task (Load or Execute)
        /// </summary>
        public async Task CancelAsync()
        {
            try
            {
                IsCanceling = true;
                await _backenClient.CancelAsync();
            }
            catch (Exception) { }
        }


        /// <summary>
        /// Stop/Kill server, a load that is running stays loading until it has unwound, so another load cannot start over it.
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                await UnloadPreviewAsync();
                await _backenClient.StopAsync();
            }
            catch (Exception) { }
            finally
            {
                IsLoaded = false;
                IsExecuting = false;
                IsCanceling = false;
                DisposeRuntime();
            }
        }


        /// <summary>
        /// Unload the pipeline
        /// </summary>
        public async Task UnloadAsync()
        {
            await UnloadPreviewAsync();
            if (_backenClient is not null)
                await _backenClient.UnloadAsync();

            IsLoaded = false;
            IsLoading = false;
            IsExecuting = false;
            IsCanceling = false;
        }


        /// <summary>
        /// Creates the backend client for the backend.
        /// </summary>
        /// <param name="backend">The backend.</param>
        protected abstract BackendClient CreateBackendClient(BackendType backend);


        /// <summary>
        /// Loads the preview model for the pipeline.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        private async Task LoadPreviewAsync(PipelineModel pipeline)
        {
            if (_previewService is null)
                return;

            await _previewService.LoadAsync(pipeline);
        }


        /// <summary>
        /// Unloads the preview model.
        /// </summary>
        private async Task UnloadPreviewAsync()
        {
            if (_previewService is null)
                return;

            await _previewService.UnloadAsync();
        }


        private void DisposeRuntime()
        {
            _backenClient?.Dispose();
            _backenClient = null;
        }


        public void Dispose()
        {
            _previewService?.Dispose();
            DisposeRuntime();
        }
    }


    public interface IGenerateService
    {
        bool IsLoaded { get; }
        bool IsLoading { get; }
        bool IsExecuting { get; }
        bool IsCanceling { get; }
        bool CanCancel { get; }
        PipelineModel Pipeline { get; }
        GenerateDefaultOptions DefaultOptions { get; }
        Task LoadAsync(PipelineModel pipeline, IProgress<PipelineProgress> progressCallback);
        Task ReloadAsync(PipelineModel pipeline, IProgress<PipelineProgress> progressCallback);
        Task UpdateAsync(PipelineModel pipeline);
        Task UnloadAsync();
        Task CancelAsync();
        Task StopAsync();
        Task<ImageTensor> GenerateImageAsync(GenerateInputOptions options);
        Task<VideoInputStream> GenerateVideoAsync(GenerateInputOptions options);
        Task<AudioInputStream> GenerateAudioAsync(GenerateInputOptions options);
        Task<TextResult> GenerateTextAsync(GenerateInputOptions options);
        IAsyncEnumerable<string> GenerateTextStreamAsync(GenerateInputOptions options, IAsyncEnumerable<AudioTensor> inputAudio, CancellationToken cancellationToken = default);
        Task<ImageInput> GeneratePreviewAsync(PipelineProgress progress);
    }
}
