using Amuse.App.Common;
using Amuse.Common;
using Amuse.Common.Config;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Common;

namespace Amuse.App.Services
{
    public sealed class EnvironmentService : IEnvironmentService
    {
        private readonly ILogger _logger;
        private readonly Settings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoService"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public EnvironmentService(Settings settings, ILogger<EnvironmentService> logger)
        {
            _logger = logger;
            _settings = settings;
        }


        public bool Exists(PipelineModel pipeline)
        {
            var environment = GetEnvironment(pipeline);
            return Exists(environment);
        }


        public bool Exists(EnvironmentModel environment)
        {
            return Directory.Exists(GetPath(environment));
        }


        public async Task CreateAsync(PipelineModel pipeline, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            var environment = GetEnvironment(pipeline);
            await CreateInternalAsync(environment, EnvironmentMode.Create, progressCallback, cancellationToken);
        }


        public async Task CreateAsync(EnvironmentModel environment, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            await CreateInternalAsync(environment, EnvironmentMode.Create, progressCallback, cancellationToken);
        }


        public async Task UpdateAsync(EnvironmentModel environment, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            await CreateInternalAsync(environment, EnvironmentMode.Update, progressCallback, cancellationToken);
        }


        public async Task RebuildAsync(EnvironmentModel environment, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            await CreateInternalAsync(environment, EnvironmentMode.Rebuild, progressCallback, cancellationToken);
        }


        public Task DeleteAsync(EnvironmentModel environment)
        {
            FileHelper.DeleteDirectory(GetPath(environment));
            return Task.CompletedTask;
        }


        public EnvironmentMode GetStatus(PipelineModel pipeline)
        {
            var environment = GetEnvironment(pipeline);
            return GetStatus(environment);
        }


        public EnvironmentMode GetStatus(EnvironmentModel environment)
        {
            return environment.Status;
        }


        public EnvironmentModel GetEnvironment(PipelineModel pipeline)
        {
            return GetEnvironment(pipeline.Device, pipeline.GenerateModel.Backend, pipeline.GenerateModel.Pipeline);
        }


        public EnvironmentModel GetEnvironment(Device device, BackendType backendType, PipelineType pipelineType)
        {
            var pipelineEnvironment = _settings.Environments
                .Where(x => x.Backend == backendType && x.Vendor == device.Vendor && x.Type == EnvironmentType.Pipeline && x.Pipeline == pipelineType)
                .OrderByDescending(x => x.IsDefault)
                .FirstOrDefault();
            if (pipelineEnvironment != null)
                return pipelineEnvironment;

            var deviceEnvironment = _settings.Environments
                .Where(x => x.Backend == backendType && x.Vendor == device.Vendor && x.Type == EnvironmentType.Device && x.Device == device.HardwareID)
                .OrderByDescending(x => x.IsDefault)
                .FirstOrDefault();
            if (deviceEnvironment != null)
                return deviceEnvironment;

            var vendorEnvironment = _settings.Environments
                .Where(x => x.Backend == backendType && x.Vendor == device.Vendor && x.Type == EnvironmentType.Vendor)
                .OrderByDescending(x => x.IsDefault)
                .FirstOrDefault();
            if (vendorEnvironment != null)
                return vendorEnvironment;

            return _settings.Environments.FirstOrDefault(x => x.Backend == backendType);
        }


