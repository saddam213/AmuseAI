using Amuse.App.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common.Common;

namespace Amuse.App.Services
{
    public sealed class MigrationService : IMigrationService
    {
        private static readonly FolderRename[] _modelFolderRenames =
        [
            new FolderRename("Diffusion", ModelFolders.Diffusion),
            new FolderRename("TextEncoder", ModelFolders.TextEncoder),
            new FolderRename("LLM", ModelFolders.LLM),
            new FolderRename("AutoEncoder", ModelFolders.AutoEncoder),
            new FolderRename("LoraAdapter", ModelFolders.LoraAdapter),
            new FolderRename("ControlNet", ModelFolders.ControlNet),
            new FolderRename("Upscale", ModelFolders.Upscale),
            new FolderRename("Extract", ModelFolders.Extract),
            new FolderRename("Tokenizer", ModelFolders.Tokenizer),
            new FolderRename("Components", ModelFolders.Components)
        ];

        private static readonly string[] _environmentFolders = ["RuntimeStableDiffusionCpp", "RuntimeAudioCpp", "RuntimeLlamaCpp"];

        private readonly Settings _settings;
        private readonly ILogger<MigrationService> _logger;

        public MigrationService(Settings settings, ILogger<MigrationService> logger)
        {
            _logger = logger;
            _settings = settings;
        }


        /// <summary>
        /// Runs the model folder migrations now, used when the model folder changes. Installed environments are left alone.
        /// </summary>
        public Task RunMigrationsAsync()
        {
            _settings.RunMigrations = true;
            return RunMigrationsAsync(false);
        }


        /// <summary>
        /// Runs the migrations on startup when they are pending. Before the first run has completed the environments of earlier versions are deleted first,
        /// they cannot be upgraded, so the first run setup installs them fresh.
        /// </summary>
        public Task RunAutoMigrationsAsync()
        {
            return RunMigrationsAsync(true);
        }


        /// <summary>
        /// Runs the pending migrations.
        /// </summary>
        /// <param name="isStartup">if set to <c>true</c> the app is starting, so the environments of earlier versions can be deleted.</param>
        private async Task RunMigrationsAsync(bool isStartup)
        {
            _logger.LogInformation("[MigrationService] [RunMigrations] Checking migrations...");
            if (!_settings.RunMigrations)
            {
                _logger.LogInformation("[MigrationService] [RunMigrations] Migrations not required.");
                return;
            }

            if (isStartup && !_settings.IsFirstRunComplete)
                await DeleteEnvironmentsAsync();

            var modelDirectory = _settings.DirectoryModel;
            var languageModelFolders = GetLanguageModelFolders();
            await Task.Run(() => MigrateModelFolders(modelDirectory, languageModelFolders));

            _settings.ScanModels();
            _settings.RunMigrations = false;
            await _settings.SaveAsync();
            _logger.LogInformation("[MigrationService] [RunMigrations] Migrations complete.");
        }


        /// <summary>
        /// Deletes the environment folders of earlier versions, so no backend is seen as installed and the first run setup installs every environment fresh.
        /// The legacy RuntimeHuggingFace folder is not touched.
        /// </summary>
        private async Task DeleteEnvironmentsAsync()
        {
            foreach (var environmentFolder in _environmentFolders)
            {
                var timestamp = Stopwatch.GetTimestamp();
                var path = Path.Combine(App.DirectoryData, environmentFolder);
                try
                {
                    if (!Directory.Exists(path))
                        continue;

                    if (await FileHelper.DeleteDirectoryAsync(path, retries: 10))
                        _logger.LogInformation("[MigrationService] [Environments] Deleted environment folder, Path: {Path}, Elapsed: {Elapsed:c}", path, Stopwatch.GetElapsedTime(timestamp));
                    else
                        _logger.LogWarning("[MigrationService] [Environments] Failed to delete environment folder, Path: {Path}", path);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MigrationService] [Environments] An exception occurred deleting the environment folder, Path: {Path}", path);
                }
            }
        }


