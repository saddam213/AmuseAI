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
using TensorStack.Python.Common;
using TensorStack.Python.Config;

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


        public async Task<PipelineClient> CreateClientAsync(PipelineModel pipeline, PipelineConfig pipelineConfig, EnvironmentMode mode, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            var environment = await GetAsync(pipeline);
            var pipelineClientConfig = new ClientConfig
            {
                Environment = environment,
                ServerPath = App.DirectoryServer,
                IsDebugMode = environment.IsDebug,
            };

            var diffusionPipeline = new PipelineClient(pipelineClientConfig, progressCallback, _logger);

            try
            {
                await diffusionPipeline.LoadAsync(pipelineConfig, mode, cancellationToken);
                return diffusionPipeline;
            }
            catch (Exception)
            {
                diffusionPipeline?.Dispose();
                throw;
            }
        }


        public Task<DownloadClient> CreateDownloadClientAsync(IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            var environmentModel = _settings.Environments
                      .Where(x => x.IsDefault && Exists(x))
                      .OrderByDescending(x => x.IsDefault)
                      .FirstOrDefault()
                ?? throw new Exception("No Environment Found");

            var environment = FromModel(environmentModel, _settings.IsServerDebugEnabled);
            var pipelineClientConfig = new ClientConfig
            {
                Environment = environment,
                ServerPath = App.DirectoryServer,
                IsDebugMode = environment.IsDebug,
            };

            return Task.FromResult(new DownloadClient(pipelineClientConfig, progressCallback, _logger));
        }


        public Task<EnvironmentConfig> GetAsync(PipelineModel pipeline)
        {
            var environment = GetEnvironment(pipeline);
            return Task.FromResult(FromModel(environment, _settings.IsServerDebugEnabled));
        }


        public Task<EnvironmentConfig> GetAsync(EnvironmentModel environment)
        {
            return Task.FromResult(FromModel(environment, _settings.IsServerDebugEnabled));
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


        public bool Exists(PipelineModel pipeline)
        {
            var environment = GetEnvironment(pipeline);
            return Exists(environment);
        }


        public bool Exists(EnvironmentModel environment)
        {
            return Directory.Exists(GetPath(environment));
        }


        public bool IsInstalled()
        {
            var environment = _settings.Environments
                .Where(x => Exists(x))
                .OrderByDescending(x => x.IsDefault)
                .FirstOrDefault();
            if (environment == null)
                return false;

            return true;
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
            return GetEnvironment(pipeline.Device, pipeline.DiffusionModel);
        }


        public EnvironmentModel GetEnvironment(Device device, DiffusionModel diffusionModel)
        {
            var pipelineEnvironment = _settings.Environments
                .Where(x => x.Vendor == device.Vendor && x.Type == EnvironmentType.Pipeline && x.Pipeline == diffusionModel.Pipeline)
                .OrderByDescending(x => x.IsDefault)
                .FirstOrDefault();
            if (pipelineEnvironment != null)
                return pipelineEnvironment;

            var deviceEnvironment = _settings.Environments
                .Where(x => x.Vendor == device.Vendor && x.Type == EnvironmentType.Device && x.Device == device.HardwareID)
                .OrderByDescending(x => x.IsDefault)
                .FirstOrDefault();
            if (deviceEnvironment != null)
                return deviceEnvironment;

            var vendorEnvironment = _settings.Environments
                .Where(x => x.Vendor == device.Vendor && x.Type == EnvironmentType.Vendor)
                .OrderByDescending(x => x.IsDefault)
                .FirstOrDefault();
            if (vendorEnvironment != null)
                return vendorEnvironment;

            return _settings.Environments.First();
        }


        private async Task CreateInternalAsync(EnvironmentModel environment, EnvironmentMode mode, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            using var pipelineClient = new PipelineClient(new ClientConfig
            {
                IsDebugMode = _settings.IsServerDebugEnabled,
                Environment = FromModel(environment, _settings.IsServerDebugEnabled),
                ServerPath = App.DirectoryServer,
            }, progressCallback, _logger);
            await pipelineClient.StartAsync(mode, cancellationToken);
            await SaveEnvironmentStatusAsync(environment);
        }


        private static string GetPath(EnvironmentModel environment)
        {
            return Path.Combine(App.DirectoryPython, "Pipelines", $".{environment.Environment}");
        }


        private async Task SaveEnvironmentStatusAsync(EnvironmentModel environment)
        {
            environment.Status = EnvironmentMode.Create;
            await SettingsManager.SaveAsync(_settings);
        }


        private static EnvironmentConfig FromModel(EnvironmentModel environment, bool isDebugEnabled)
        {
            return new EnvironmentConfig
            {
                IsDebug = isDebugEnabled,
                Directory = App.DirectoryPython,
                Variables = environment.Variables,
                Environment = environment.Environment,
                Requirements = environment.Requirements
            };
        }
    }


    public interface IEnvironmentService
    {
        Task<EnvironmentConfig> GetAsync(PipelineModel pipeline);
        Task<EnvironmentConfig> GetAsync(EnvironmentModel environment);
        Task<PipelineClient> CreateClientAsync(PipelineModel pipeline, PipelineConfig pipelineConfig, EnvironmentMode mode, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default);
        Task<DownloadClient> CreateDownloadClientAsync(IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default);

        bool IsInstalled();
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
        EnvironmentModel GetEnvironment(Device device, DiffusionModel diffusionModel);
    }
}
