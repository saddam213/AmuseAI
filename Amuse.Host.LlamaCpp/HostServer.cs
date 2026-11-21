using Amuse.Common;
using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Pipeline;
using TensorStack.LlamaCpp;
using PipelineProgress = Amuse.Common.PipelineProgress;

namespace Amuse.Host.LlamaCpp
{
    public sealed class HostServer : PipelineServer
    {
        private readonly IProgress<IRunProgress> _progressCallback;
        private PipelineCreateOptions _options;
        private PipelineLoadOptions _pipelineOptions;
        private LlamaCppPipeline _pipeline;

        /// <summary>
        /// Initializes a new instance of the <see cref="HostServer"/> class.
        /// </summary>
        /// <param name="channelConfig">The channel configuration.</param>
        /// <param name="logger">The logger.</param>
        public HostServer(ServerConfig channelConfig, ILogger logger)
            : base(channelConfig, logger)
        {
            _progressCallback = new SyncProgress<IRunProgress>(UpdateProgress);
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
                var configuration = new BackendConfig
                {
                    Name = request.CreateOptions.Environment,
                    Requirements = request.CreateOptions.Requirements,
                    Directory = request.CreateOptions.Directory
                };

                var installManager = new LlamaCppBackend(configuration, _progressCallback, Logger);
                if (!await installManager.SetupAsync(request.CreateOptions.Status))
                    throw new Exception($"Failed to Install llama.cpp {request.CreateOptions.HostVersion}");

                _options = request.CreateOptions;
                Logger.LogInformation("[AmuseHost] [PipelineServer] [CreatePipeline] Environment created, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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

                var loadOptions = _options.CreateLoadOptions(_pipelineOptions);
                var backendDirectory = Path.Combine(_options.Directory, _options.Environment);
                _pipeline ??= new LlamaCppPipeline(backendDirectory, _progressCallback, Logger);
                await _pipeline.LoadAsync(loadOptions, cancellationToken);

                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                _pipeline?.Unload();
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
                _pipelineOptions.ProcessType = request.ReloadOptions.ProcessType;
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
                _pipeline?.Unload();
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
                await SendLoadingProgress("Generating...");
                using (PipelineCancellation = new CancellationTokenSource())
                {
                    ReadTensorRequest(request);

                    if (request.RunOptions.TextOptions == null)
                        throw new NotSupportedException("llama.cpp only supports text requests.");

                    // Text
                    var textOptions = request.RunOptions.TextOptions.CreateTextOptions();
                    var textResult = await _pipeline.GenerateTextAsync(textOptions, PipelineCancellation.Token);
                    await SendTensorResponse(cancellationToken, textResult);
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
        private void UpdateProgress(IRunProgress progress)
        {
            if (progress is TensorStack.Common.Pipeline.PipelineProgress pipelineProgress)
            {
                EnqueueProgress(pipelineProgress.ToProgress());
            }
        }


        /// <summary>
        /// Sends loading the progress.
        /// </summary>
        /// <param name="message">The message.</param>
        private async Task SendLoadingProgress(string message = null)
        {
            await QueueProgress(new PipelineProgress { Key = "Load", Subkey = "Pipeline", Message = message ?? "Loading Pipeline Components..." });
        }

    }
}
