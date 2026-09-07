using Amuse.Common;
using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common.Tensor;
using TensorStack.HuggingFace;

namespace Amuse.Host.HuggingFace
{
    public sealed class HostServer : PipelineServer
    {
        private readonly IProgress<TensorStack.HuggingFace.Common.PipelineProgress> _progressCallback;
        private HuggingFacePipeline _pipeline;

        public HostServer(ServerConfig config, ILogger logger)
            : base(config, logger)
        {
            _progressCallback = new Progress<TensorStack.HuggingFace.Common.PipelineProgress>(async (p) => await UpdateProgress(p));
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
            _pipeline?.Dispose();
            return Task.CompletedTask;
        }

        protected override async Task CreatePipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var timestamp = Stopwatch.GetTimestamp();
                var environmentRequest = request.CreateOptions;
                var pythonOptions = request.CreateOptions.ToPythonOptions();
                var environmentMode = environmentRequest.Mode.Cast<Amuse.Common.EnvironmentMode, TensorStack.HuggingFace.Common.EnvironmentMode>();
                var pythonEnvironment = new InstallManager(pythonOptions, Config.DirectoryBase, Logger);
                if (environmentRequest.Mode == Amuse.Common.EnvironmentMode.Load || (environmentRequest.Mode == Amuse.Common.EnvironmentMode.Create && pythonEnvironment.Exists()))
                {
                    Logger.LogInformation("[PipelineServer] [CreatePipeline] Loading existing environment...");
                    await pythonEnvironment.LoadAsync(_progressCallback);
                }
                else
                {
                    Logger.LogInformation($"[PipelineServer] [CreatePipeline] Creating environment, Mode: {environmentRequest.Mode}...");
                    await pythonEnvironment.CreateAsync(environmentMode, _progressCallback);
                }

                Logger.LogInformation($"[PipelineServer] [CreatePipeline] Environment created, Elapsed: {Stopwatch.GetElapsedTime(timestamp)}");
                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PipelineServer] [CreatePipeline] An exception occurred creating environment.");
                await SendException(ex, cancellationToken);
            }
        }

        protected override async Task LoadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var pythonOptions = request.LoadOptions.ToPythonOptions();
                _pipeline = new HuggingFacePipeline(pythonOptions, _progressCallback, Logger);
                await _pipeline.LoadAsync();
                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PipelineServer] [LoadPipeline] An exception occurred loading pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        protected override async Task ReloadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var pythonOptions = request.ReloadOptions.ToPythonOptions();
                await _pipeline.ReloadAsync(pythonOptions);
                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PipelineServer] [ReloadPipeline] An exception occurred reloading pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        protected override async Task UnloadPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await _pipeline.UnloadAsync();
                await SendResponse(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PipelineServer] [UnloadPipeline] An exception occurred unloading pipeline.");
                await SendException(ex, cancellationToken);
            }
        }


        protected override async Task RunPipelineAsync(PipelineRequest request, CancellationToken cancellationToken)
        {
            try
            {
                PipelineCancellation = new CancellationTokenSource();

                ReadTensorRequest(request);
                if (request.RunOptions.ImageOptions != null)
                {
                    var pythonOptions = request.RunOptions.ImageOptions.ToPythonOptions();
                    var response = await _pipeline.GenerateImageAsync(pythonOptions, PipelineCancellation.Token);
                    await SendTensorResponse(cancellationToken, response);
                }
                else if (request.RunOptions.VideoOptions != null)
                {
                    var pythonOptions = request.RunOptions.VideoOptions.ToPythonOptions();
                    var response = await _pipeline.GenerateVideoAsync(pythonOptions, PipelineCancellation.Token);
                    await SendTensorResponse(cancellationToken, default(VideoSequence));
                }
                else if (request.RunOptions.AudioOptions != null)
                {
                    var pythonOptions = request.RunOptions.AudioOptions.ToPythonOptions();
                    var response = await _pipeline.GenerateAudioAsync(pythonOptions, PipelineCancellation.Token);
                    await SendTensorResponse(cancellationToken, response);
                }
                else if (request.RunOptions.TextOptions != null)
                {
                    var pythonOptions = request.RunOptions.TextOptions.ToPythonOptions();
                    var response = await _pipeline.GenerateTextAsync(pythonOptions, PipelineCancellation.Token);
                    await SendTensorResponse(cancellationToken, response);
                }
            }
            catch (OperationCanceledException ex)
            {
                Logger.LogError("[PipelineServer] [RunPipeline] {Message}", ex.Message);
                await SendException(ex, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PipelineServer] [RunPipeline] An exception occurred running pipeline.");
                await SendException(ex, cancellationToken);
            }
            finally
            {
                PipelineCancellation.Dispose();
                PipelineCancellation = null;
            }
        }


        private async Task UpdateProgress(TensorStack.HuggingFace.Common.PipelineProgress progress)
        {
            await QueueProgress(progress.ToProgress());
        }

    }
}
