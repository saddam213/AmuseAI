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
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for LanguageModelWizardDialog.xaml
    /// </summary>
    public partial class LanguageModelWizardDialog : DialogControl
    {
        private readonly IModelDownloadService _downloadService;
        private readonly BackendType _backend;
        private WizardItemModel _selectedPipelineOption;
        private WizardOptionModel _selectedTemplateOption;
        private LanguageModel _selectedTemplate;
        private ModelSourceType _selectedSource;
        private bool _hasVisionProjector;
        private string _selectedFile;
        private string _selectedProjectorFile;
        private string _selectedName;
        private string _fileFilter;
        private DataType[] _checkpointTypes;
        private DataType? _selectedCheckpointType;

        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageModelWizardDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="downloadService">The model download service.</param>
        public LanguageModelWizardDialog(Settings settings, IModelDownloadService downloadService)
        {
            Settings = settings;
            _downloadService = downloadService;
            _backend = BackendType.LlamaCpp;
            PipelineOptions = new ObservableCollection<WizardItemModel>();
            Errors = new ObservableCollection<string>();
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CheckpointTypes = [DataType.Default, .. _backend.GetDataTypes()];
            FileFilter = $"{AppDefault.ModelFiles}|*.gguf;";
            FilterPipelineOptions();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public ObservableCollection<WizardItemModel> PipelineOptions { get; }

        public WizardItemModel SelectedPipelineOption
        {
            get { return _selectedPipelineOption; }
            set
            {
                SetProperty(ref _selectedPipelineOption, value);
                SelectedTemplateOption = _selectedPipelineOption?.Options?.FirstOrDefault();
            }
        }

        public WizardOptionModel SelectedTemplateOption
        {
            get { return _selectedTemplateOption; }
            set
            {
                SetProperty(ref _selectedTemplateOption, value);
                SelectedTemplate = GetTemplate(_selectedTemplateOption?.Template);
            }
        }

        public LanguageModel SelectedTemplate
        {
            get { return _selectedTemplate; }
            set
            {
                SetProperty(ref _selectedTemplate, value);
                HasVisionProjector = _selectedTemplate?.Checkpoint?.TextModel2 != null;
                SelectedSource = ModelSourceType.LocalFile;
            }
        }

        public ModelSourceType SelectedSource
        {
            get { return _selectedSource; }
            set
            {
                SetProperty(ref _selectedSource, value);
                NotifyPropertyChanged(nameof(IsLocalFile));
                NotifyPropertyChanged(nameof(IsLocalFileVision));
                NotifyPropertyChanged(nameof(IsCustomCheckpoint));
                NotifyPropertyChanged(nameof(IsModelFileVisible));
            }
        }

        public bool IsLocalFile
        {
            get { return _selectedSource == ModelSourceType.LocalFile; }
            set { if (value) SelectedSource = ModelSourceType.LocalFile; }
        }

        public bool IsLocalFileVision
        {
            get { return _selectedSource == ModelSourceType.LocalFileVision; }
            set { if (value) SelectedSource = ModelSourceType.LocalFileVision; }
        }

        public bool IsCustomCheckpoint
        {
            get { return _selectedSource == ModelSourceType.Checkpoint; }
            set { if (value) SelectedSource = ModelSourceType.Checkpoint; }
        }

        public bool IsModelFileVisible
        {
            get { return _selectedSource == ModelSourceType.LocalFile || _selectedSource == ModelSourceType.LocalFileVision; }
        }

        public bool HasVisionProjector
        {
            get { return _hasVisionProjector; }
            set { SetProperty(ref _hasVisionProjector, value); }
        }

        public string SelectedFile
        {
            get { return _selectedFile; }
            set { SetProperty(ref _selectedFile, value); SetModelName(); SetCheckpointType(value); }
        }

        public string SelectedProjectorFile
        {
            get { return _selectedProjectorFile; }
            set { SetProperty(ref _selectedProjectorFile, value); }
        }

        public string SelectedName
        {
            get { return _selectedName; }
            set { SetProperty(ref _selectedName, value); }
        }

        /// <summary>
        /// Gets or sets the data types the backend can load.
        /// </summary>
        public DataType[] CheckpointTypes
        {
            get { return _checkpointTypes; }
            set { SetProperty(ref _checkpointTypes, value); }
        }

        public DataType? SelectedCheckpointType
        {
            get { return _selectedCheckpointType; }
            set { SetProperty(ref _selectedCheckpointType, value); }
        }

        public string FileFilter
        {
            get { return _fileFilter; }
            set { SetProperty(ref _fileFilter, value); }
        }


        /// <summary>
        /// Adds the model from the selected template and files.
        /// </summary>
        protected override async Task SaveAsync()
        {
            _selectedTemplate.Name = _selectedName;
            _selectedTemplate.Status = ModelStatusType.Unknown;

            if (_selectedSource == ModelSourceType.LocalFile)
            {
                _selectedTemplate.ModelType = "Base";
                _selectedTemplate.ProcessTypes = [ProcessType.TextToText];
                _selectedTemplate.Checkpoint.TextModel2 = null;
                _selectedTemplate.Checkpoint.TextModel = new CheckpointComponent
                {
                    Name = "TextModel",
                    Path = _selectedFile,
                    Type = CheckpointType.LocalFile,
                };
            }
            else if (_selectedSource == ModelSourceType.LocalFileVision)
            {
                _selectedTemplate.Checkpoint.TextModel = new CheckpointComponent
                {
                    Name = "TextModel",
                    Path = _selectedFile,
                    Type = CheckpointType.LocalFile,
                };
                _selectedTemplate.Checkpoint.TextModel2 = new CheckpointComponent
                {
                    Name = "TextModel2",
                    Path = _selectedProjectorFile,
                    Type = CheckpointType.LocalFile,
                };
            }

            _selectedTemplate.ModelParams = _selectedTemplateOption.ModelParams;
            _selectedTemplate.DefaultOptions.MaxLength = _selectedTemplateOption.ModelMaxLength;
            _selectedTemplate.Checkpoint.Type = _selectedCheckpointType.Value;
            _selectedTemplate.Checkpoint.MemorySize = Utils.GetMemorySize(_selectedTemplate.ModelParams, _selectedCheckpointType.Value);
            _selectedTemplate.Initialize(Settings);
            Settings.LanguageModels.Add(_selectedTemplate);
            await _downloadService.PromptDownloadAsync(_selectedTemplate);
            await base.SaveAsync();
        }


        /// <summary>
        /// Determines whether the model can be saved, listing the validation errors.
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
        /// Closes the dialog.
        /// </summary>
        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        /// <summary>
        /// Gets a copy of the template with a new model id.
        /// </summary>
        /// <param name="name">The template name.</param>
        private LanguageModel GetTemplate(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            var template = Settings.Templates.LanguageTemplates.FirstOrDefault(x => x.Name == name);
            if (template == null)
                return null;

            var modelId = Settings.LanguageModels.NextId();
            return template.DeepClone(modelId);
        }


        /// <summary>
        /// Fills the model name from the model file when no name is set.
        /// </summary>
        private void SetModelName()
        {
            if (!string.IsNullOrEmpty(_selectedName) || string.IsNullOrEmpty(_selectedFile))
                return;

            SelectedName = Path.GetFileNameWithoutExtension(_selectedFile);
        }


        /// <summary>
        /// Selects the data type the model file name suggests, keeping the selection when it does not suggest one the backend can load.
        /// </summary>
        /// <param name="fileName">The model file name.</param>
        private void SetCheckpointType(string fileName)
        {
            var checkpointType = Utils.GetDataType(fileName);
            if (checkpointType.HasValue && _checkpointTypes.Contains(checkpointType.Value))
                SelectedCheckpointType = checkpointType;
        }


        /// <summary>
        /// Fills the pipelines of the backend.
        /// </summary>
        private void FilterPipelineOptions()
        {
            PipelineOptions.Clear();
            foreach (var pipelineOption in Settings.Templates.LanguageTemplateMap.Where(x => x.Backend == _backend))
            {
                PipelineOptions.Add(pipelineOption);
            }
            SelectedPipelineOption = PipelineOptions.FirstOrDefault();
        }


        /// <summary>
        /// Gets the validation errors of the current input.
        /// </summary>
        private IEnumerable<string> GetValidationErrors()
        {
            if (_selectedPipelineOption == null)
                yield return AppErrors.ValidationPipelineEmpty;

            if (_selectedTemplateOption == null)
                yield return AppErrors.ValidationTypeEmpty;

            if (_selectedCheckpointType is null)
                yield return AppErrors.ValidationQuantTypeEmpty;

            if (string.IsNullOrWhiteSpace(_selectedName))
                yield return AppErrors.ValidationModelNameEmpty;
            if (Settings.LanguageModels.Any(x => x.Name.Equals(_selectedName, StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationModelNameExists, _selectedName);

            if (_selectedSource == ModelSourceType.LocalFile || _selectedSource == ModelSourceType.LocalFileVision)
            {
                if (string.IsNullOrEmpty(_selectedFile))
                    yield return AppErrors.ValidationModelFileNameEmpty;
                else if (!File.Exists(_selectedFile))
                    yield return AppErrors.ValidationModelFileNotFound;
            }

            if (_selectedSource == ModelSourceType.LocalFileVision)
            {
                if (string.IsNullOrEmpty(_selectedProjectorFile))
                    yield return $"{AppDefault.VisionProjector}: {AppErrors.ValidationModelFileNameEmpty}";
                else if (!File.Exists(_selectedProjectorFile))
                    yield return $"{AppDefault.VisionProjector}: {AppErrors.ValidationModelFileNotFound}";
            }
            else if (_selectedSource == ModelSourceType.Checkpoint)
            {
                if (_selectedTemplate?.Checkpoint != null)
                {
                    foreach (var checkpoint in _selectedTemplate.Checkpoint.GetComponents())
                    {
                        if (!checkpoint.IsValid(out var checkpointValidation))
                            yield return $"{checkpoint.Name} {checkpointValidation}";
                        else if (_selectedTemplate.Backend == BackendType.LlamaCpp && IsMainFileMissing(checkpoint))
                            yield return $"{checkpoint.Name} {AppErrors.ValidationMainFileEmpty}";
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether a folder checkpoint has no main file, the file a backend that loads files is given.
        /// </summary>
        /// <param name="checkpoint">The checkpoint.</param>
        private static bool IsMainFileMissing(CheckpointComponent checkpoint)
        {
            var isFolder = checkpoint.Type == CheckpointType.OnlineFolder || checkpoint.Type == CheckpointType.LocalFolder;
            return isFolder && string.IsNullOrEmpty(checkpoint.MainFile);
        }
    }
}
