using Amuse.Common;
using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common.Pipeline;
using TensorStack.StableDiffusionCpp;

namespace Amuse.Host.StableDiffusionCpp
{
    public sealed class HostServer : PipelineServer
    {
        private readonly IProgress<IRunProgress> _progressCallback;
        private PipelineCreateOptions _options;
        private PipelineLoadOptions _pipelineOptions;
        private StableDiffusionPipeline _pipeline;

        /// <summary>
        /// Initializes a new instance of the <see cref="HostServer"/> class.
        /// </summary>
        /// <param name="channelConfig">The channel configuration.</param>
        /// <param name="logger">The logger.</param>
        public HostServer(ServerConfig channelConfig, ILogger logger)
            : base(channelConfig, logger)
        {
            _progressCallback = new Progress<IRunProgress>(async (p) => await UpdateProgress(p));
        }


        /// <summary>
        /// Called when the Channel is opened.
        /// </summary>
        /// <returns>Task.</returns>
        protected override Task ChannelOpenedAsync()
        {
            return Task.CompletedTask;
        }


        /// <summary>
        /// Called when the Channel is closed.
        /// </summary>
        protected override Task ChannelClosedAsync()
        {
            if (_pipeline != null)
                _pipeline.Dispose();

            return Task.CompletedTask;
        }


        /// <summary>
        /// Create Pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override async Task CreatePipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await SendLoadingProgress("Creating Environment...");

                var timestamp = Stopwatch.GetTimestamp();
                var configuration = new TensorStack.Common.BackendConfig
                {
                    Name = request.CreateOptions.Environment,
                    Requirements = request.CreateOptions.Requirements,
                    Directory = request.CreateOptions.Directory
                };

                var installManager = new StableDiffusionBackend(configuration, _progressCallback, Logger);
                if (!await installManager.SetupAsync(request.CreateOptions.Status))
                    throw new Exception($"Failed to Install StableDiffusion.Cpp {request.CreateOptions.HostVersion}");

                _options = request.CreateOptions;
                Logger.LogInformation($"[AmuseHost] [PipelineServer] [CreatePipeline] Environment created, Elapsed: {Stopwatch.GetElapsedTime(timestamp)}");
                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[AmuseHost] [PipelineServer] [CreatePipeline] An exception occurred creating environment.");
                await SendException(ex, cancellationToken);
            }
        }


        /// <summary>
        /// Loads the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override async Task LoadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await SendLoadingProgress();
                _pipelineOptions = request.LoadOptions;

                var contextOptions = _options.CreateContextOptions(_pipelineOptions);
                var backendDirectory = Path.Combine(_options.Directory, _options.Environment);
                _pipeline = new StableDiffusionPipeline(backendDirectory, _progressCallback, Logger);
                await _pipeline.LoadAsync(contextOptions, cancellationToken: cancellationToken);

                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                _pipeline?.Dispose();
                Logger.LogError(ex, "[AmuseHost] [PipelineServer] [LoadPipeline] An exception occurred loading pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        /// <summary>
        /// Reload the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override async Task ReloadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await SendLoadingProgress();
                var reloadOptions = request.ReloadOptions;
                _pipelineOptions.LoraAdapters = reloadOptions.LoraAdapters;
                _pipelineOptions.ProcessType = reloadOptions.ProcessType;
                _pipelineOptions.ControlNet = reloadOptions.ControlNet; // TODO: Reload model/controlnet
                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[AmuseHost] [PipelineServer] [ReloadPipeline] An exception occurred reloading pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        /// <summary>
        /// Unloads the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override async Task UnloadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                _pipeline.Unload();
                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[AmuseHost] [PipelineServer] [UnloadPipeline] An exception occurred unloading pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        /// <summary>
        /// Runs the pipeline
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override async Task RunPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await SendLoadingProgress();
                using (PipelineCancellation = new CancellationTokenSource())
                {
                    ReadTensorRequest(request);

                    if (request.RunOptions.ImageOptions != null)
                    {
                        var options = request.RunOptions.ImageOptions;
                        var generateOptions = _pipeline.DefaultImageOptions.CreateImageOptions(options, _pipelineOptions);
                        var imageResult = await _pipeline.GenerateImageAsync(generateOptions, PipelineCancellation.Token);
                        await SendTensorResponse(cancellationToken, imageResult);
                    }
                    else if (request.RunOptions.VideoOptions != null)
                    {
                        var options = request.RunOptions.VideoOptions;
                        var generateVideoOptions = _pipeline.DefaultVideoOptions.CreateVideoOptions(options, _pipelineOptions);
                        var videoResult = await _pipeline.GenerateVideoAsync(generateVideoOptions, PipelineCancellation.Token);
                        await SendTensorResponse(cancellationToken, videoResult);
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                Logger.LogInformation("[AmuseHost] [PipelineServer] [RunPipeline] {Message}", ex.Message);
                await SendException(ex, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[AmuseHost] [PipelineServer] [RunPipeline] An exception occurred running pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        /// <summary>
        /// Updates the progress.
        /// </summary>
        /// <param name="progress">The progress.</param>
        private async Task UpdateProgress(IRunProgress progress)
        {
            if (progress is TensorStack.StableDiffusionCpp.Common.PipelineProgress pipelineProgress)
            {
                await QueueProgress(new PipelineProgress
                {
                    BatchMaximum = pipelineProgress.BatchMaximum,
                    BatchValue = pipelineProgress.BatchValue,
                    Elapsed = pipelineProgress.Elapsed,
                    ElapsedKey = pipelineProgress.ElapsedKey,
                    Key = pipelineProgress.Key,
                    Maximum = pipelineProgress.Maximum,
                    Message = pipelineProgress.Message,
                    Subkey = pipelineProgress.Subkey,
                    Timestamp = pipelineProgress.Timestamp,
                    Value = pipelineProgress.Value,
                    Tensors = pipelineProgress.Tensors
                });
            }
        }


        /// <summary>
        /// Sends loading the progress.
        /// </summary>
        private async Task SendLoadingProgress(string message = null)
        {
            await QueueProgress(new PipelineProgress { Key = "Load", Subkey = "Pipeline", Message = message ?? "Loading Pipeline Components..." });
        }
    }
}