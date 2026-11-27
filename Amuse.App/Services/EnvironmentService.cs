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
    /// <summary>
    /// Installs, updates and deletes the backend environments, and selects the environment a pipeline runs from.
    /// </summary>
    public sealed class EnvironmentService : IEnvironmentService
    {
        private static readonly BackendType[] SetupBackends = [BackendType.StableDiffusionCpp, BackendType.AudioCpp, BackendType.LlamaCpp];
        private readonly ILogger _logger;
        private readonly Settings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="logger">The logger.</param>
        public EnvironmentService(Settings settings, ILogger<EnvironmentService> logger)
        {
            _logger = logger;
            _settings = settings;
        }


        /// <summary>
        /// Determines whether the environment is installed, its folder exists.
        /// </summary>
        /// <param name="environment">The environment.</param>
        public bool Exists(EnvironmentModel environment)
        {
            return Directory.Exists(GetPath(environment));
        }


        /// <summary>
        /// Installs the environment, deleting its folder if cancelled or failed.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task CreateAsync(EnvironmentModel environment, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            await SetupAsync(environment, BackendStatus.Create, progressCallback, cancellationToken);
        }


        /// <summary>
        /// Updates the environment, deleting its folder if cancelled or failed.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task UpdateAsync(EnvironmentModel environment, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            await SetupAsync(environment, BackendStatus.Update, progressCallback, cancellationToken);
        }


        /// <summary>
        /// Deletes the folder of the environment, retrying while the host's files are released.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <returns><c>true</c> if deleted or not found, <c>false</c> when files are still locked.</returns>
        public async Task<bool> DeleteAsync(EnvironmentModel environment)
        {
            var path = GetPath(environment);
            var isDeleted = await FileHelper.DeleteDirectoryAsync(path, retries: 10);
            if (!isDeleted)
                _logger.LogWarning("[EnvironmentService] [Delete] Failed to delete environment folder {Path}", path);

            return isDeleted;
        }


        /// <summary>
        /// Gets the environments that can stand in for an environment, the same backend, vendor and type, with the same device or pipeline, in settings order.
        /// </summary>
        /// <param name="environment">The environment.</param>
        public IReadOnlyList<EnvironmentModel> GetEnvironmentGroup(EnvironmentModel environment)
        {
            return _settings.Environments
                .Where(environment.IsSameDefaultGroup)
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


        /// <summary>
        /// Gets the environment a pipeline runs from, the most specific match first: its pipeline type, its device, then its vendor, the default of each first.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="backendType">The backend.</param>
        /// <param name="pipelineType">The pipeline type.</param>
        public EnvironmentModel GetEnvironment(Device device, BackendType backendType, PipelineType pipelineType)
        {
            var environments = _settings.Environments
                .Where(x => x.Backend == backendType && x.Vendor == device.Vendor)
                .OrderByDescending(x => x.IsDefault)
                .ToList();

            return environments.FirstOrDefault(x => x.Type == EnvironmentType.Pipeline && x.Pipeline == pipelineType)
                ?? environments.FirstOrDefault(x => x.Type == EnvironmentType.Device && x.Device == device.HardwareID)
                ?? environments.FirstOrDefault(x => x.Type == EnvironmentType.Vendor)
                ?? _settings.Environments.FirstOrDefault(x => x.Backend == backendType);
        }


        /// <summary>
        /// Creates the options the backend host needs to create or load the environment.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="status">The status, whether the host creates or updates the environment.</param>
        public PipelineCreateOptions CreatePipelineOptions(EnvironmentModel environment, BackendStatus status = BackendStatus.Create)
        {
            if (!SetupBackends.Contains(environment.Backend))
                throw new NotImplementedException();

            return new PipelineCreateOptions
            {
                IsDebug = _settings.IsServerDebugEnabled,
                Directory = GetBackendPath(environment),
                Environment = environment.Environment,
                HostVersion = environment.HostVersion,
                Requirements = [.. environment.Requirements],
                Variables = environment.Variables?.ToDictionary() ?? [],
                Status = status
            };
        }


        /// <summary>
        /// Creates or updates the environment, deleting its folder if cancelled or failed so a partial install is not seen as installed.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="status">The status.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task SetupAsync(EnvironmentModel environment, BackendStatus status, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken)
        {
            try
            {
                await SetupHostAsync(environment, status, progressCallback, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[EnvironmentService] [Setup] Environment {Environment} cancelled, deleting folder", environment.Environment);
                await DeleteAsync(environment);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EnvironmentService] [Setup] Environment {Environment} failed, deleting folder", environment.Environment);
                await DeleteAsync(environment);
                throw;
            }
        }


        /// <summary>
        /// Creates or updates the environment in its backend host, then marks it as created.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="status">The status.</param>
        /// <param name="progressCallback">The progress callback.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task SetupHostAsync(EnvironmentModel environment, BackendStatus status, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken)
        {
            var createOptions = CreatePipelineOptions(environment, status);
            var clientConfig = new ClientConfig
            {
                IsDebugMode = createOptions.IsDebug,
                ServerPath = App.DirectoryServer,
                ServerType = GetServerType(environment.Backend),
                ServerVariables = createOptions.Variables
            };

            using (var pipelineClient = new PipelineClient(clientConfig, progressCallback, _logger))
            {
                await pipelineClient.CreateAsync(createOptions, cancellationToken);
                await SaveEnvironmentStatusAsync(environment);
            }
        }


        /// <summary>
        /// Gets the host that sets up the environments of a backend.
        /// </summary>
        /// <param name="backend">The backend.</param>
        private static ServerType GetServerType(BackendType backend)
        {
            return backend switch
            {
                BackendType.StableDiffusionCpp => ServerType.StableDiffusionCpp,
                BackendType.AudioCpp => ServerType.AudioCpp,
                BackendType.LlamaCpp => ServerType.LlamaCpp,
                _ => throw new NotImplementedException()
            };
        }


        /// <summary>
        /// Gets the folder the environment is installed to.
        /// </summary>
        /// <param name="environment">The environment.</param>
        private static string GetPath(EnvironmentModel environment)
        {
            var backendPath = GetBackendPath(environment);
            return Path.Combine(backendPath, environment.Environment);
        }


        /// <summary>
        /// Gets the folder that holds every environment of the environment's backend.
        /// </summary>
        /// <param name="environment">The environment.</param>
        private static string GetBackendPath(EnvironmentModel environment)
        {
            return environment.Backend switch
            {
                BackendType.AudioCpp => Path.Combine(App.DirectoryData, "RuntimeAudioCpp"),
                BackendType.LlamaCpp => Path.Combine(App.DirectoryData, "RuntimeLlamaCpp"),
                _ => Path.Combine(App.DirectoryData, "RuntimeStableDiffusionCpp")
            };
        }


        /// <summary>
        /// Gets the installed environments that need an update or rebuild, then the backends on the system's devices.
        /// Vendors can share an environment folder, so an update is only listed when the system has a device from its vendor.
        /// </summary>
        /// <param name="includeMissing">if set to <c>true</c> include backends with no installed environment.</param>
        /// <param name="includeInstalled">if set to <c>true</c> include backends that are already installed, to change or try other environments.</param>
        public IReadOnlyList<EnvironmentSetupItem> GetSetupEnvironments(bool includeMissing, bool includeInstalled = false)
        {
            var results = new List<EnvironmentSetupItem>();

            // Updates
            var systemDevices = _settings.Devices ?? [];
            foreach (var environment in _settings.Environments.Where(x => x.Status == BackendStatus.Update))
            {
                if (!Exists(environment))
                    continue;
                if (!systemDevices.Any(x => x.Vendor == environment.Vendor))
                    continue;
                if (results.Any(x => x.Environments.Any(e => IsSameFolder(e, environment))))
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


        /// <summary>
        /// Marks the environment, and every environment that shares its folder, as created and saves the settings.
        /// </summary>
        /// <param name="environment">The environment.</param>
        private async Task SaveEnvironmentStatusAsync(EnvironmentModel environment)
        {
            foreach (var shared in _settings.Environments.Where(x => IsSameFolder(x, environment)))
                shared.Status = BackendStatus.Create;

            await _settings.SaveAsync();
        }


        /// <summary>
        /// Determines whether two environments are installed to the same folder.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="other">The other environment.</param>
        private static bool IsSameFolder(EnvironmentModel environment, EnvironmentModel other)
        {
            return string.Equals(GetPath(environment), GetPath(other), StringComparison.OrdinalIgnoreCase);
        }
    }


    public interface IEnvironmentService
    {
        bool Exists(EnvironmentModel environment);

        Task CreateAsync(EnvironmentModel environment, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default);
        Task UpdateAsync(EnvironmentModel environment, IProgress<PipelineProgress> progressCallback, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(EnvironmentModel environment);
        Task SetDefaultAsync(EnvironmentModel environment);
        IReadOnlyList<EnvironmentModel> GetEnvironmentGroup(EnvironmentModel environment);

        EnvironmentModel GetEnvironment(Device device, BackendType backendType, PipelineType pipelineType);

        PipelineCreateOptions CreatePipelineOptions(EnvironmentModel environment, BackendStatus status = BackendStatus.Create);

        IReadOnlyList<EnvironmentSetupItem> GetSetupEnvironments(bool includeMissing, bool includeInstalled = false);
    }
}
