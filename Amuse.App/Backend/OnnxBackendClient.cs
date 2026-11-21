using Amuse.App.Services;
using Amuse.Common;
using Amuse.Common.Config;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Amuse.App.Runtime
{
    /// <summary>
    /// PipelineClient implemntation for Amuse.Host.Onnx
    /// Implements the <see cref="Amuse.App.Runtime.BackendClient" />
    /// </summary>
    /// <seealso cref="Amuse.App.Runtime.BackendClient" />
    public sealed class OnnxBackendClient : BackendClient
    {
        private readonly ServerType _serverType;

        /// <summary>
        /// Initializes a new instance of the <see cref="OnnxBackendClient"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="mediaService">The media service.</param>
        /// <param name="logger">The logger.</param>
        public OnnxBackendClient(Settings settings, IToolService toolService, IMediaService mediaService, ILogger logger)
            : this(settings, toolService, mediaService, logger, ServerType.OnnxRuntime) { }


        /// <summary>
        /// Initializes a new instance of the <see cref="OnnxBackendClient"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="toolService">The tool service.</param>
        /// <param name="mediaService">The media service.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="serverType">The server type, a separate host instance with its own mutex and channels.</param>
        public OnnxBackendClient(Settings settings, IToolService toolService, IMediaService mediaService, ILogger logger, ServerType serverType)
            : base(settings, toolService, mediaService, logger)
        {
            _serverType = serverType;
        }


        /// <summary>
        /// Create PipelineClient targeting Amuse.Host.Onnx.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <exception cref="System.OperationCanceledException"></exception>
        protected override async Task<PipelineClient> CreatePipelineClientAsync(CancellationToken cancellationToken = default)
        {
            var createOptions = new PipelineCreateOptions();
            var clientConfig = new ClientConfig
            {
                ServerPath = App.DirectoryServer,
                ServerType = _serverType,
                IsDebugMode = Settings.IsServerDebugEnabled,
            };
            return await CreatePipelineClientAsync(clientConfig, createOptions, cancellationToken);
        }

    }
}
