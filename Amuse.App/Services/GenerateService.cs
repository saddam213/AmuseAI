using Amuse.App.Runtime;
using Microsoft.Extensions.Logging;
using System;
using TensorStack.Common;

namespace Amuse.App.Services
{
    /// <summary>
    /// Runs the diffusion pipelines, image and video, audio pipelines run in the <see cref="AudioService"/> and language pipelines in the <see cref="LanguageService"/>.
    /// </summary>
    public sealed class GenerateService : GenerateServiceBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="environmentService">The environment service.</param>
        /// <param name="toolService">The tool service.</param>
        /// <param name="mediaService">The media service.</param>
        /// <param name="previewService">The preview service.</param>
        /// <param name="logger">The logger.</param>
        public GenerateService(Settings settings, IEnvironmentService environmentService, IToolService toolService, IMediaService mediaService, IPreviewService previewService, ILogger<GenerateService> logger)
            : base(settings, environmentService, toolService, mediaService, previewService, logger) { }


        /// <summary>
        /// Creates the backend client for the backend.
        /// </summary>
        /// <param name="backend">The backend.</param>
        protected override BackendClient CreateBackendClient(BackendType backend)
        {
            return backend switch
            {
                BackendType.StableDiffusionCpp => new StableDiffusionCppClient(Settings, ToolService, MediaService, EnvironmentService, Logger),
                _ => throw new NotImplementedException()
            };
        }
    }
}