        /// <summary>
        /// Renames the model folders of earlier versions to the current folder names, and moves the language models out of the text encoder folder they used to share.
        /// Nothing is overwritten, a file that already exists in the new folder is left where it was.
        /// </summary>
        /// <param name="modelDirectory">The model directory.</param>
        /// <param name="languageModelFolders">The folders of the language models.</param>
        private void MigrateModelFolders(string modelDirectory, IReadOnlyList<string> languageModelFolders)
        {
            var timestamp = Stopwatch.GetTimestamp();
            try
            {
                foreach (var folderRename in _modelFolderRenames)
                {
                    var source = FindDirectory(modelDirectory, folderRename.OldName);
                    if (source != null)
                        MoveDirectory(source, Path.Combine(modelDirectory, folderRename.NewName));
                }

                foreach (var languageModelFolder in languageModelFolders)
                {
                    var source = Path.Combine(modelDirectory, ModelFolders.TextEncoder, languageModelFolder);
                    if (Directory.Exists(source))
                        MoveDirectory(source, Path.Combine(modelDirectory, ModelFolders.LLM, languageModelFolder));
                }

                _logger.LogInformation("[MigrationService] [ModelFolders] Model folders checked, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MigrationService] [ModelFolders] An exception occurred migrating the model folders, ModelDirectory: {ModelDirectory}", modelDirectory);
            }
        }


        /// <summary>
        /// Moves a directory. A rename that only changes the case goes through a temporary name, an existing destination is merged into.
        /// </summary>
        /// <param name="source">The source directory.</param>
        /// <param name="destination">The destination directory.</param>
        private void MoveDirectory(string source, string destination)
        {
            if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            {
                var temporary = destination + ".rename";
                Directory.Move(source, temporary);
                Directory.Move(temporary, destination);
            }
            else if (Directory.Exists(destination))
            {
                MergeDirectory(source, destination);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                Directory.Move(source, destination);
            }
            _logger.LogInformation("[MigrationService] [ModelFolders] Moved model folder, Source: {Source}, Destination: {Destination}", source, destination);
        }


        /// <summary>
        /// Moves the contents of a directory into an existing directory, a file that already exists in the destination is left in the source.
        /// The source is removed when nothing is left in it.
        /// </summary>
        /// <param name="source">The source directory.</param>
        /// <param name="destination">The destination directory.</param>
        private void MergeDirectory(string source, string destination)
        {
            foreach (var directory in Directory.GetDirectories(source))
            {
                var target = Path.Combine(destination, Path.GetFileName(directory));
                if (Directory.Exists(target))
                    MergeDirectory(directory, target);
                else
                    Directory.Move(directory, target);
            }

            foreach (var file in Directory.GetFiles(source))
            {
                var target = Path.Combine(destination, Path.GetFileName(file));
                if (File.Exists(target))
                {
                    _logger.LogWarning("[MigrationService] [ModelFolders] File already exists in the new folder and was not moved, File: {File}", file);
                    continue;
                }
                File.Move(file, target);
            }

            if (!Directory.EnumerateFileSystemEntries(source).Any())
                Directory.Delete(source);
        }


        /// <summary>
        /// Gets the folders of the language models, earlier versions kept them in the text encoder folder.
        /// A folder that a text encoder component also uses is left out.
        /// </summary>
        private List<string> GetLanguageModelFolders()
        {
            var componentFolders = _settings.Components
                .Where(x => x.Checkpoint != null && string.Equals(x.Checkpoint.Folder, ModelFolders.TextEncoder, StringComparison.OrdinalIgnoreCase))
                .Select(x => GetRootFolder(x.Checkpoint.Path))
                .Where(x => x != null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return _settings.LanguageModels
                .SelectMany(x => x.GetCheckpoints())
                .SelectMany(x => x.GetComponents())
                .Select(x => GetRootFolder(x.Folder))
                .Where(x => x != null && !componentFolders.Contains(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }


        /// <summary>
        /// Finds a directory by its exact name. The file system ignores case, so a folder that only differs in case has to be matched by name.
        /// </summary>
        /// <param name="parentDirectory">The parent directory.</param>
        /// <param name="name">The directory name.</param>
        /// <returns>The directory, or <c>null</c> when there is no directory with exactly that name.</returns>
        private static string FindDirectory(string parentDirectory, string name)
        {
            if (!Directory.Exists(parentDirectory))
                return null;

            return Directory.EnumerateDirectories(parentDirectory)
                .FirstOrDefault(x => string.Equals(Path.GetFileName(x), name, StringComparison.Ordinal));
        }


        /// <summary>
        /// Gets the first folder of a relative path.
        /// </summary>
        /// <param name="path">The relative path.</param>
        private static string GetRootFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        }


        /// <summary>
        /// A model folder name of an earlier version and its current name.
        /// </summary>
        private sealed record FolderRename(string OldName, string NewName);
    }


    public interface IMigrationService
    {
        Task RunMigrationsAsync();
        Task RunAutoMigrationsAsync();
    }
}
