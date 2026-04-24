using Amuse.App.Common;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TensorStack.Common.Common;

namespace Amuse.App
{
    public static partial class HuggingFace
    {
        public static string GetCacheId(string repositoryUrl)
        {
            return $"models--{repositoryUrl.Replace("/", "--")}";
        }


        public static ModelStatusType ModelStatus(DiffusionModel model, string modelDirectory)
        {
            var isValid = false;
            if (model.Source == ModelSourceType.Folder)
                isValid = Directory.Exists(model.Path);
            else if (model.Source == ModelSourceType.HuggingFace)
                isValid = Directory.Exists(Path.Combine(modelDirectory, GetCacheId(model.Path)));
            else if (model.Source == ModelSourceType.SingleFile)
            {
                isValid = model.Checkpoint is not null && IsCheckpointInstalled(modelDirectory, model.Checkpoint.SingleFile);
            }
            else if (model.Source == ModelSourceType.Checkpoint)
            {
                isValid = model.Checkpoint is not null
                    && TryParseRepo(model.Path, out _)
                    && (string.IsNullOrEmpty(model.Checkpoint.TextEncoder) || IsCheckpointInstalled(modelDirectory, model.Checkpoint.TextEncoder))
                    && (string.IsNullOrEmpty(model.Checkpoint.TextEncoder2) || IsCheckpointInstalled(modelDirectory, model.Checkpoint.TextEncoder2))
                    && (string.IsNullOrEmpty(model.Checkpoint.TextEncoder3) || IsCheckpointInstalled(modelDirectory, model.Checkpoint.TextEncoder3))
                    && (string.IsNullOrEmpty(model.Checkpoint.Transformer) || IsCheckpointInstalled(modelDirectory, model.Checkpoint.Transformer))
                    && (string.IsNullOrEmpty(model.Checkpoint.Transformer2) || IsCheckpointInstalled(modelDirectory, model.Checkpoint.Transformer2))
                    && (string.IsNullOrEmpty(model.Checkpoint.Vae) || IsCheckpointInstalled(modelDirectory, model.Checkpoint.Vae))
                    && (string.IsNullOrEmpty(model.Checkpoint.AudioVae) || IsCheckpointInstalled(modelDirectory, model.Checkpoint.AudioVae))
                    && (string.IsNullOrEmpty(model.Checkpoint.Vocoder) || IsCheckpointInstalled(modelDirectory, model.Checkpoint.Vocoder))
                    && (string.IsNullOrEmpty(model.Checkpoint.Connectors) || IsCheckpointInstalled(modelDirectory, model.Checkpoint.Connectors));
            }

            if (!isValid)
            {
                // Files not found
                return ModelStatusType.Pending;
            }
            else
            {
                // Files found
                if (model.Status == ModelStatusType.Pending || model.Status == ModelStatusType.Verifying)
                    return ModelStatusType.Unknown;
                else if (model.Status == ModelStatusType.Downloading || model.Status == ModelStatusType.DownloadQueue || model.Status == ModelStatusType.DownloadFailed)
                    return ModelStatusType.Pending;
            }
            return model.Status;
        }


        public static ModelStatusType ModelStatus(LoraAdapterModel model, string modelDirectory)
        {
            var isValid = false;
            if (model.Source == ModelSourceType.Folder)
                isValid = Directory.Exists(model.Path);
            else if (model.Source == ModelSourceType.SingleFile)
            {
                isValid = IsLoraAdapterInstalled(modelDirectory, model.Path, model.Weights);
            }
            else if (model.Source == ModelSourceType.HuggingFace)
            {
                isValid = IsLoraAdapterInstalled(modelDirectory, model.Path, model.Weights);
            }

            if (model.Status == ModelStatusType.Pending && isValid)
                return ModelStatusType.Installed;
            else if (model.Status == ModelStatusType.Installed && !isValid)
                return ModelStatusType.Pending;
            else if (model.Status == ModelStatusType.Downloading || model.Status == ModelStatusType.DownloadQueue || model.Status == ModelStatusType.DownloadFailed || model.Status == ModelStatusType.Verifying)
                return ModelStatusType.Pending;
            return model.Status;
        }


