using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Amuse.App.Services
{
    public sealed class MigrationService : IMigrationService
    {
        private readonly Settings _settings;
        private readonly ILogger<MigrationService> _logger;

        public MigrationService(Settings settings, ILogger<MigrationService> logger)
        {
            _logger = logger;
            _settings = settings;
        }


        public Task RunMigrationsAsync()
        {
            _settings.RunMigrations = true;
            return RunAutoMigrationsAsync();
        }


        public async Task RunAutoMigrationsAsync()
        {
            _logger.LogInformation("[MigrationService] [RunMigrations] Checking migrations...");
            if (!_settings.RunMigrations)
            {
                _logger.LogInformation("[MigrationService] RunMigrations] Migrations not required.");
                return;
            }

            // Run runtime migrations
            if (IsRuntimeMigrationRequired())
            {
                _logger.LogInformation("[MigrationService] [RunMigrations] Runtime migrations found, Migrating...");
                RunRuntimeMigrations(false);
                _logger.LogInformation("[MigrationService] [RunMigrations] Runtime migrations complete.");
            }

            // Run model migrations
            if (IsModelMigrationRequired(_settings.DirectoryModel))
            {
                _logger.LogInformation("[MigrationService] [RunMigrations] Model migrations found, Migrating...");
                RunModelMigrations(_settings.DirectoryModel, false);
                _logger.LogInformation("[MigrationService] [RunMigrations] Model migrations complete.");
            }

            _settings.ScanModels();
            _settings.RunMigrations = false;
            await SettingsManager.SaveAsync(_settings);
            _logger.LogInformation("[MigrationService] [RunMigrations] Migrations complete.");
        }


        private bool IsRuntimeMigrationRequired()
        {
            return RunRuntimeMigrations(true);
        }


        private bool IsModelMigrationRequired(string modelDirectory)
        {
            return RunModelMigrations(modelDirectory, true);
        }


        private bool RunRuntimeMigrations(bool isReadOnly)
        {
            // v3.8.0 - Rename Runtime Folders
            var source1 = Path.Combine(App.DirectoryData, "PythonRuntime");
            var source2 = Path.Combine(App.DirectoryData, "StableDiffusionCppRuntime");
            var source3 = Path.Combine(App.DirectoryData, "Python");
            var source4 = Path.Combine(App.DirectoryData, "Pipelines");
            var destination1 = Path.Combine(App.DirectoryData, "RuntimeHuggingFace");
            var destination2 = Path.Combine(App.DirectoryData, "RuntimeStableDiffusionCpp");

            if (Directory.Exists(source1) || Directory.Exists(source2) || Directory.Exists(source3) || Directory.Exists(source4))
            {
                if (isReadOnly)
                    return true;
                try
                {
                    if (Directory.Exists(source1))
                        Directory.Move(source1, destination1);
                    if (Directory.Exists(source2))
                        Directory.Move(source2, destination2);
                    if (Directory.Exists(source3))
                        Directory.Delete(source3, true);
                    if (Directory.Exists(source4))
                        Directory.Delete(source4, true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MigrationService] [RuntimeMigration] ");
                }
            }
            return false;
        }


        private bool RunModelMigrations(string modelDirectory, bool isReadOnly)
        {
            // v3.7.0 - Flatten Lora directory
            var loraDirectory = Path.Combine(modelDirectory, "LoraAdapter");
            MoveMigration[] moveMigrations =
            [
                new MoveMigration(Path.Combine(loraDirectory, "Anima", "anima-greg-rutkowski-style.safetensors"), Path.Combine(loraDirectory, "anima-greg-rutkowski-style.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Anima", "anima-highres-aesthetic-boost.safetensors"), Path.Combine(loraDirectory, "anima-greg-rutkowski-style.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Anima", "anima-rl-v0.1.safetensors"), Path.Combine(loraDirectory,"anima-rl-v0.1.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Anima", "anima-turbo-lora-v0.2.safetensors"), Path.Combine(loraDirectory, "anima-turbo-lora-v0.2.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Flux1", "Hyper-FLUX.1-dev-8steps-lora.safetensors"), Path.Combine(loraDirectory, "Hyper-FLUX.1-dev-8steps-lora.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Flux2", "Flux2-Klein-9B-consistency-V2.safetensors"), Path.Combine(loraDirectory, "Flux2-Klein-9B-consistency-V2.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Flux2", "flux-background-remove-lora.safetensors"), Path.Combine(loraDirectory, "flux-background-remove-lora.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Flux2", "flux-klein-tryon.safetensors"), Path.Combine(loraDirectory, "flux-klein-tryon.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Flux2", "flux-outpaint-lora.safetensors"), Path.Combine(loraDirectory, "flux-outpaint-lora.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Flux2", "flux-red-zoom-lora.safetensors"), Path.Combine(loraDirectory, "flux-red-zoom-lora.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Flux2", "flux-spritesheet-lora.safetensors"), Path.Combine(loraDirectory, "flux-spritesheet-lora.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Flux2", "klein4b-doodle_v1.safetensors"), Path.Combine(loraDirectory, "klein4b-doodle_v1.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Flux2", "klein9b-doodle_v1.safetensors"), Path.Combine(loraDirectory, "klein9b-doodle_v1.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Flux2", "realistic.safetensors"), Path.Combine(loraDirectory, "realistic.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Krea2", "darkbrush.safetensors"), Path.Combine(loraDirectory, "darkbrush.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Krea2", "dotmatrix.safetensors"), Path.Combine(loraDirectory, "dotmatrix.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Krea2", "kidsdrawing.safetensors"), Path.Combine(loraDirectory, "kidsdrawing.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Krea2", "neondrip.safetensors"), Path.Combine(loraDirectory, "neondrip.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Krea2", "rainywindow.safetensors"), Path.Combine(loraDirectory, "rainywindow.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Krea2", "retroanime.safetensors"), Path.Combine(loraDirectory, "retroanime.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Krea2", "softwatercolor.safetensors"), Path.Combine(loraDirectory, "softwatercolor.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Krea2", "sunsetblur.safetensors"), Path.Combine(loraDirectory, "sunsetblur.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "Krea2", "vintagetarot.safetensors"), Path.Combine(loraDirectory, "vintagetarot.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "LTX20", "ltx-2-19b-lora-camera-control-dolly-in.safetensors"), Path.Combine(loraDirectory, "ltx-2-19b-lora-camera-control-dolly-in.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "LTX20", "ltx-2-19b-lora-camera-control-dolly-left.safetensors"), Path.Combine(loraDirectory, "ltx-2-19b-lora-camera-control-dolly-left.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "LTX20", "ltx-2-19b-lora-camera-control-dolly-out.safetensors"), Path.Combine(loraDirectory, "ltx-2-19b-lora-camera-control-dolly-out.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "LTX20", "ltx-2-19b-lora-camera-control-dolly-right.safetensors"), Path.Combine(loraDirectory, "ltx-2-19b-lora-camera-control-dolly-right.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "LTX20", "ltx-2-19b-lora-camera-control-jib-down.safetensors"), Path.Combine(loraDirectory, "ltx-2-19b-lora-camera-control-jib-down.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "LTX20", "ltx-2-19b-lora-camera-control-jib-up.safetensors"), Path.Combine(loraDirectory, "ltx-2-19b-lora-camera-control-jib-up.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "LTX20", "ltx-2-19b-lora-camera-control-static.safetensors"), Path.Combine(loraDirectory, "ltx-2-19b-lora-camera-control-static.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "LTX20", "ltx-2-19b-ic-lora-detailer.safetensors"), Path.Combine(loraDirectory, "ltx-2-19b-ic-lora-detailer.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "StableDiffusion3", "Hyper-SD3-8steps-CFG-lora.safetensors"), Path.Combine(loraDirectory, "Hyper-SD3-8steps-CFG-lora.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "StableDiffusionXL", "Hyper-SDXL-8steps-CFG-lora.safetensors"), Path.Combine(loraDirectory, "Hyper-SDXL-8steps-CFG-lora.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "ZImage", "40sCyberpunkZ_000003000.safetensors"), Path.Combine(loraDirectory, "40sCyberpunkZ_000003000.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "ZImage", "80sFantasyZ_000003000.safetensors"), Path.Combine(loraDirectory, "80sFantasyZ_000003000.safetensors")),
                new MoveMigration(Path.Combine(loraDirectory, "ZImage", "Coloring_Book_Z_Image_Turbo_v1_renderartist_2000.safetensors"), Path.Combine(loraDirectory, "Coloring_Book_Z_Image_Turbo_v1_renderartist_2000.safetensors")),
            ];

            // Remove empty lora
            DeleteMigration[] deleteMigrations =
            [
                new DeleteMigration(Path.Combine(loraDirectory, "Anima"), false),
                new DeleteMigration(Path.Combine(loraDirectory, "Flux1"), false),
                new DeleteMigration(Path.Combine(loraDirectory, "Flux2"), false),
                new DeleteMigration(Path.Combine(loraDirectory, "Krea2"), false),
                new DeleteMigration(Path.Combine(loraDirectory, "LTX20"), false),
                new DeleteMigration(Path.Combine(loraDirectory, "StableDiffusion3"), false),
                new DeleteMigration(Path.Combine(loraDirectory, "StableDiffusionXL"), false),
                new DeleteMigration(Path.Combine(loraDirectory, "ZImage"), false)
            ];

            return RunMigrations(moveMigrations, isReadOnly)
        || RunMigrations(deleteMigrations, isReadOnly);
        }


        private bool RunMigrations(DeleteMigration[] migrations, bool isReadOnly)
        {
            foreach (var migration in migrations)
            {
                try
                {
                    if (Directory.Exists(migration.Source))
                    {
                        if (isReadOnly)
                            return true;

                        _logger.LogInformation("[MigrationService] [DeleteMigration] Delete if empty, Source: {directory}", migration.Source);
                        Directory.Delete(migration.Source, migration.Recursive);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MigrationService] [DeleteMigration] Directory: {directory}", migration.Source);
                }
            }
            return false;
        }


        private bool RunMigrations(MoveMigration[] migrations, bool isReadOnly)
        {
            foreach (var migration in migrations)
            {
                try
                {
                    if (!File.Exists(migration.Source))
                        continue;

                    if (isReadOnly)
                        return true;

                    _logger.LogInformation("[MigrationService] [MoveMigration] Source: {Source}, Destination: {Destination}", migration.Source, migration.Destination);
                    File.Move(migration.Source, migration.Destination, true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MigrationService] [MoveMigration] Source: {Source}, Destination: {Destination}", migration.Source, migration.Destination);
                }
            }
            return false;
        }

        private record DeleteMigration(string Source, bool Recursive);
        private record MoveMigration(string Source, string Destination);
    }


    public interface IMigrationService
    {
        Task RunMigrationsAsync();
        Task RunAutoMigrationsAsync();
    }
}
