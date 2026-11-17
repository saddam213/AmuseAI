using Amuse.App.Common;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Tensor;
using TensorStack.OnnxRuntime;
using TensorStack.WPF.Image;

namespace Amuse.App.Services
{
    public sealed class PreviewService : IPreviewService
    {
        private readonly Settings _settings;
        private readonly ILogger<PreviewService> _logger;
        private readonly SemaphoreSlim _asyncLock;
        private readonly string _previewModelDirectory;
        private ModelSession<ModelConfig> _previewModelSession;
        private MediaType _mediaType;
        private PipelineType _pipelineType;
        private BackendType _backendType;

        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="logger">The logger.</param>
        public PreviewService(Settings settings, ILogger<PreviewService> logger)
        {
            _logger = logger;
            _settings = settings;
            _asyncLock = new SemaphoreSlim(1, 1);
            _previewModelDirectory = Path.Combine(App.DirectoryData, "Plugins", "Preview");
        }


        /// <summary>
        /// Load the preview model
        /// </summary>
        /// <param name="pipelineType">Type of the pipeline.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task LoadAsync(PipelineModel pipeline, CancellationToken cancellationToken = default)
        {
            if (!_settings.IsDiffusionImagePreviewEnabled || pipeline.DiffusionModel == null)
                return;

            var timestamp = Stopwatch.GetTimestamp();
            _mediaType = pipeline.DiffusionModel.MediaType;
            _pipelineType = pipeline.DiffusionModel.Pipeline;
            _backendType = pipeline.GenerateModel.Backend;

            try
            {
                _previewModelSession = CreateModelSession(pipeline.Device);
                if (_previewModelSession != null)
                {
                    await _previewModelSession.LoadAsync(cancellationToken: cancellationToken);
                    _logger.LogInformation("[Load] {pipelineType} preview model loaded, Elapsed: {Elapsed:c}", _pipelineType, Stopwatch.GetElapsedTime(timestamp));
                }
            }
            catch (Exception ex)
            {
                _previewModelSession?.Dispose();
                _previewModelSession = null;
                _logger.LogError(ex, "[Load] An exception occured loading preview model, Pipeline: {pipelineType}", _pipelineType);
            }
        }


        /// <summary>
        /// Unload the pipeline model
        /// </summary>
        public async Task UnloadAsync()
        {
            if (_previewModelSession == null)
                return;

            try
            {
                _logger.LogInformation("[Unload] Unloading preview model...");
                await _previewModelSession.UnloadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Unload] An exception occured unloading preview model");
            }
            finally
            {
                _previewModelSession?.Dispose();
                _previewModelSession = null;
                _logger.LogInformation("[Unload] Preview model unloaded.");
            }
        }


