using Amuse.App.Runtime;
using Microsoft.Extensions.Logging;
using System;
using TensorStack.Common;

namespace Amuse.App.Services
{
    /// <summary>
    /// Runs the Audio.cpp pipelines in their own host, so an audio model stays loaded next to the diffusion model in the <see cref="GenerateService"/>.
    /// </summary>
    public sealed class AudioService : GenerateServiceBase, IAudioService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AudioService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="environmentService">The environment service.</param>
        /// <param name="toolService">The tool service.</param>
        /// <param name="mediaService">The media service.</param>
        /// <param name="logger">The logger.</param>
        public AudioService(Settings settings, IEnvironmentService environmentService, IToolService toolService, IMediaService mediaService, ILogger<AudioService> logger)
            : base(settings, environmentService, toolService, mediaService, null, logger) { }


        /// <summary>
        /// Creates the backend client for the backend.
        /// </summary>
        /// <param name="backend">The backend.</param>
        protected override BackendClient CreateBackendClient(BackendType backend)
        {
            return backend switch
            {
                BackendType.AudioCpp => new AudioCppClient(Settings, ToolService, MediaService, EnvironmentService, Logger),
                _ => throw new NotImplementedException()
            };
        }
    }


    public interface IAudioService : IGenerateService
    {
    }
}
