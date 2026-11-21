using Amuse.App.Common;
using Amuse.Common;
using Amuse.Common.Config;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
        private static readonly BackendType[] SetupBackends = [BackendType.StableDiffusionCpp, BackendType.AudioCpp, BackendType.LlamaCpp];
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
            await CreateInternalAsync(environment, BackendStatus.Create, progressCallback, cancellationToken);
        }


        public async Task CreateAsync(EnvironmentModel environment, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            await CreateInternalAsync(environment, BackendStatus.Create, progressCallback, cancellationToken);
        }


        public async Task UpdateAsync(EnvironmentModel environment, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            await CreateInternalAsync(environment, BackendStatus.Update, progressCallback, cancellationToken);
        }


        public async Task RebuildAsync(EnvironmentModel environment, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            await CreateInternalAsync(environment, BackendStatus.Rebuild, progressCallback, cancellationToken);
        }


        public Task DeleteAsync(EnvironmentModel environment)
        {
            FileHelper.DeleteDirectory(GetPath(environment));
            return Task.CompletedTask;
        }


        /// <summary>
        /// Gets the environments that can stand in for an environment, the same backend, vendor and type, with the same device or pipeline, in settings order.
        /// </summary>
        /// <param name="environment">The environment.</param>
        public IReadOnlyList<EnvironmentModel> GetEnvironmentGroup(EnvironmentModel environment)
        {
            return _settings.Environments
                .Where(x => x.Backend == environment.Backend && x.Vendor == environment.Vendor && x.Type == environment.Type)
                .Where(x => environment.Type switch
                {
                    EnvironmentType.Device => x.Device == environment.Device,
                    EnvironmentType.Pipeline => x.Pipeline == environment.Pipeline,
                    _ => true
                })
                .ToList();
        }


        /// <summary>
        /// Sets the environment as the default for its backend and vendor, device or pipeline, so inference uses it, and saves the settings.
        /// </summary>
        /// <param name="environment">The environment.</param>
        public async Task SetDefaultAsync(EnvironmentModel environment)
        {
            foreach (var other in _settings.Environments.Where(environment.IsSameDefaultGroup))
                other.IsDefault = false;

            environment.IsDefault = true;
            await _settings.SaveAsync();
        }


        public BackendStatus GetStatus(PipelineModel pipeline)
        {
            var environment = GetEnvironment(pipeline);
            return GetStatus(environment);
        }


        public BackendStatus GetStatus(EnvironmentModel environment)
        {
            return environment.Status;
        }


        public EnvironmentModel GetEnvironment(PipelineModel pipeline)
        {
            return GetEnvironment(pipeline.Device, pipeline.Backend, pipeline.PipelineType);
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


        public PipelineCreateOptions CreatePipelineOptions(EnvironmentModel environment, BackendStatus status = BackendStatus.Create)
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
                    Status = status
                };

                environmentConfig.Variables.Add("HF_HUB_OFFLINE", "1");
                environmentConfig.Variables.Add("HF_HUB_CACHE", _settings.DirectoryDiffusion);
                environmentConfig.Variables.Add("PIP_CACHE_DIR", Path.Combine(backendPath, "Pipelines", ".pip-cache"));
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
                    Status = status
                };
                return environmentConfig;
            }
            else if (environment.Backend == BackendType.AudioCpp || environment.Backend == BackendType.LlamaCpp)
            {
                var environmentConfig = new PipelineCreateOptions
                {
                    IsDebug = _settings.IsServerDebugEnabled,
                    Directory = backendPath,
                    Environment = environment.Environment,
                    HostVersion = environment.HostVersion,
                    Requirements = [.. environment.Requirements],
                    Variables = environment.Variables?.ToDictionary() ?? [],
                    Status = status
                };
                return environmentConfig;
            }
            throw new NotImplementedException();
        }


        /// <summary>
        /// Creates, updates or rebuilds the environment, deleting its folder if cancelled or failed so a partial install is not seen as installed.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="status">The status.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task CreateInternalAsync(EnvironmentModel environment, BackendStatus status, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            try
            {
                await CreateEnvironmentAsync(environment, status, progressCallback, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[EnvironmentService] [Create] Environment {Environment} cancelled, deleting folder", environment.Environment);
                await DeletePartialAsync(environment);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EnvironmentService] [Create] Environment {Environment} failed, deleting folder", environment.Environment);
                await DeletePartialAsync(environment);
                throw;
            }
        }


        /// <summary>
        /// Deletes the folder of a partly installed environment, retrying while the host's files are released.
        /// </summary>
        /// <param name="environment">The environment.</param>
        private async Task DeletePartialAsync(EnvironmentModel environment)
        {
            var path = GetPath(environment);
            if (!await FileHelper.DeleteDirectoryAsync(path, retries: 10))
                _logger.LogWarning("[EnvironmentService] [Delete] Failed to delete environment folder {Path}", path);
        }


        /// <summary>
        /// Creates, updates or rebuilds the environment in its backend host.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="status">The status.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task CreateEnvironmentAsync(EnvironmentModel environment, BackendStatus status, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            var createOptions = CreatePipelineOptions(environment, status);
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
            else if (environment.Backend == BackendType.AudioCpp)
            {
                var clientConfig = new ClientConfig
                {
                    IsDebugMode = createOptions.IsDebug,
                    ServerPath = App.DirectoryServer,
                    ServerType = ServerType.AudioCpp,
                    ServerVariables = createOptions.Variables
                };

                using (var pipelineClient = new PipelineClient(clientConfig, progressCallback, _logger))
                {
                    await pipelineClient.CreateAsync(createOptions, cancellationToken);
                    await SaveEnvironmentStatusAsync(environment);
                }
            }
            else if (environment.Backend == BackendType.LlamaCpp)
            {
                var clientConfig = new ClientConfig
                {
                    IsDebugMode = createOptions.IsDebug,
                    ServerPath = App.DirectoryServer,
                    ServerType = ServerType.LlamaCpp,
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
                ? Path.Combine(backendPath, "Pipelines", environment.Environment)
                : Path.Combine(backendPath, environment.Environment);
        }


        private static string GetBackendPath(EnvironmentModel environment)
        {
            return environment.Backend switch
            {
                BackendType.HuggingFace => Path.Combine(App.DirectoryData, "RuntimeHuggingFace"),
                BackendType.AudioCpp => Path.Combine(App.DirectoryData, "RuntimeAudioCpp"),
                BackendType.LlamaCpp => Path.Combine(App.DirectoryData, "RuntimeLlamaCpp"),
                _ => Path.Combine(App.DirectoryData, "RuntimeStableDiffusionCpp")
            };
        }


        /// <summary>
        /// Gets the installed environments that need an update or rebuild, then the backends on the system's devices.
        /// </summary>
        /// <param name="includeMissing">if set to <c>true</c> include backends with no installed environment.</param>
        /// <param name="includeInstalled">if set to <c>true</c> include backends that are already installed, to change or try other environments.</param>
        public IReadOnlyList<EnvironmentSetupItem> GetSetupEnvironments(bool includeMissing, bool includeInstalled = false)
        {
            var results = new List<EnvironmentSetupItem>();

            // Updates
            foreach (var environment in _settings.Environments.Where(x => x.Status == BackendStatus.Update || x.Status == BackendStatus.Rebuild))
            {
                if (!Exists(environment))
                    continue;

                results.Add(new EnvironmentSetupItem(environment.Backend, environment.Vendor, [environment], true, true, Exists));
            }

            if (!includeMissing && !includeInstalled)
                return results;

            // Missing
            var vendors = _settings.Vendors ?? [];
            var devices = (_settings.Devices ?? [])
                .Where(x => vendors.Contains(x.Vendor))
                .GroupBy(x => x.Vendor);
            foreach (var vendorDevices in devices)
            {
                foreach (var backend in SetupBackends)
                {
                    var supportedDevices = vendorDevices
                        .Where(x => x.SupportedBackends is not null && x.SupportedBackends.Contains(backend))
                        .ToList();
                    if (supportedDevices.Count == 0)
                        continue;

                    // Pipeline specific environments are installed on demand when the pipeline is used
                    var environments = _settings.Environments
                        .Where(x => x.Backend == backend && x.Vendor == vendorDevices.Key)
                        .Where(x => x.Type == EnvironmentType.Vendor || (x.Type == EnvironmentType.Device && supportedDevices.Any(d => d.HardwareID == x.Device)))
                        .ToList();
                    if (environments.Count == 0)
                        continue;

                    // Installed, only listed when managing environments, unselected as there is nothing to do
                    var isInstalled = environments.Any(Exists);
                    if (isInstalled && (!includeInstalled || results.Any(x => x.Backend == backend && x.Vendor == vendorDevices.Key)))
                        continue;
                    if (!isInstalled && !includeMissing)
                        continue;

                    results.Add(new EnvironmentSetupItem(backend, vendorDevices.Key, environments, false, !isInstalled, Exists));
                }
            }
            return results;
        }


        private async Task SaveEnvironmentStatusAsync(EnvironmentModel environment)
        {
            environment.Status = BackendStatus.Create;
            await _settings.SaveAsync();
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
        Task SetDefaultAsync(EnvironmentModel environment);
        IReadOnlyList<EnvironmentModel> GetEnvironmentGroup(EnvironmentModel environment);

        BackendStatus GetStatus(PipelineModel pipeline);
        BackendStatus GetStatus(EnvironmentModel environment);

        EnvironmentModel GetEnvironment(PipelineModel pipeline);
        EnvironmentModel GetEnvironment(Device device, BackendType backendType, PipelineType pipelineType);

        PipelineCreateOptions CreatePipelineOptions(EnvironmentModel environment, BackendStatus status = BackendStatus.Create);

        IReadOnlyList<EnvironmentSetupItem> GetSetupEnvironments(bool includeMissing, bool includeInstalled = false);
    }
}