        public static ModelStatusType ModelStatus(ControlNetModel model, string modelDirectory)
        {
            var isValid = false;
            if (model.Source == ModelSourceType.Folder)
                isValid = Directory.Exists(model.Path);
            else if (model.Source == ModelSourceType.SingleFile)
            {
                isValid = IsControlNetInstalled(modelDirectory, model.Path);
            }
            else if (model.Source == ModelSourceType.HuggingFace)
            {
                isValid = IsControlNetInstalled(modelDirectory, model.Path);
            }

            if (model.Status == ModelStatusType.Pending && isValid)
                return ModelStatusType.Installed;
            else if (model.Status == ModelStatusType.Installed && !isValid)
                return ModelStatusType.Pending;
            else if (model.Status == ModelStatusType.Downloading || model.Status == ModelStatusType.DownloadQueue || model.Status == ModelStatusType.DownloadFailed || model.Status == ModelStatusType.Verifying)
                return ModelStatusType.Pending;

            return model.Status;
        }


        public static void ModelDelete(DiffusionModel model, string modelDirectory)
        {
            if (model.Source == ModelSourceType.Folder)
            {
                FileHelper.DeleteDirectory(model.Path);
            }
            else if (model.Source == ModelSourceType.SingleFile)
            {
                DeleteCacheFile(model.Checkpoint.SingleFile);
            }
            else if (model.Source == ModelSourceType.HuggingFace)
            {
                DeleteCacheDirectory(modelDirectory, GetCacheId(model.Path));
            }
            else if (model.Source == ModelSourceType.Checkpoint)
            {
                if (!string.IsNullOrEmpty(model.Checkpoint?.SingleFile))
                {
                    DeleteCacheFile(model.Checkpoint.SingleFile);
                }
                else if (!string.IsNullOrEmpty(model.Checkpoint?.Transformer))
                {
                    DeleteCacheFile(model.Checkpoint.Transformer);
                }
                else if (!string.IsNullOrEmpty(model.Checkpoint?.Transformer2))
                {
                    DeleteCacheFile(model.Checkpoint.Transformer2);
                }
            }
        }


        public static void ModelDelete(LoraAdapterModel model, string modelDirectory)
        {
            if (model.Source == ModelSourceType.Folder)
            {
                FileHelper.DeleteDirectory(model.Path);
            }
            else if (model.Source == ModelSourceType.SingleFile)
            {
                DeleteCacheFile(Path.Combine(model.Path, model.Weights));
            }
            else if (model.Source == ModelSourceType.HuggingFace)
            {
                var cacheId = GetCacheId(model.Path);
                var cachePath = Path.Combine(modelDirectory, cacheId);
                var snapshotsPath = Path.Combine(modelDirectory, cacheId, "snapshots");
                var weightsFile = FileHelper.FindFile(cachePath, model.Weights);
                if (weightsFile?.Exists == true)
                {
                    DeleteCacheFile(weightsFile.FullName);

                    var snapshot = weightsFile.DirectoryName;
                    if (FileHelper.IsDirectoryEmpty(snapshot))
                        FileHelper.DeleteDirectory(snapshot);
                }

                if (FileHelper.IsDirectoryEmpty(snapshotsPath))
                {
                    DeleteCacheDirectory(modelDirectory, cacheId);
                }
            }
        }


        public static void ModelDelete(ControlNetModel model, string modelDirectory)
        {
            if (model.Source == ModelSourceType.Folder)
            {
                FileHelper.DeleteDirectory(model.Path);
            }
            else if (model.Source == ModelSourceType.SingleFile)
            {
                DeleteCacheFile(model.Path);
            }
            else if (model.Source == ModelSourceType.HuggingFace || model.Source == ModelSourceType.Checkpoint)
            {
                DeleteCacheDirectory(modelDirectory, GetCacheId(model.Path));
            }
        }


        public static string ModelDirectory(DiffusionModel model, string modelDirectory)
        {
            if (model.Source == ModelSourceType.Folder)
            {
                return model.Path;
            }
            else if (model.Source == ModelSourceType.SingleFile)
            {
                var cacheId = GetCacheId(model.Path);
                var cachePath = Path.Combine(modelDirectory, cacheId);
                if (Directory.Exists(cachePath))
                    return cachePath;

                var directory = Path.GetDirectoryName(model.Checkpoint.SingleFile);
                if (Path.IsPathRooted(directory))
                    return directory;
            }
            return Path.Combine(modelDirectory, GetCacheId(model.Path));
        }


        public static string ModelDirectory(LoraAdapterModel model, string modelDirectory)
        {
            if (model.Source == ModelSourceType.Folder)
            {
                return model.Path;
            }
            else if (model.Source == ModelSourceType.SingleFile)
            {
                var directory = Path.GetDirectoryName(model.Path);
                if (Path.IsPathRooted(directory))
                    return directory;
            }
            return Path.Combine(modelDirectory, GetCacheId(model.Path));
        }


