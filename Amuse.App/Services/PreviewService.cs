using Amuse.App.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Tensor;
using TensorStack.WPF.Image;

namespace Amuse.App.Services
{
    /// <summary>
    /// Turns the step tensors a StableDiffusion.cpp pipeline reports into preview images.
    /// </summary>
    public sealed class PreviewService : IPreviewService
    {
        private readonly ILogger<PreviewService> _logger;
        private readonly SemaphoreSlim _asyncLock;
        private BackendType? _backendType;

        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewService"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public PreviewService(ILogger<PreviewService> logger)
        {
            _logger = logger;
            _asyncLock = new SemaphoreSlim(1, 1);
        }


        /// <summary>
        /// Starts previews for a pipeline, only a StableDiffusion.cpp diffusion pipeline reports step images.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public Task LoadAsync(PipelineModel pipeline, CancellationToken cancellationToken = default)
        {
            _backendType = pipeline.DiffusionModel == null ? null : pipeline.GenerateModel.Backend;
            return Task.CompletedTask;
        }


        /// <summary>
        /// Stops previews.
        /// </summary>
        public Task UnloadAsync()
        {
            _backendType = null;
            return Task.CompletedTask;
        }


        /// <summary>
        /// Generates the preview image of a step tensor, a step is skipped while the previous one is still rendering.
        /// </summary>
        /// <param name="inputTensor">The step tensor.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task<ImageInput> GenerateAsync(Tensor<float> inputTensor, CancellationToken cancellationToken = default)
        {
            if (_backendType != BackendType.StableDiffusionCpp)
                return default;

            if (!await _asyncLock.WaitAsync(0, cancellationToken))
                return default;

            try
            {
                return await inputTensor
                    .AsImageTensor()
                    .ToImageInputAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PreviewService] [Generate] Generating preview image failed");
                return default;
            }
            finally
            {
                _asyncLock.Release();
            }
        }


        /// <summary>
        /// Releases the preview lock.
        /// </summary>
        public void Dispose()
        {
            _asyncLock?.Dispose();
        }
    }


    public interface IPreviewService : IDisposable
    {
        Task UnloadAsync();
        Task LoadAsync(PipelineModel pipeline, CancellationToken cancellationToken = default);
        Task<ImageInput> GenerateAsync(Tensor<float> inputTensor, CancellationToken cancellationToken = default);
    }
}
