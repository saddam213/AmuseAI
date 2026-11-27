using Amuse.App.Resources;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class CheckpointComponent : BaseModel
    {
        private string _name;
        private CheckpointType _type;
        private string _path;
        private string _folder;
        private string _mainFile;
        private string[] _downloadFiles;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool AutoMap { get; init; }

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        public CheckpointType Type
        {
            get { return _type; }
            set { SetProperty(ref _type, value); }
        }


        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Path
        {
            get { return _path; }
            set { SetProperty(ref _path, value); }
        }


        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Folder
        {
            get
            {
                if (_type == CheckpointType.LocalFile || _type == CheckpointType.LocalFolder || _type == CheckpointType.Component)
                    return null;

                return _folder;
            }
            set { SetProperty(ref _folder, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string MainFile
        {
            get { return _mainFile; }
            set { SetProperty(ref _mainFile, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[] DownloadFiles
        {
            get
            {
                if (_type == CheckpointType.LocalFile || _type == CheckpointType.LocalFolder || _type == CheckpointType.Component)
                    return null;

                return _downloadFiles;
            }
            set { SetProperty(ref _downloadFiles, value); }
        }


        public bool IsValid()
        {
            return IsValid(out _);
        }


        public bool IsValid(out string validation)
        {
            if (_type == CheckpointType.OnlineFolder)
            {
                if (_downloadFiles.IsNullOrEmpty())
                {
                    validation = AppErrors.CheckpointNoDownloadFiles;
                    return false;
                }
            }
            else if (_type == CheckpointType.OnlineFile)
            {
                if (_downloadFiles.IsNullOrEmpty())
                {
                    validation = AppErrors.CheckpointDownloadFileNotFound;
                    return false;
                }
            }
            else if (_type == CheckpointType.LocalFolder)
            {
                if (!System.IO.Directory.Exists(_path))
                {
                    validation = AppErrors.CheckpointLocalFolderNotFound;
                    return false;
                }
            }
            else if (_type == CheckpointType.LocalFile)
            {
                if (!System.IO.File.Exists(_path))
                {
                    validation = AppErrors.CheckpointLocalFileNotFound;
                    return false;
                }
            }
            else if (_type == CheckpointType.Component)
            {
                if (string.IsNullOrEmpty(_path))
                {
                    validation = AppErrors.CheckpointComponentNotSelected;
                    return false;
                }
            }
            validation = null;
            return true;
        }


        /// <summary>
        /// Resolves the path the checkpoint is loaded from. An online checkpoint that is not in the model directory resolves to the
        /// model search folder that holds its files, so the result can be a folder shared with other applications.
        /// Never delete or move the result, <see cref="DeleteFiles"/> deletes only the files of the checkpoint.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="modelDirectory">The model directory, the root model directory when <c>null</c>.</param>
        /// <param name="isFileMode">if set to <c>true</c> a folder checkpoint resolves to its main file.</param>
        public string Resolve(Settings settings, string modelDirectory, bool isFileMode = false)
        {
            var directory = modelDirectory ?? settings.DirectoryModel;
            if (_type == CheckpointType.OnlineFolder)
            {
                var installDirectory = FindDirectory(settings, directory) ?? GetSafePath(directory, _folder, _path);
                return GetSafePath(installDirectory, null, null, isFileMode ? _mainFile : null);
            }
            if (_type == CheckpointType.OnlineFile)
            {
                var installDirectory = FindDirectory(settings, directory) ?? GetSafePath(directory, _folder, _path);
                var path = GetSafePath(installDirectory, null, null, isFileMode ? _mainFile : null);
                var modelFiles = FileHelper.GetFileMapping(DownloadFiles, path);
                if (modelFiles.IsNullOrEmpty())
                    throw new System.Exception(string.Format(AppErrors.CheckpointResolveOnlineFile, _path));

                return modelFiles.Values.FirstOrDefault();
            }
            if (_type == CheckpointType.Component)
            {
                var component = settings.Components?.FirstOrDefault(x => x.Key == _path);
                if (component == null)
                    throw new System.Exception(string.Format(AppErrors.CheckpointResolveComponent, _path));

                return component.Checkpoint.Resolve(settings, null, isFileMode);
            }
            if (_type == CheckpointType.LocalFolder)
            {
                return GetSafePath(_path, null, null, isFileMode ? _mainFile : null);
            }
            return _path;
        }


        /// <summary>
        /// Determines whether the files of the checkpoint are installed, in the model directory or in a model search folder.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="modelDirectory">The model directory, the root model directory when <c>null</c>.</param>
        public bool IsInstalled(Settings settings, string modelDirectory)
        {
            if (_type == CheckpointType.OnlineFolder || _type == CheckpointType.OnlineFile)
            {
                return FindDirectory(settings, modelDirectory ?? settings.DirectoryModel) != null;
            }
            else if (_type == CheckpointType.LocalFolder)
            {
                return System.IO.Directory.Exists(_path);
            }
            else if (_type == CheckpointType.LocalFile)
            {
                return System.IO.File.Exists(_path);
            }
            else if (_type == CheckpointType.Component)
            {
                var component = settings.Components?.FirstOrDefault(x => x.Key == _path);
                if (component == null)
                    return false;

                return component.Status == ModelStatusType.Installed;
            }
            return false;
        }


        /// <summary>
        /// Deletes the checkpoint files, downloaded checkpoints only remove their own files as folders can be shared.
        /// The files are deleted from the model directory and from every model search folder that holds them,
        /// empty folders are only removed inside the model directory.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="modelDirectory">The model directory.</param>
        public void DeleteFiles(Settings settings, string modelDirectory)
        {
            if (_type == CheckpointType.LocalFile)
            {
                FileHelper.DeleteFile(_path);
            }
            else if (_type == CheckpointType.LocalFolder)
            {
                FileHelper.DeleteDirectory(_path);
            }
            else if (_type == CheckpointType.OnlineFile || _type == CheckpointType.OnlineFolder)
            {
                if (DownloadFiles.IsNullOrEmpty())
                    return;

                var directory = System.IO.Path.GetFullPath(modelDirectory ?? settings.DirectoryModel);
                var path = GetSafePath(directory, _folder, _path);
                var modelFiles = GetModelFiles(path);
                var searchFiles = settings.ModelSearch
                    .FindDirectories(path, x => GetModelFiles(x).Values, IsSingleFile())
                    .SelectMany(x => GetModelFiles(x).Values)
                    .ToList();
                foreach (var modelFile in modelFiles.Values.Concat(searchFiles))
                    FileHelper.DeleteFile(modelFile);

                var folders = modelFiles.Values
                    .Select(System.IO.Path.GetDirectoryName)
                    .Distinct()
                    .OrderByDescending(x => x.Length);
                foreach (var folder in folders)
                {
                    var current = System.IO.Path.GetFullPath(folder);
                    while (current.Length > directory.Length
                        && current.StartsWith(directory, System.StringComparison.OrdinalIgnoreCase)
                        && System.IO.Directory.Exists(current)
                        && !System.IO.Directory.EnumerateFileSystemEntries(current).Any())
                    {
                        FileHelper.DeleteDirectory(current, false);
                        current = System.IO.Path.GetDirectoryName(current);
                    }
                }
            }
        }


        public CheckpointComponent DeepClone()
        {
            return new CheckpointComponent
            {
                Name = Name,
                Type = Type,
                Folder = Folder,
                Path = Path,
                MainFile = MainFile,
                AutoMap = AutoMap,
                DownloadFiles = DownloadFiles?.ToArray(),
            };
        }


        /// <summary>
        /// Finds the directory that holds every file of an online checkpoint, the model directory first and then the model search folders.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="modelDirectory">The model directory.</param>
        /// <returns>The directory, or <c>null</c> when the files are not installed.</returns>
        private string FindDirectory(Settings settings, string modelDirectory)
        {
            var directory = GetSafePath(modelDirectory, _folder, _path);
            if (_downloadFiles.IsNullOrEmpty())
                return directory;

            if (GetModelFiles(directory).Values.All(System.IO.File.Exists))
                return directory;

            return settings.ModelSearch.FindDirectory(directory, x => GetModelFiles(x).Values, IsSingleFile());
        }


        /// <summary>
        /// Gets the files of an online checkpoint for a directory, keyed by download url.
        /// </summary>
        /// <param name="directory">The directory.</param>
        private Dictionary<string, string> GetModelFiles(string directory)
        {
            return _type == CheckpointType.OnlineFolder && AutoMap
                ? FileHelper.GetUrlFileMapping(_downloadFiles, directory)
                : FileHelper.GetFileMapping(_downloadFiles, directory);
        }


        /// <summary>
        /// Determines whether the checkpoint is one file that it resolves to, only these can be found by file name in a model search folder.
        /// </summary>
        private bool IsSingleFile()
        {
            if (_downloadFiles.IsNullOrEmpty() || _downloadFiles.Length != 1)
                return false;

            if (_type == CheckpointType.OnlineFile)
                return true;

            return string.Equals(_mainFile, System.IO.Path.GetFileName(_downloadFiles[0]), System.StringComparison.OrdinalIgnoreCase);
        }


        public static string GetSafePath(string directory, string folder, string path, string mainFile = null)
        {
            var finalPath = directory;
            if (!string.IsNullOrEmpty(folder))
            {
                finalPath = System.IO.Path.Combine(finalPath, folder);

            }
            if (!string.IsNullOrEmpty(path))
            {
                finalPath = System.IO.Path.Combine(finalPath, path);
            }
            if (!string.IsNullOrEmpty(mainFile))
            {
                finalPath = System.IO.Path.Combine(finalPath, mainFile);
            }

            return finalPath;
        }
    }
}
