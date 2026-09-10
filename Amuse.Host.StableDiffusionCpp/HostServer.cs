using Amuse.Common;
using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.StableDiffusionCpp;

namespace Amuse.Host.StableDiffusionCpp
{
    public sealed class HostServer : PipelineServer
    {
        private readonly IProgress<TensorStack.StableDiffusionCpp.Common.PipelineProgress> _pipelineRelayCallback;
        private readonly IProgress<PipelineProgress> _progressRelayCallback;
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
            _progressRelayCallback = new Progress<PipelineProgress>(async (p) => await QueueProgress(p));
            _pipelineRelayCallback = new Progress<TensorStack.StableDiffusionCpp.Common.PipelineProgress>(async (p) => await UpdateProgress(p));
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
                var isReinstall = request.CreateOptions.Mode == EnvironmentMode.Update || request.CreateOptions.Mode == EnvironmentMode.Rebuild || request.CreateOptions.Mode == EnvironmentMode.Reinstall;
                var backendConfig = new TensorStack.StableDiffusionCpp.Common.BackendConfig
                {
                    Name = request.CreateOptions.Environment,
                    Requirements = request.CreateOptions.Requirements,
                    Directory = Path.Combine(request.CreateOptions.Directory, request.CreateOptions.Environment),
                };

                if (!await InstallManager.InitializeAsync(backendConfig, isReinstall, async (l, m) => await SendProgressMessage(l, m)))
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
                _pipeline = new StableDiffusionPipeline(backendDirectory, _pipelineRelayCallback, OnLogCallback);
                await _pipeline.LoadContextAsync(contextOptions, cancellationToken);

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
                _pipeline.UnloadContext();
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
        private async Task UpdateProgress(TensorStack.StableDiffusionCpp.Common.PipelineProgress progress)
        {
            await QueueProgress(new PipelineProgress
            {
                BatchMaximum = progress.BatchMaximum,
                BatchValue = progress.BatchValue,
                Elapsed = progress.Elapsed,
                ElapsedKey = progress.ElapsedKey,
                Key = progress.Key,
                Maximum = progress.Maximum,
                Message = progress.Message,
                Subkey = progress.Subkey,
                Timestamp = progress.Timestamp,
                Value = progress.Value,
                Tensors = progress.Tensors
            });
        }


        /// <summary>
        /// Called when StableDiffusion.cpp log emitted.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <param name="message">The message.</param>
        private void OnLogCallback(LogLevelType level, string message)
        {
            var logLevel = level switch
            {
                LogLevelType.Info => LogLevel.Information,
                LogLevelType.Debug => LogLevel.Debug,
                LogLevelType.Warn => LogLevel.Warning,
                LogLevelType.Error => LogLevel.Error,
                LogLevelType.Verbose => LogLevel.Trace,
                _ => LogLevel.Trace
            };
            Logger?.Log(logLevel, "[StableDiffusion.cpp] {message}", message);
        }


        /// <summary>
        /// Sends loading the progress.
        /// </summary>
        private async Task SendLoadingProgress(string message = null)
        {
            await QueueProgress(new PipelineProgress { Key = "Load", Subkey = "Pipeline", Message = message ?? "Loading Pipeline Components..." });
        }


        /// <summary>
        /// Sends the progress message.
        /// </summary>
        /// <param name="message">The message.</param>
        private async Task SendProgressMessage(LogLevelType level, string message)
        {
            OnLogCallback(level, message);
            await QueueProgress(new PipelineProgress { Key = "Initialize", Message = message });
        }
    }
}