        /// <summary>
        /// Generate the preview image.
        /// </summary>
        /// <param name="inputTensor">The input tensor.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task<ImageInput> GenerateAsync(Tensor<float> inputTensor, CancellationToken cancellationToken = default)
        {
            if (!_settings.IsDiffusionImagePreviewEnabled)
                return default;

            if (!await _asyncLock.WaitAsync(0, cancellationToken))
                return default; // Skip current preview if previous is still rendering

            var timestamp = Stopwatch.GetTimestamp();
            try
            {
                if (_backendType == BackendType.StableDiffusionCpp)
                {
                    return await inputTensor
                        .AsImageTensor()
                        .ToImageInputAsync();
                }

                switch (_pipelineType)
                {
                    case PipelineType.GlmImagePipeline:
                        return default; // not supported

                    // OnnxRuntime Inference
                    case PipelineType.StableDiffusionPipeline:
                    case PipelineType.LatentConsistencyPipeline:
                    case PipelineType.StableDiffusionXLPipeline:
                    case PipelineType.StableDiffusion3Pipeline:
                    case PipelineType.FluxPipeline:
                    case PipelineType.ChromaPipeline:
                    case PipelineType.ZImagePipeline:
                    case PipelineType.Flux2Pipeline:
                    case PipelineType.Flux2KleinPipeline:
                    case PipelineType.ErniePipeline:
                    case PipelineType.IdeogramPipeline:
                    case PipelineType.AnimaPipeline:
                    case PipelineType.QwenImagePipeline:
                    case PipelineType.Krea2Pipeline:
                    case PipelineType.Kandinsky5ImagePipeline:
                    case PipelineType.JoyImagePipeline:
                        return await RunInferenceAsync(inputTensor, cancellationToken);

                    // Pixel Space
                    case PipelineType.PrxPixelPipeline:
                        return await inputTensor
                        .AsImageTensor()
                        .ToImageInputAsync();

                    default:
                        break;
                }

                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Generate] An exception occured generation preview tensor.");
                return null;
            }
            finally
            {
                _asyncLock.Release();
                _logger.LogInformation("[Generate] Generating preview image complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
        }


        /// <summary>
        /// Creates the model session.
        /// </summary>
        /// <param name="device">The device.</param>
        private ModelSession<ModelConfig> CreateModelSession(DeviceModel device)
        {
            if (_backendType == BackendType.StableDiffusionCpp || _backendType == BackendType.AudioCpp)
                return null;

            var previewModelPath = GetModelPath();
            if (!File.Exists(previewModelPath))
            {
                _logger.LogInformation("[Load] No preview model found for {pipelineType}", _pipelineType);
                return null;
            }

            _logger.LogInformation("[Load] Loading {pipelineType} preview model...", _pipelineType);
            return new ModelSession<ModelConfig>(new ModelConfig
            {
                Path = previewModelPath,
                ExecutionProvider = device.GetProvider(Microsoft.ML.OnnxRuntime.GraphOptimizationLevel.ORT_DISABLE_ALL),
            });
        }


        /// <summary>
        /// Run inference
        /// </summary>
        /// <param name="inputTensor">The input tensor.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task<ImageInput> RunInferenceAsync(Tensor<float> inputTensor, CancellationToken cancellationToken = default)
        {
            if (_previewModelSession == null || !_previewModelSession.IsLoaded())
                return default;

            using (var modelParameters = new ModelParameters(_previewModelSession.Metadata, cancellationToken))
            {
                modelParameters.AddInput(inputTensor);
                modelParameters.AddOutput();
                using (var results = _previewModelSession.RunInference(modelParameters))
                {
                    return await results[0]
                        .ToTensor()
                        .AsImageTensor()
                        .ToImageInputAsync();
                }
            }
        }


        /// <summary>
        /// Gets the model path.
        /// </summary>
        private string GetModelPath()
        {
            switch (_pipelineType)
            {
                case PipelineType.GlmImagePipeline:
                    return null; // not supported

                case PipelineType.StableDiffusionPipeline:
                case PipelineType.LatentConsistencyPipeline:
                    return Path.Combine(_previewModelDirectory, "StableDiffusion.onnx");
                case PipelineType.StableDiffusionXLPipeline:
                    return Path.Combine(_previewModelDirectory, "StableDiffusionXL.onnx");
                case PipelineType.StableDiffusion3Pipeline:
                    return Path.Combine(_previewModelDirectory, "StableDiffusion3.onnx");
                case PipelineType.FluxPipeline:
                case PipelineType.ChromaPipeline:
                case PipelineType.ZImagePipeline:
                    return Path.Combine(_previewModelDirectory, "Flux1.onnx");
                case PipelineType.Flux2Pipeline:
                case PipelineType.Flux2KleinPipeline:
                case PipelineType.ErniePipeline:
                case PipelineType.IdeogramPipeline:
                    return Path.Combine(_previewModelDirectory, "Flux2.onnx");
                case PipelineType.AnimaPipeline:
                case PipelineType.QwenImagePipeline:
                case PipelineType.Krea2Pipeline:
                case PipelineType.JoyImagePipeline:
                    return Path.Combine(_previewModelDirectory, "Qwen.onnx");
                case PipelineType.Kandinsky5ImagePipeline:
                    return Path.Combine(_previewModelDirectory, "Flux1.onnx");
            }
            return null;
        }


        public void Dispose()
        {
            _asyncLock?.Dispose();
            _previewModelSession?.Dispose();
            _previewModelSession = null;
        }
    }


    public interface IPreviewService : IDisposable
    {
        Task UnloadAsync();
        Task LoadAsync(PipelineModel pipeline, CancellationToken cancellationToken = default);
        Task<ImageInput> GenerateAsync(Tensor<float> inputTensor, CancellationToken cancellationToken = default);
    }
}