        public static string ModelDirectory(ControlNetModel model, string modelDirectory)
        {
            if (model.Source == ModelSourceType.SingleFile)
            {
                var directory = Path.GetDirectoryName(model.Path);
                if (Path.IsPathRooted(directory))
                    return directory;
            }
            return Path.Combine(modelDirectory, GetCacheId(model.Path));
        }



        public static bool IsCheckpointInstalled(string modelDirectory, string checkpoint)
        {
            if (string.IsNullOrEmpty(checkpoint))
                return false;

            if (File.Exists(checkpoint))
                return true;

            if (!TryParseRepo(checkpoint, out var repositoryId))
                return false;

            var directory = Path.Combine(modelDirectory, GetCacheId(repositoryId));
            if (!Directory.Exists(directory))
                return false;

            var filename = Path.GetFileName(checkpoint);
            return Directory.EnumerateFiles(directory, filename, SearchOption.AllDirectories).Any();
        }


        public static bool IsLoraAdapterInstalled(string modelDirectory, string path, string weights)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(weights))
                return false;

            var adapter = Path.Combine(path, weights);
            if (File.Exists(adapter))
                return true;

            if (!TryParseRepo(path, out var repositoryId))
                return false;

            var directory = Path.Combine(modelDirectory, GetCacheId(repositoryId));
            if (!Directory.Exists(directory))
                return false;

            return FindCacheFile(directory, weights, SearchOption.AllDirectories) != null;
        }


        public static bool IsControlNetInstalled(string modelDirectory, string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (File.Exists(path))
                return true;

            if (!TryParseRepo(path, out var repositoryId))
                return false;

            var directory = Path.Combine(modelDirectory, GetCacheId(repositoryId));
            return Directory.Exists(directory);
        }


        public static bool IsValidLink(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (url.Split('/', StringSplitOptions.RemoveEmptyEntries).Length == 2)
                return true; // username/repository

            return HuggingFaceLinkRegex.IsMatch(url);
        }


        public static bool TryParseRepo(string input, out string repoId)
        {
            repoId = null;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (input.Contains("/blob/main/"))
                input = input.Split("/blob/main/").FirstOrDefault();

            var match = HuggingFaceRepoRegex.Match(input.Trim());
            if (!match.Success)
                return false;

            repoId = match.Groups["repo"].Value;
            return true;
        }


        private static FileInfo FindCacheFile(string directory, string filename, SearchOption searchOption = SearchOption.AllDirectories)
        {
            var file = Directory.EnumerateFiles(directory, filename, searchOption).FirstOrDefault();
            if (string.IsNullOrEmpty(file))
                return default;

            return new FileInfo(file);
        }


        private static void DeleteCacheDirectory(string modelDirectory, string cacheId)
        {
            var cachePath = Path.Combine(modelDirectory, cacheId);
            var cacheLockPath = Path.Combine(modelDirectory, ".locks", cacheId);
            FileHelper.DeleteDirectory(cachePath);
            FileHelper.DeleteDirectory(cacheLockPath);
        }


        private static bool DeleteCacheFile(string filename)
        {
            try
            {
                if (!File.Exists(filename))
                    return true;

                var fileInfo = new FileInfo(filename);
                if (fileInfo.Exists)
                {
                    if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        var targetPath = fileInfo.LinkTarget;
                        if (!string.IsNullOrEmpty(targetPath))
                        {
                            var tagetFile = Path.GetFullPath(targetPath, fileInfo.DirectoryName);
                            if (File.Exists(tagetFile))
                            {
                                File.Delete(tagetFile);
                            }
                        }
                    }
                    File.Delete(filename);
                }

                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }


        private static readonly Regex HuggingFaceLinkRegex = CreateHuggingFaceLinkRegex();
        private static readonly Regex HuggingFaceRepoRegex = CreateHuggingFaceRepoRegex();

        [GeneratedRegex(@"^https?:\/\/(www\.)?huggingface\.co\/(datasets\/|spaces\/)?[\w.-]+\/[\w.-]+", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-NZ")]
        private static partial Regex CreateHuggingFaceLinkRegex();

        [GeneratedRegex(@"^(?:https?:\/\/)?(?:www\.)?huggingface\.co\/(?<repo>[^\/\s]+\/[^\/\s]+)$|^(?<repo>[^\/\s]+\/[^\/\s]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-NZ")]
        private static partial Regex CreateHuggingFaceRepoRegex();
    }
}
