// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using Amuse.Common;
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
    /// Interaction logic for ControlNetModelWizardDialog.xaml
    /// </summary>
    public partial class ControlNetModelWizardDialog : DialogControl
    {
        private static readonly string[] _fileExtensions = [".safetensors", ".gguf"];

        private readonly IModelDownloadService _downloadService;
        private PipelineType? _selectedPipeline;
        private CheckpointType _selectedSource;
        private string _selectedFile;
        private string _selectedLink;
        private string _selectedName;
        private string _suggestedName;
        private ControlNetModel _controlNetModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="ControlNetModelWizardDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="downloadService">The model download service.</param>
        public ControlNetModelWizardDialog(Settings settings, IModelDownloadService downloadService)
        {
            Settings = settings;
            _downloadService = downloadService;
            PipelineOptions = new ObservableCollection<PipelineType>();
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
        public ObservableCollection<PipelineType> PipelineOptions { get; }
        public string FileFilter { get; }

        public ControlNetModel ControlNetModel
        {
            get { return _controlNetModel; }
            private set { SetProperty(ref _controlNetModel, value); }
        }

        public PipelineType? SelectedPipeline
        {
            get { return _selectedPipeline; }
            set { SetProperty(ref _selectedPipeline, value); }
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
        /// Shows the wizard with the pipelines that support ControlNet, selecting the first one.
        /// </summary>
        public Task<bool> AddAsync()
        {
            SetPipelineOptions(null);
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Shows the wizard with the pipelines that support ControlNet, selecting a pipeline.
        /// </summary>
        /// <param name="pipelineType">The pipeline to select.</param>
        public Task<bool> AddAsync(PipelineType pipelineType)
        {
            SetPipelineOptions(pipelineType);
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Adds the ControlNet from the selected pipeline, file and name.
        /// </summary>
        protected override async Task SaveAsync()
        {
            ControlNetModel = new ControlNetModel
            {
                Id = Settings.ControlNetModels.NextId(),
                Name = _selectedName.Trim(),
                Pipeline = _selectedPipeline.Value,
                Status = _selectedSource == CheckpointType.OnlineFile ? ModelStatusType.Available : ModelStatusType.Unknown,
                Checkpoint = CreateCheckpoint()
            };

            Settings.ControlNetModels.Add(ControlNetModel);
            ControlNetModel.Initialize(Settings);
            await Settings.SaveAsync();
            await _downloadService.PromptDownloadAsync(ControlNetModel);
            await base.SaveAsync();
        }


        /// <summary>
        /// Determines whether the ControlNet can be saved, listing the validation errors.
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
        /// Fills the pipelines that support ControlNet, adding the pipeline to select when it is not one of them.
        /// </summary>
        /// <param name="pipelineType">The pipeline to select, the first when <c>null</c>.</param>
        private void SetPipelineOptions(PipelineType? pipelineType)
        {
            PipelineOptions.Clear();
            foreach (var pipeline in Settings.GetControlNetPipelines())
                PipelineOptions.Add(pipeline);

            if (pipelineType.HasValue && !PipelineOptions.Contains(pipelineType.Value))
                PipelineOptions.Add(pipelineType.Value);

            SelectedPipeline = pipelineType ?? PipelineOptions.Cast<PipelineType?>().FirstOrDefault();
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
                    Name = "ControlNet",
                    Type = CheckpointType.OnlineFile,
                    DownloadFiles = [DownloadLink.GetDownloadLink(_selectedLink)]
                };
            }

            return new CheckpointComponent
            {
                Name = "ControlNet",
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
            if (_selectedPipeline is null)
                yield return AppErrors.ValidationPipelineEmpty;

            if (string.IsNullOrWhiteSpace(_selectedName))
                yield return AppErrors.ValidationModelNameEmpty;
            else if (Settings.ControlNetModels.Any(x => x.Name.Equals(_selectedName.Trim(), StringComparison.OrdinalIgnoreCase)))
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
