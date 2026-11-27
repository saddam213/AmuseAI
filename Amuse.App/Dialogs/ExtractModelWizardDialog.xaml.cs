// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for ExtractModelWizardDialog.xaml
    /// </summary>
    public partial class ExtractModelWizardDialog : DialogControl
    {
        private static readonly string[] _fileExtensions = [".onnx"];

        private readonly IModelDownloadService _downloadService;
        private ExtractModel _selectedTemplate;
        private CheckpointType _selectedSource;
        private string _selectedFile;
        private string _selectedLink;
        private string _selectedName;
        private string _suggestedName;
        private ExtractModel _extractModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtractModelWizardDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="downloadService">The model download service.</param>
        public ExtractModelWizardDialog(Settings settings, IModelDownloadService downloadService)
        {
            Settings = settings;
            _downloadService = downloadService;
            TemplateOptions = new ObservableCollection<ExtractModel>();
            Errors = new ObservableCollection<string>();
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            FileFilter = DownloadLink.GetFileFilter(AppDefault.ModelFiles, _fileExtensions);
            SelectedSource = CheckpointType.LocalFile;
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public ObservableCollection<ExtractModel> TemplateOptions { get; }
        public string FileFilter { get; }

        public ExtractModel ExtractModel
        {
            get { return _extractModel; }
            private set { SetProperty(ref _extractModel, value); }
        }

        public ExtractModel SelectedTemplate
        {
            get { return _selectedTemplate; }
            set { SetProperty(ref _selectedTemplate, value); }
        }

        public CheckpointType SelectedSource
        {
            get { return _selectedSource; }
            set
            {
                SetProperty(ref _selectedSource, value);
                NotifyPropertyChanged(nameof(IsLocalFile));
                NotifyPropertyChanged(nameof(IsOnlineFile));
                SuggestName(GetSourceFile());
            }
        }

        public bool IsLocalFile
        {
            get { return _selectedSource == CheckpointType.LocalFile; }
            set { if (value) SelectedSource = CheckpointType.LocalFile; }
        }

        public bool IsOnlineFile
        {
            get { return _selectedSource == CheckpointType.OnlineFile; }
            set { if (value) SelectedSource = CheckpointType.OnlineFile; }
        }

        public string SelectedFile
        {
            get { return _selectedFile; }
            set { SetProperty(ref _selectedFile, value); SuggestName(value); }
        }

        public string SelectedLink
        {
            get { return _selectedLink; }
            set { SetProperty(ref _selectedLink, value); SuggestName(DownloadLink.GetFileName(value, _fileExtensions)); }
        }

        public string SelectedName
        {
            get { return _selectedName; }
            set { SetProperty(ref _selectedName, value); }
        }


        /// <summary>
        /// Shows the wizard with the built-in extractors to take the settings from, selecting the first one.
        /// </summary>
        public Task<bool> AddAsync()
        {
            TemplateOptions.Clear();
            foreach (var template in Settings.ExtractModels.Where(x => x.Id <= Utils.FixedIdRange))
                TemplateOptions.Add(template);

            SelectedTemplate = TemplateOptions.FirstOrDefault();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Adds the extractor from the selected file and name, the other settings come from the selected built-in extractor.
        /// </summary>
        protected override async Task SaveAsync()
        {
            var model = _selectedTemplate.DeepClone(Settings.ExtractModels.NextId());
            model.Name = _selectedName.Trim();
            model.Status = _selectedSource == CheckpointType.OnlineFile ? ModelStatusType.Available : ModelStatusType.Unknown;
            model.Checkpoint = CreateCheckpoint();
            model.Link = null;
            model.Metadata = null;
            ExtractModel = model;

            Settings.ExtractModels.Add(ExtractModel);
            ExtractModel.Initialize(Settings);
            await Settings.SaveAsync();
            await _downloadService.PromptDownloadAsync(ExtractModel);
            await base.SaveAsync();
        }


        /// <summary>
        /// Determines whether the extractor can be saved, listing the validation errors.
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
        /// Creates the checkpoint of the selected source.
        /// </summary>
        private CheckpointComponent CreateCheckpoint()
        {
            if (_selectedSource == CheckpointType.OnlineFile)
            {
                return new CheckpointComponent
                {
                    Name = "Extract",
                    Type = CheckpointType.OnlineFile,
                    DownloadFiles = [DownloadLink.GetDownloadLink(_selectedLink)]
                };
            }

            return new CheckpointComponent
            {
                Name = "Extract",
                Type = CheckpointType.LocalFile,
                Path = _selectedFile
            };
        }


        /// <summary>
        /// Gets the file of the selected source.
        /// </summary>
        private string GetSourceFile()
        {
            return _selectedSource == CheckpointType.OnlineFile ? DownloadLink.GetFileName(_selectedLink, _fileExtensions) : _selectedFile;
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
        /// Gets the validation errors of the current input.
        /// </summary>
        private IEnumerable<string> GetValidationErrors()
        {
            if (_selectedTemplate is null)
                yield return AppErrors.ValidationTypeEmpty;

            if (string.IsNullOrWhiteSpace(_selectedName))
                yield return AppErrors.ValidationModelNameEmpty;
            else if (Settings.ExtractModels.Any(x => x.Name.Equals(_selectedName.Trim(), StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationModelNameExists, _selectedName.Trim());

            if (_selectedSource == CheckpointType.OnlineFile)
            {
                if (string.IsNullOrWhiteSpace(_selectedLink))
                    yield return AppErrors.ValidationDownloadLinkEmpty;
                else if (DownloadLink.GetFileName(_selectedLink, _fileExtensions) == null)
                    yield return string.Format(AppErrors.ValidationDownloadLinkInvalid, string.Join(", ", _fileExtensions));
            }
            else
            {
                if (string.IsNullOrEmpty(_selectedFile))
                    yield return AppErrors.ValidationModelFileNameEmpty;
                else if (!File.Exists(_selectedFile))
                    yield return AppErrors.ValidationModelFileNotFound;
                else if (!DownloadLink.IsSupportedFile(_selectedFile, _fileExtensions))
                    yield return string.Format(AppErrors.ValidationModelFileType, string.Join(", ", _fileExtensions));
            }
        }
    }
}
