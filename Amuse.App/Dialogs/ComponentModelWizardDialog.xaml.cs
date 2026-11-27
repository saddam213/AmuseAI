// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for ComponentModelWizardDialog.xaml
    /// </summary>
    public partial class ComponentModelWizardDialog : DialogControl
    {
        private static readonly string[] _fileExtensions = [".safetensors", ".gguf"];

        private readonly IModelDownloadService _downloadService;
        private WizardComponentKind _selectedKind;
        private string _selectedName;
        private string _suggestedName;
        private string _selectedKey;
        private string _suggestedKey;
        private string _suggestedFolder;
        private ComponentModel _componentModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentModelWizardDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="downloadService">The model download service.</param>
        public ComponentModelWizardDialog(Settings settings, IModelDownloadService downloadService)
        {
            Settings = settings;
            _downloadService = downloadService;
            KindOptions =
            [
                new WizardComponentKind { Name = AppDefault.TextEncoder, KeyPrefix = "TE", Folder = "text_encoders" },
                new WizardComponentKind { Name = AppDefault.AutoEncoder, KeyPrefix = "VAE", Folder = "vae" },
                new WizardComponentKind { Name = AppDefault.AudioVae, KeyPrefix = "AVAE", Folder = "vae" },
                new WizardComponentKind { Name = AppDefault.Connectors, KeyPrefix = "CON", Folder = ModelFolders.Components },
                new WizardComponentKind { Name = AppDefault.LatentUpsampler, KeyPrefix = "UPS", Folder = ModelFolders.Components }
            ];
            CheckpointTypes = [CheckpointType.LocalFile, CheckpointType.LocalFolder, CheckpointType.OnlineFile, CheckpointType.OnlineFolder];
            Checkpoint = new CheckpointComponent { Name = "Component", Type = CheckpointType.LocalFile };
            Checkpoint.PropertyChanged += Checkpoint_PropertyChanged;
            Errors = new ObservableCollection<string>();
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            FileFilter = DownloadLink.GetFileFilter(AppDefault.ModelFiles, _fileExtensions);
            SelectedKind = KindOptions[0];
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public WizardComponentKind[] KindOptions { get; }
        public CheckpointType[] CheckpointTypes { get; }
        public CheckpointComponent Checkpoint { get; }
        public string FileFilter { get; }

        public ComponentModel ComponentModel
        {
            get { return _componentModel; }
            private set { SetProperty(ref _componentModel, value); }
        }

        public WizardComponentKind SelectedKind
        {
            get { return _selectedKind; }
            set { SetProperty(ref _selectedKind, value); SuggestKey(); SuggestFolder(); }
        }

        public string SelectedName
        {
            get { return _selectedName; }
            set { SetProperty(ref _selectedName, value); SuggestKey(); SuggestFolder(); }
        }

        public string SelectedKey
        {
            get { return _selectedKey; }
            set { SetProperty(ref _selectedKey, value); }
        }


        /// <summary>
        /// Shows the wizard.
        /// </summary>
        public Task<bool> AddAsync()
        {
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Adds the component from the selected kind, source, name and key.
        /// </summary>
        protected override async Task SaveAsync()
        {
            ComponentModel = new ComponentModel
            {
                Id = Settings.Components.NextId(),
                Name = _selectedName.Trim(),
                Key = _selectedKey.Trim().ToUpperInvariant(),
                Status = IsOnline() ? ModelStatusType.Available : ModelStatusType.Unknown,
                Checkpoint = CreateCheckpoint()
            };

            Settings.Components.Add(ComponentModel);
            ComponentModel.Initialize(Settings);
            await Settings.SaveAsync();
            await _downloadService.PromptDownloadAsync(ComponentModel);
            await base.SaveAsync();
        }


        /// <summary>
        /// Determines whether the component can be saved, listing the validation errors.
        /// </summary>
        protected override bool CanExecuteSave()
        {
            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        /// <summary>
        /// Cancels the dialog.
        /// </summary>
        protected override Task CancelAsync()
        {
            return base.CancelAsync();
        }


        /// <summary>
        /// Handles a change of the source, a new source type starts with an empty path and no main file, and the name is filled from the source.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void Checkpoint_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CheckpointComponent.Type))
            {
                _suggestedFolder = null;
                Checkpoint.MainFile = null;
                Checkpoint.Path = null;
                SuggestFolder();
            }
            else if (e.PropertyName == nameof(CheckpointComponent.Path) || e.PropertyName == nameof(CheckpointComponent.DownloadFiles))
            {
                SuggestName(GetSourceName());
            }
        }


        /// <summary>
        /// Determines whether the source is a download.
        /// </summary>
        private bool IsOnline()
        {
            return Checkpoint.Type == CheckpointType.OnlineFile || Checkpoint.Type == CheckpointType.OnlineFolder;
        }


        /// <summary>
        /// Gets the download links of the source, each as its direct download link.
        /// </summary>
        private string[] GetDownloadLinks()
        {
            return [.. (Checkpoint.DownloadFiles ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(DownloadLink.GetDownloadLink)];
        }


        /// <summary>
        /// Creates the checkpoint of the source, a download goes in the folder of the selected kind and a file is the main file of its folder.
        /// </summary>
        private CheckpointComponent CreateCheckpoint()
        {
            if (Checkpoint.Type == CheckpointType.LocalFile)
            {
                return new CheckpointComponent
                {
                    Name = "Component",
                    Type = CheckpointType.LocalFile,
                    Path = Checkpoint.Path,
                    MainFile = Path.GetFileName(Checkpoint.Path)
                };
            }

            if (Checkpoint.Type == CheckpointType.LocalFolder)
            {
                return new CheckpointComponent
                {
                    Name = "Component",
                    Type = CheckpointType.LocalFolder,
                    Path = Checkpoint.Path,
                    MainFile = Checkpoint.MainFile
                };
            }

            var downloadLinks = GetDownloadLinks();
            var folder = DownloadLink.GetFolderName(Checkpoint.Path);
            return new CheckpointComponent
            {
                Name = "Component",
                Type = CheckpointType.OnlineFolder,
                Folder = string.IsNullOrWhiteSpace(Checkpoint.Folder) ? _selectedKind.Folder : Checkpoint.Folder.Trim(),
                Path = string.IsNullOrEmpty(folder) ? null : folder,
                MainFile = Checkpoint.Type == CheckpointType.OnlineFile ? DownloadLink.GetLocalFileName(downloadLinks[0]) : Checkpoint.MainFile,
                DownloadFiles = downloadLinks
            };
        }


        /// <summary>
        /// Gets the name the source suggests, the file or folder name of a local source and the file name of the first download link.
        /// </summary>
        private string GetSourceName()
        {
            if (IsOnline())
                return DownloadLink.GetFileName(GetDownloadLinks().FirstOrDefault(), _fileExtensions);

            return Path.GetFileName(Checkpoint.Path);
        }


        /// <summary>
        /// Fills the name from a file name, unless the user has typed a name of their own.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        private void SuggestName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return;

            if (!string.IsNullOrEmpty(_selectedName) && _selectedName != _suggestedName)
                return;

            _suggestedName = Path.GetFileNameWithoutExtension(fileName);
            SelectedName = _suggestedName;
        }


        /// <summary>
        /// Fills the key from the kind prefix and the letters, digits and underscores of the name, unless the user has typed a key of their own.
        /// </summary>
        private void SuggestKey()
        {
            if (_selectedKind == null)
                return;

            if (!string.IsNullOrEmpty(_selectedKey) && _selectedKey != _suggestedKey)
                return;

            var name = new string([.. (_selectedName ?? string.Empty).Select(x => char.IsLetterOrDigit(x) ? x : '_')]).Trim('_');
            _suggestedKey = string.IsNullOrEmpty(name) ? null : $"{_selectedKind.KeyPrefix}_{name}".ToUpperInvariant();
            SelectedKey = _suggestedKey;
        }


        /// <summary>
        /// Fills the download folder from the selected kind and the sub folder from the name, unless the user has typed a sub folder of their own.
        /// </summary>
        private void SuggestFolder()
        {
            if (_selectedKind == null || Checkpoint == null || !IsOnline())
                return;

            if (string.IsNullOrEmpty(Checkpoint.Folder) || KindOptions.Any(x => x.Folder == Checkpoint.Folder))
                Checkpoint.Folder = _selectedKind.Folder;

            if (!string.IsNullOrEmpty(Checkpoint.Path) && Checkpoint.Path != _suggestedFolder)
                return;

            _suggestedFolder = DownloadLink.GetFolderName(_selectedName);
            Checkpoint.Path = _suggestedFolder;
        }


        /// <summary>
        /// Gets the validation errors of the current input.
        /// </summary>
        private IEnumerable<string> GetValidationErrors()
        {
            if (_selectedKind is null)
                yield return AppErrors.ValidationTypeEmpty;

            if (string.IsNullOrWhiteSpace(_selectedName))
                yield return AppErrors.ValidationModelNameEmpty;
            else if (Settings.Components.Any(x => x.Name.Equals(_selectedName.Trim(), StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationModelNameExists, _selectedName.Trim());

            if (string.IsNullOrWhiteSpace(_selectedKey))
                yield return AppErrors.ValidationKeyEmpty;
            else if (Settings.Components.Any(x => x.Key.Equals(_selectedKey.Trim(), StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationKeyExists, _selectedKey.Trim());

            if (Checkpoint.Type == CheckpointType.OnlineFile)
            {
                var downloadLinks = GetDownloadLinks();
                if (downloadLinks.Length == 0)
                    yield return AppErrors.ValidationDownloadLinkEmpty;
                else if (downloadLinks.Length > 1 || DownloadLink.GetFileName(downloadLinks[0], _fileExtensions) == null)
                    yield return string.Format(AppErrors.ValidationDownloadLinkInvalid, string.Join(", ", _fileExtensions));
            }
            else if (Checkpoint.Type == CheckpointType.OnlineFolder)
            {
                var downloadLinks = GetDownloadLinks();
                if (downloadLinks.Length == 0)
                    yield return AppErrors.ValidationDownloadLinkEmpty;
                else if (downloadLinks.Any(x => string.IsNullOrEmpty(DownloadLink.GetLocalFileName(x))))
                    yield return AppErrors.CheckpointNoDownloadFiles;
            }
            else if (Checkpoint.Type == CheckpointType.LocalFolder)
            {
                if (string.IsNullOrEmpty(Checkpoint.Path))
                    yield return AppErrors.ValidationModelFolderNameEmpty;
                else if (!Directory.Exists(Checkpoint.Path))
                    yield return AppErrors.ValidationModelFolderNotFound;
            }
            else
            {
                if (string.IsNullOrEmpty(Checkpoint.Path))
                    yield return AppErrors.ValidationModelFileNameEmpty;
                else if (!File.Exists(Checkpoint.Path))
                    yield return AppErrors.ValidationModelFileNotFound;
                else if (!DownloadLink.IsSupportedFile(Checkpoint.Path, _fileExtensions))
                    yield return string.Format(AppErrors.ValidationModelFileType, string.Join(", ", _fileExtensions));
            }

            var isFolder = Checkpoint.Type == CheckpointType.OnlineFolder || Checkpoint.Type == CheckpointType.LocalFolder;
            if (isFolder && string.IsNullOrEmpty(Checkpoint.MainFile))
                yield return AppErrors.ValidationMainFileEmpty;
        }
    }
}
