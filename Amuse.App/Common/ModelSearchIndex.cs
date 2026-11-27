using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Amuse.App.Common
{
    /// <summary>
    /// An immutable snapshot of the model search folders, used to find checkpoint files that are not in the model directory.
    /// Nothing is downloaded or moved to a search folder, deleting a model deletes its own files from them but never a folder.
    /// </summary>
    public sealed class ModelSearchIndex
    {
        public static readonly ModelSearchIndex Empty = new ModelSearchIndex(null, null, null);

        private readonly string _modelDirectory;
        private readonly Dictionary<string, string[]> _searchDirectories;
        private readonly HashSet<string> _ambiguousNames;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelSearchIndex"/> class.
        /// </summary>
        /// <param name="modelDirectory">The model directory.</param>
        /// <param name="searchPaths">The search paths, missing folders and folders inside the model directory are ignored.</param>
        /// <param name="checkpoints">Every checkpoint of the settings, used to find the file names that are not unique.</param>
        public ModelSearchIndex(string modelDirectory, IEnumerable<ModelSearchPath> searchPaths, IEnumerable<CheckpointComponent> checkpoints)
        {
            _modelDirectory = GetFullPath(modelDirectory);
            _searchDirectories = GetSearchDirectories(_modelDirectory, searchPaths);
            _ambiguousNames = GetAmbiguousNames(checkpoints);
        }


        /// <summary>
        /// Finds the first search folder directory that holds the files of a checkpoint.
        /// </summary>
        /// <param name="directory">The directory of the checkpoint in the model directory.</param>
        /// <param name="getFiles">Gets the files of the checkpoint for a directory.</param>
        /// <param name="isSingleFile">if set to <c>true</c> the checkpoint is a single file.</param>
        /// <returns>The directory, or <c>null</c> when the files are not in a search folder.</returns>
        public string FindDirectory(string directory, Func<string, IReadOnlyCollection<string>> getFiles, bool isSingleFile)
        {
            return FindDirectories(directory, getFiles, isSingleFile).FirstOrDefault();
        }


        /// <summary>
        /// Finds every search folder directory that holds the files of a checkpoint.
        /// The directory is matched by the sub path it has in its model folder, a single file checkpoint is also matched by its file name in the search folder itself.
        /// A file directly in a search folder is only matched when its name is used by a single download.
        /// </summary>
        /// <param name="directory">The directory of the checkpoint in the model directory.</param>
        /// <param name="getFiles">Gets the files of the checkpoint for a directory.</param>
        /// <param name="isSingleFile">if set to <c>true</c> the checkpoint is a single file.</param>
        public IEnumerable<string> FindDirectories(string directory, Func<string, IReadOnlyCollection<string>> getFiles, bool isSingleFile)
        {
            if (_searchDirectories.Count == 0)
                yield break;

            var checkpointDirectory = GetFullPath(directory);
            if (!IsSubPath(_modelDirectory, checkpointDirectory))
                yield break;

            var relativePath = Path.GetRelativePath(_modelDirectory, checkpointDirectory);
            if (relativePath == ".")
                yield break;

            var separatorIndex = relativePath.IndexOf(Path.DirectorySeparatorChar);
            var folder = separatorIndex < 0 ? relativePath : relativePath[..separatorIndex];
            var subPath = separatorIndex < 0 ? string.Empty : relativePath[(separatorIndex + 1)..];
            if (!_searchDirectories.TryGetValue(folder, out var searchDirectories))
                yield break;

            foreach (var searchDirectory in searchDirectories)
            {
                var subDirectory = Path.Combine(searchDirectory, subPath);
                if (HasFiles(searchDirectory, getFiles(subDirectory)))
                    yield return subDirectory;

                if (isSingleFile && subPath.Length > 0 && HasFiles(searchDirectory, getFiles(searchDirectory)))
                    yield return searchDirectory;
            }
        }


        /// <summary>
        /// Determines whether the path is the directory or is inside it.
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <param name="path">The path.</param>
        public static bool IsSubPath(string directory, string path)
        {
            var fullDirectory = GetFullPath(directory);
            var fullPath = GetFullPath(path);
            if (fullDirectory == null || fullPath == null)
                return false;

            var relativePath = Path.GetRelativePath(fullDirectory, fullPath);
            if (relativePath == ".")
                return true;

            return !Path.IsPathRooted(relativePath)
                && relativePath != ".."
                && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }


        /// <summary>
        /// Determines whether every file exists, files directly in the search folder must have a name that is used by a single download.
        /// </summary>
        /// <param name="searchDirectory">The search folder.</param>
        /// <param name="files">The files.</param>
        private bool HasFiles(string searchDirectory, IReadOnlyCollection<string> files)
        {
            if (files == null || files.Count == 0)
                return false;

            foreach (var file in files)
            {
                var isSearchDirectoryFile = string.Equals(Path.GetDirectoryName(file), searchDirectory, StringComparison.OrdinalIgnoreCase);
                if (isSearchDirectoryFile && _ambiguousNames.Contains(Path.GetFileName(file)))
                    return false;

                if (!File.Exists(file))
                    return false;
            }
            return true;
        }


        /// <summary>
        /// Gets the existing search folders of each model folder, keyed by the model folder name.
        /// </summary>
        /// <param name="modelDirectory">The model directory.</param>
        /// <param name="searchPaths">The search paths.</param>
        private static Dictionary<string, string[]> GetSearchDirectories(string modelDirectory, IEnumerable<ModelSearchPath> searchPaths)
        {
            var searchDirectories = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (modelDirectory == null || searchPaths == null)
                return searchDirectories;

            foreach (var folderPaths in searchPaths.Where(x => x != null).GroupBy(x => x.Folder))
            {
                var directories = folderPaths
                    .Select(x => GetFullPath(x.Path))
                    .Where(x => x != null && !IsSubPath(modelDirectory, x) && Directory.Exists(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (directories.Length > 0)
                    searchDirectories.Add(ModelFolders.GetName(folderPaths.Key), directories);
            }
            return searchDirectories;
        }


        /// <summary>
        /// Gets the file names that more than one download uses, a file with one of these names cannot be identified by its name alone.
        /// </summary>
        /// <param name="checkpoints">The checkpoints.</param>
        private static HashSet<string> GetAmbiguousNames(IEnumerable<CheckpointComponent> checkpoints)
        {
            var ambiguousNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (checkpoints == null)
                return ambiguousNames;

            var downloadFiles = checkpoints
                .Where(x => x != null && x.DownloadFiles != null)
                .SelectMany(x => x.DownloadFiles)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var fileDownloads in downloadFiles.GroupBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase))
            {
                if (fileDownloads.Count() > 1)
                    ambiguousNames.Add(fileDownloads.Key);
            }
            return ambiguousNames;
        }


        /// <summary>
        /// Gets the full path without a trailing separator.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The full path, or <c>null</c> when the path is empty or not a valid path.</returns>
        private static string GetFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
