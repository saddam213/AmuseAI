using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Services;
using Amuse.Common;
using Amuse.Common.Config;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.WPF.Services;

namespace Amuse.App.Runtime
{
    /// <summary>
    /// PipelineClient implemntation for Amuse.Host.StableDiffusionCpp
    /// Implements the <see cref="Amuse.App.Runtime.BackendClient" />
    /// </summary>
    /// <seealso cref="Amuse.App.Runtime.BackendClient" />
    public sealed class StableDiffusionCppClient : BackendClient
    {
        private readonly IEnvironmentService _environmentService;

        /// <summary>
        /// Initializes a new instance of the <see cref="StableDiffusionCppClient"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="mediaService">The media service.</param>
        /// <param name="logger">The logger.</param>
        public StableDiffusionCppClient(Settings settings, IToolService toolService, IMediaService mediaService, IEnvironmentService environmentService, ILogger logger)
            : base(settings, toolService, mediaService, logger)
        {
            _environmentService = environmentService;
            StopHostOnException = true;
            ResolveComponentFiles = true;
        }


        /// <summary>
        /// Create PipelineClient targeting Amuse.Host.StableDiffusionCpp.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <exception cref="System.OperationCanceledException"></exception>
        protected override async Task<PipelineClient> CreatePipelineClientAsync(CancellationToken cancellationToken = default)
        {
            var environment = await InitializeEnvironmentAsync() ?? throw new OperationCanceledException();
            var createOptions = _environmentService.CreatePipelineOptions(environment);
            var clientConfig = new ClientConfig
            {
                ServerPath = App.DirectoryServer,
                ServerType = ServerType.StableDiffusionCpp,
                IsDebugMode = Settings.IsServerDebugEnabled,
                ServerVariables = createOptions.Variables
            };
            return await CreatePipelineClientAsync(clientConfig, createOptions, cancellationToken);
        }


        /// <summary>
        /// Initialize StableDiffusion.cpp environment, Load, Update Create.
        /// </summary>
        private async Task<EnvironmentModel> InitializeEnvironmentAsync()
        {
            var environment = _environmentService.GetEnvironment(Pipeline.Device, Pipeline.GenerateModel.Backend, Pipeline.GenerateModel.Pipeline);
            if ((environment.Status == EnvironmentMode.Create || environment.Status == EnvironmentMode.Load) && _environmentService.Exists(environment))
                return environment;

            var environmentDialog = DialogService.GetDialog<EnvironmentDialog>();
            if (environment.Status == EnvironmentMode.Update)
            {
                if (await environmentDialog.UpdateAsync(environment))
                    return environment;
            }
            else if (environment.Status == EnvironmentMode.Rebuild)
            {
                if (await environmentDialog.RebuildAsync(environment))
                    return environment;
            }
            else
            {
                if (await environmentDialog.CreateAsync(environment))
                    return environment;
            }
            return null;
        }

    }
}
