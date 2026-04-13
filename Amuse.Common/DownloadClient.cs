using Amuse.Common.Config;
using Amuse.Common.Message;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Python.Common;
using TensorStack.Python.Config;

namespace Amuse.Common
{
    public sealed class DownloadClient : BaseClient
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadClient"/> class.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <param name="logger">The logger.</param>
        public DownloadClient(ClientConfig config, IProgress<PipelineProgress> progressCallback, ILogger logger = default)
            : base(config, ChannelConfig.DownloadConfig, progressCallback, logger) { }


        /// <summary>
        /// Download the pipeline.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task DownloadAsync(PipelineConfig pipeline, CancellationToken cancellationToken = default)
        {
            try
            {
                IsCanceled = false;
                await StartAsync(EnvironmentMode.Load, cancellationToken);
                await SendPipelineRequestAsync(new PipelineRequest(pipeline, RequestType.PipelineDownload), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await KillServerAsync();
                throw;
            }
        }
    }
}
