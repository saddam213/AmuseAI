using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common.Tensor;
using TensorStack.Python.Common;
using TensorStack.Python.Config;

namespace Amuse.Common
{
    public sealed class PipelineClient : BaseClient
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PipelineClient"/> class.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <param name="logger">The logger.</param>
        public PipelineClient(ClientConfig config, IProgress<PipelineProgress> progressCallback, ILogger logger = default)
            : base(config, ChannelConfig.PipelineConfig, progressCallback, logger) { }


        /// <summary>
        /// Load the pipeline
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="mode">The mode.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task LoadAsync(PipelineConfig pipeline, EnvironmentMode mode = EnvironmentMode.Create, CancellationToken cancellationToken = default)
        {
            try
            {
                IsCanceled = false;
                await StartAsync(mode, cancellationToken);
                await SendPipelineRequestAsync(new PipelineRequest(pipeline, RequestType.PipelineLoad), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await KillServerAsync();
                throw;
            }
        }


        /// <summary>
        /// Reload the pipeline
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task ReloadAsync(PipelineReloadOptions options, CancellationToken cancellationToken = default)
        {
            try
            {
                IsCanceled = false;
                await SendPipelineRequestAsync(new PipelineRequest(options), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await KillServerAsync();
                throw;
            }
        }


        /// <summary>
        /// Unload the pipeline
        /// </summary>
        public async Task UnloadAsync()
        {
            IsCanceled = true;
            await SendPipelineRequestAsync(new PipelineRequest(RequestType.PipelineUnload));
            await StopAsync();
        }


        /// <summary>
        /// Run the pipeline
        /// </summary>
        /// <param name="options">The options.</param>
        public async Task<Tensor<float>> RunAsync(PipelineOptions options)
        {
            IsCanceled = false;
            var response = await SendPipelineRequestAsync(new PipelineRequest(options));
            return response.Tensors.FirstOrDefault();
        }


        /// <summary>
        /// Cancel pipeline Load/Run
        /// </summary>
        public async Task CancelAsync()
        {
            IsCanceled = true;
            await SendObjectRequestAsync(new CommandRequest());
        }

    }
}
