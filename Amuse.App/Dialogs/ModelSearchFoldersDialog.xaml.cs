// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Dialogs;
using TensorStack.WPF.Services;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for ModelSearchFoldersDialog.xaml
    /// </summary>
    public partial class ModelSearchFoldersDialog : DialogControl
    {
        private static readonly ComfyFolder[] _comfyFolders =
        [
            new ComfyFolder("diffusion_models", ModelFolderType.Diffusion),
            new ComfyFolder("unet", ModelFolderType.Diffusion),
            new ComfyFolder("checkpoints", ModelFolderType.Diffusion),
            new ComfyFolder("text_encoders", ModelFolderType.TextEncoder),
            new ComfyFolder("clip", ModelFolderType.TextEncoder),
            new ComfyFolder("vae", ModelFolderType.AutoEncoder),
            new ComfyFolder("loras", ModelFolderType.LoraAdapter),
            new ComfyFolder("controlnet", ModelFolderType.ControlNet),
            new ComfyFolder("upscale_models", ModelFolderType.Upscale)
        ];

        private readonly HashSet<string> _savedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelSearchFoldersDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public ModelSearchFoldersDialog(Settings settings)
        {
            Settings = settings;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            AddCommand = new AsyncRelayCommand(AddAsync);
            AddComfyUICommand = new AsyncRelayCommand(AddComfyUIAsync);
            BrowseCommand = new AsyncRelayCommand<ModelSearchPath>(BrowseAsync);
            RemoveCommand = new AsyncRelayCommand<ModelSearchPath>(RemoveAsync);
            SearchPaths = new ObservableCollection<ModelSearchPath>();
            Errors = new ObservableCollection<string>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand AddCommand { get; }
        public AsyncRelayCommand AddComfyUICommand { get; }
        public AsyncRelayCommand<ModelSearchPath> BrowseCommand { get; }
        public AsyncRelayCommand<ModelSearchPath> RemoveCommand { get; }
        public ObservableCollection<ModelSearchPath> SearchPaths { get; }
        public ObservableCollection<string> Errors { get; }


        /// <summary>
        /// Shows the dialog with a copy of the model search folders, the settings are only changed when the dialog is saved.
        /// </summary>
        public override Task<bool> ShowDialogAsync()
        {
            SearchPaths.Clear();
            _savedPaths.Clear();
            if (!Settings.ModelSearchPaths.IsNullOrEmpty())
            {
                foreach (var searchPath in Settings.ModelSearchPaths)
                {
                    SearchPaths.Add(searchPath.DeepClone());
                    _savedPaths.Add(searchPath.Path ?? string.Empty);
                }
            }
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Adds a search folder, the model folder is preselected when the folder has the name of a known model folder.
        /// </summary>
        private async Task AddAsync()
        {
            var directory = await DialogService.OpenFolderAsync(AppDefault.ModelSearchFolderSelect);
            if (string.IsNullOrEmpty(directory))
                return;

            AddSearchPath(GetFolderType(directory), directory);
        }


        /// <summary>
        /// Adds the model folders of a ComfyUI install, the install folder or its models folder can be selected.
        /// </summary>
        private async Task AddComfyUIAsync()
        {
            var directory = await DialogService.OpenFolderAsync(AppDefault.ModelSearchFoldersComfyUISelect);
            if (string.IsNullOrEmpty(directory))
                return;

            var modelDirectory = GetComfyModelDirectory(directory);
            var comfyFolders = _comfyFolders
                .Where(x => Directory.Exists(Path.Combine(modelDirectory, x.Name)))
                .ToList();
            if (comfyFolders.Count == 0)
            {
                await DialogService.ShowMessageAsync(AppDefault.ModelSearchFoldersComfyUI, AppDefault.ModelSearchFoldersComfyUINotFound, MessageDialogType.Ok, MessageBoxIconType.Info, MessageBoxStyleType.Info);
                return;
            }

            foreach (var comfyFolder in comfyFolders)
                AddSearchPath(comfyFolder.Folder, Path.Combine(modelDirectory, comfyFolder.Name));
        }


        /// <summary>
        /// Selects the directory of a search folder.
        /// </summary>
        /// <param name="searchPath">The search path.</param>
        private async Task BrowseAsync(ModelSearchPath searchPath)
        {
            var directory = await DialogService.OpenFolderAsync(AppDefault.ModelSearchFolderSelect, searchPath.Path);
            if (string.IsNullOrEmpty(directory))
                return;

            searchPath.Path = directory;
        }


        /// <summary>
        /// Removes a search folder.
        /// </summary>
        /// <param name="searchPath">The search path.</param>
        private Task RemoveAsync(ModelSearchPath searchPath)
        {
            SearchPaths.Remove(searchPath);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Saves the search folders to the settings, duplicates are removed.
        /// </summary>
        protected override Task SaveAsync()
        {
            var searchPaths = new List<ModelSearchPath>();
            foreach (var searchPath in SearchPaths)
            {
                var directory = Path.TrimEndingDirectorySeparator(searchPath.Path.Trim());
                if (searchPaths.Any(x => x.Folder == searchPath.Folder && string.Equals(x.Path, directory, StringComparison.OrdinalIgnoreCase)))
                    continue;

                searchPaths.Add(new ModelSearchPath { Folder = searchPath.Folder, Path = directory });
            }

            Settings.ModelSearchPaths = searchPaths.Count > 0 ? searchPaths : null;
            return base.SaveAsync();
        }


        /// <summary>
        /// Determines whether the search folders can be saved, and updates the validation errors.
        /// </summary>
        protected override bool CanExecuteSave()
        {
            Errors.Clear();
            foreach (var validationError in GetValidationErrors())
                Errors.Add(validationError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        /// <summary>
        /// Adds a search folder unless it is already in the list.
        /// </summary>
        /// <param name="folder">The model folder.</param>
        /// <param name="directory">The directory.</param>
        private void AddSearchPath(ModelFolderType folder, string directory)
        {
            if (SearchPaths.Any(x => x.Folder == folder && string.Equals(x.Path, directory, StringComparison.OrdinalIgnoreCase)))
                return;

            SearchPaths.Add(new ModelSearchPath { Folder = folder, Path = directory });
        }


        /// <summary>
        /// Gets the validation errors, a search folder inside the model directory would search the model directory twice. A folder saved earlier may be missing, such as a drive that is not connected, only a new folder must exist.
        /// </summary>
        private IEnumerable<string> GetValidationErrors()
        {
            if (SearchPaths.Any(x => string.IsNullOrWhiteSpace(x.Path)))
                yield return AppErrors.ValidationSearchFolderEmpty;
            if (SearchPaths.Any(x => ModelSearchIndex.IsSubPath(Settings.DirectoryModel, x.Path)))
                yield return AppErrors.ValidationSearchFolderInsideModels;
            if (SearchPaths.Any(x => !string.IsNullOrWhiteSpace(x.Path) && ModelSearchIndex.IsSubPath(x.Path, Settings.DirectoryModel)))
                yield return AppErrors.ValidationSearchFolderContainsModels;

            var missingPath = SearchPaths.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Path) && (!Path.IsPathFullyQualified(x.Path) || (!_savedPaths.Contains(x.Path) && !Directory.Exists(x.Path))));
            if (missingPath is not null)
                yield return string.Format(AppErrors.ValidationSearchFolderNotFound, missingPath.Path);
        }


        /// <summary>
        /// Gets the model folder a directory is for from its name, a ComfyUI or model folder name, <see cref="ModelFolderType.Diffusion"/> when the name is not known.
        /// </summary>
        /// <param name="directory">The directory.</param>
        private static ModelFolderType GetFolderType(string directory)
        {
            var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
            var comfyFolder = _comfyFolders.FirstOrDefault(x => string.Equals(x.Name, folderName, StringComparison.OrdinalIgnoreCase));
            if (comfyFolder != null)
                return comfyFolder.Folder;

            return Enum.GetValues<ModelFolderType>().FirstOrDefault(x => string.Equals(ModelFolders.GetName(x), folderName, StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// Gets the models folder of a ComfyUI install, the directory itself when it has no models folder.
        /// </summary>
        /// <param name="directory">The ComfyUI install folder, or its models folder.</param>
        private static string GetComfyModelDirectory(string directory)
        {
            string[] modelDirectories = [Path.Combine(directory, "models"), Path.Combine(directory, "ComfyUI", "models")];
            return modelDirectories.FirstOrDefault(Directory.Exists) ?? directory;
        }


        /// <summary>
        /// A ComfyUI model folder name and the model folder it maps to.
        /// </summary>
        private sealed record ComfyFolder(string Name, ModelFolderType Folder);
    }
}
