using Amuse.App.Common;
using Amuse.App.Runtime;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Tensor;
using TensorStack.Media.Image;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;

namespace Amuse.App.Services
{
    public sealed class GenerateService : ServiceBase, IGenerateService
    {
        private readonly ILogger _logger;
        private readonly Settings _settings;
        private readonly IMediaService _mediaService;
        private readonly IEnvironmentService _environmentService;
        private readonly IPreviewService _previewService;
        private bool _isLoaded;
        private bool _isLoading;
        private bool _isExecuting;
        private bool _isCanceling;
        private BackendClient _backenClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public GenerateService(Settings settings, IEnvironmentService environmentService, IMediaService mediaService, IPreviewService previewService, ILogger<GenerateService> logger)
        {
            _logger = logger;
            _settings = settings;
            _mediaService = mediaService;
            _environmentService = environmentService;
            _previewService = previewService;
        }

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
                    await _previewService.UnloadAsync();
                    await _backenClient.UnloadAsync();
                    DisposeRuntime();
                }

                _backenClient = pipeline.GenerateModel.Backend switch
                {
                    BackendType.HuggingFace => new HuggingFaceBackendClient(_settings, _mediaService, _environmentService, _logger),
                    BackendType.OnnxRuntime => new OnnxBackendClient(_settings, _mediaService, _logger),
                    BackendType.StableDiffusionCpp => new StableDiffusionCppClient(_settings, _mediaService, _environmentService, _logger),
                    _ => throw new NotImplementedException()
                };

                await _backenClient.LoadAsync(pipeline, progressCallback);
                await _previewService.LoadAsync(pipeline);
                IsLoaded = true;
            }
            catch (OperationCanceledException)
            {
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
            catch (OperationCanceledException)
            {
                await _previewService.UnloadAsync();
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
        public async Task<ImageTensor> GenerateImageAsync(GenerateInputOptions options)
        {
            IsExecuting = true;
            IsCanceling = false;
            try
            {
                return await _backenClient.GenerateImageAsync(options);
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


        public async Task<VideoInputStream> GenerateVideoAsync(GenerateInputOptions options)
        {
            IsExecuting = true;
            IsCanceling = false;
            try
            {
                return await _backenClient.GenerateVideoAsync(options);
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


        public async Task<AudioInputStream> GenerateAudioAsync(GenerateInputOptions options)
        {
            IsExecuting = true;
            IsCanceling = false;
            try
            {
                return await _backenClient.GenerateAudioAsync(options);
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


        public async Task<TextResult> GenerateTextAsync(GenerateInputOptions options)
        {
            IsExecuting = true;
            IsCanceling = false;
            try
            {
                return await _backenClient.GenerateTextAsync(options);
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


        public async Task<ImageInput> GeneratePreviewAsync(PipelineProgress progress)
        {
            if (progress.Tensors.IsNullOrEmpty())
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
        /// Stop/Kill server
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                await _previewService.UnloadAsync();
                await _backenClient.StopAsync();
            }
            catch (Exception) { }
            finally
            {
                IsLoaded = false;
                IsLoading = false;
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
            await _previewService.UnloadAsync();
            await _backenClient.UnloadAsync();
            IsLoaded = false;
            IsLoading = false;
            IsExecuting = false;
            IsCanceling = false;
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
        Task<ImageInput> GeneratePreviewAsync(PipelineProgress progress);
    }
}
