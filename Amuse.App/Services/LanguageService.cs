using Amuse.App.Runtime;
using Microsoft.Extensions.Logging;
using System;
using TensorStack.Common;

namespace Amuse.App.Services
{
    /// <summary>
    /// Runs the language pipelines in their own host, so a language model stays loaded next to the diffusion model in the <see cref="GenerateService"/>.
    /// </summary>
    public sealed class LanguageService : GenerateServiceBase, ILanguageService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="environmentService">The environment service.</param>
        /// <param name="toolService">The tool service.</param>
        /// <param name="mediaService">The media service.</param>
        /// <param name="logger">The logger.</param>
        public LanguageService(Settings settings, IEnvironmentService environmentService, IToolService toolService, IMediaService mediaService, ILogger<LanguageService> logger)
            : base(settings, environmentService, toolService, mediaService, null, logger) { }


        /// <summary>
        /// Creates the backend client for the backend.
        /// </summary>
        /// <param name="backend">The backend.</param>
        protected override BackendClient CreateBackendClient(BackendType backend)
        {
            return backend switch
            {
                BackendType.LlamaCpp => new LlamaCppClient(Settings, ToolService, MediaService, EnvironmentService, Logger),
                _ => throw new NotImplementedException()
            };
        }
    }


    public interface ILanguageService : IGenerateService
    {
    }
}