        public PipelineCreateOptions CreatePipelineOptions(EnvironmentModel environment, EnvironmentMode environmentMode = EnvironmentMode.Create)
        {
            var backendPath = GetBackendPath(environment);
            if (environment.Backend == BackendType.HuggingFace)
            {
                var environmentConfig = new PipelineCreateOptions
                {
                    IsDebug = _settings.IsServerDebugEnabled,
                    Directory = backendPath,
                    Environment = environment.Environment,
                    HostVersion = environment.HostVersion,
                    Requirements = [.. environment.Requirements],
                    Variables = environment.Variables?.ToDictionary() ?? [],
                    Mode = environmentMode
                };

                environmentConfig.Variables.Add("HF_HUB_OFFLINE", "1");
                environmentConfig.Variables.Add("HF_HUB_CACHE", _settings.DirectoryDiffusion);
                return environmentConfig;
            }
            else if (environment.Backend == BackendType.StableDiffusionCpp)
            {
                var environmentConfig = new PipelineCreateOptions
                {
                    IsDebug = _settings.IsServerDebugEnabled,
                    ServerPort = 2345,
                    ServerAddress = "127.0.0.1",
                    Directory = backendPath,
                    Environment = environment.Environment,
                    HostVersion = environment.HostVersion,
                    Requirements = [.. environment.Requirements],
                    Variables = environment.Variables?.ToDictionary() ?? [],
                    Mode = environmentMode
                };
                return environmentConfig;
            }
            throw new NotImplementedException();
        }


        private async Task CreateInternalAsync(EnvironmentModel environment, EnvironmentMode mode, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            var createOptions = CreatePipelineOptions(environment, mode);
            if (environment.Backend == BackendType.HuggingFace)
            {
                var clientConfig = new ClientConfig
                {
                    IsDebugMode = createOptions.IsDebug,
                    ServerPath = App.DirectoryServer,
                    ServerType = ServerType.HuggingFace,
                    ServerVariables = createOptions.Variables
                };

                using (var pipelineClient = new PipelineClient(clientConfig, progressCallback, _logger))
                {
                    await pipelineClient.CreateAsync(createOptions, cancellationToken);
                    await SaveEnvironmentStatusAsync(environment);
                }
            }
            else if (environment.Backend == BackendType.StableDiffusionCpp)
            {
                var clientConfig = new ClientConfig
                {
                    IsDebugMode = createOptions.IsDebug,
                    ServerPath = App.DirectoryServer,
                    ServerType = ServerType.StableDiffusionCpp,
                    ServerVariables = createOptions.Variables
                };

                using (var pipelineClient = new PipelineClient(clientConfig, progressCallback, _logger))
                {
                    await pipelineClient.CreateAsync(createOptions, cancellationToken);
                    await SaveEnvironmentStatusAsync(environment);
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }


        private static string GetPath(EnvironmentModel environment)
        {
            var backendPath = GetBackendPath(environment);
            return environment.Backend == BackendType.HuggingFace
                ? Path.Combine(backendPath, "Pipelines", $".{environment.Environment}")
                : Path.Combine(backendPath, environment.Environment);
        }


        private static string GetBackendPath(EnvironmentModel environment)
        {
            return environment.Backend == BackendType.HuggingFace
                ? Path.Combine(App.DirectoryData, "RuntimeHuggingFace")
                : Path.Combine(App.DirectoryData, "RuntimeStableDiffusionCpp");
        }


        private async Task SaveEnvironmentStatusAsync(EnvironmentModel environment)
        {
            environment.Status = EnvironmentMode.Create;
            await SettingsManager.SaveAsync(_settings);
        }
    }


    public interface IEnvironmentService
    {
        bool Exists(PipelineModel pipeline);
        bool Exists(EnvironmentModel environment);

        Task CreateAsync(PipelineModel pipeline, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default);
        Task CreateAsync(EnvironmentModel environment, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default);

        Task UpdateAsync(EnvironmentModel environment, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default);
        Task RebuildAsync(EnvironmentModel environment, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default);
        Task DeleteAsync(EnvironmentModel environment);

        EnvironmentMode GetStatus(PipelineModel pipeline);
        EnvironmentMode GetStatus(EnvironmentModel environment);

        EnvironmentModel GetEnvironment(PipelineModel pipeline);
        EnvironmentModel GetEnvironment(Device device, BackendType backendType, PipelineType pipelineType);

        PipelineCreateOptions CreatePipelineOptions(EnvironmentModel environment, EnvironmentMode environmentMode = EnvironmentMode.Create);
    }
}
