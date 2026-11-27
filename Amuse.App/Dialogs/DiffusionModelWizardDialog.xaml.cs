// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for DiffusionModelWizardDialog.xaml
    /// </summary>
    public partial class DiffusionModelWizardDialog : DialogControl
    {
        private readonly IModelDownloadService _downloadService;
        private BackendType? _selectedBackend;
        private WizardItemModel _selectedPipelineOption;
        private WizardOptionModel _selectedTemplateOption;
        private DiffusionModel _selectedTemplate;
        private ModelSourceType _selectedSource;
        private CheckpointComponent _mainComponent;
        private bool _hasSharedComponents;
        private string _selectedFile;
        private string _selectedName;
        private string _fileFilter;
        private DataType[] _checkpointTypes;
        private DataType? _selectedCheckpointType;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiffusionModelWizardDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="downloadService">The model download service.</param>
        public DiffusionModelWizardDialog(Settings settings, IModelDownloadService downloadService)
        {
            Settings = settings;
            _downloadService = downloadService;
            PipelineOptions = new ObservableCollection<WizardItemModel>();
            RequiredFiles = new ObservableCollection<WizardFileModel>();
            SharedComponents = new ObservableCollection<WizardFileModel>();
            Errors = new ObservableCollection<string>();
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            SelectedSource = ModelSourceType.LocalFile;
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public ObservableCollection<WizardItemModel> PipelineOptions { get; }
        public ObservableCollection<WizardFileModel> RequiredFiles { get; }
        public ObservableCollection<WizardFileModel> SharedComponents { get; }

        public WizardItemModel SelectedPipelineOption
        {
            get { return _selectedPipelineOption; }
            set
            {
                SetProperty(ref _selectedPipelineOption, value);
                SetBackend(_selectedPipelineOption?.Backend);
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

        public DiffusionModel SelectedTemplate
        {
            get { return _selectedTemplate; }
            set { SetProperty(ref _selectedTemplate, value); SetTemplateFiles(); }
        }

        public ModelSourceType SelectedSource
        {
            get { return _selectedSource; }
            set
            {
                SetProperty(ref _selectedSource, value);
                NotifyPropertyChanged(nameof(IsLocalFile));
                NotifyPropertyChanged(nameof(IsLocalCheckpoint));
                NotifyPropertyChanged(nameof(IsCustomCheckpoint));
                SetCheckpointType(GetMainFile());
            }
        }

        public bool IsLocalFile
        {
            get { return _selectedSource == ModelSourceType.LocalFile; }
            set { if (value) SelectedSource = ModelSourceType.LocalFile; }
        }

        public bool IsLocalCheckpoint
        {
            get { return _selectedSource == ModelSourceType.LocalCheckpoint; }
            set { if (value) SelectedSource = ModelSourceType.LocalCheckpoint; }
        }

        public bool IsCustomCheckpoint
        {
            get { return _selectedSource == ModelSourceType.Checkpoint; }
            set { if (value) SelectedSource = ModelSourceType.Checkpoint; }
        }

        public bool HasSharedComponents
        {
            get { return _hasSharedComponents; }
            set { SetProperty(ref _hasSharedComponents, value); }
        }

        public string SelectedFile
        {
            get { return _selectedFile; }
            set { SetProperty(ref _selectedFile, value); OnMainFileChanged(value); }
        }

        public string SelectedName
        {
            get { return _selectedName; }
            set { SetProperty(ref _selectedName, value); }
        }

        /// <summary>
        /// Gets or sets the data types the selected backend can load.
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
        /// Shows the wizard with the pipelines of a media type, and of a process type when one is given.
        /// </summary>
        /// <param name="mediaType">The media type of the models to add.</param>
        /// <param name="processType">The process type the model must support, any when <c>null</c>.</param>
        public Task<bool> AddAsync(MediaType mediaType, ProcessType? processType = null)
        {
            FilterPipelineOptions(mediaType, processType);
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Adds the model from the selected template and files.
        /// </summary>
        protected override async Task SaveAsync()
        {
            _selectedTemplate.Name = _selectedName;
            _selectedTemplate.Status = ModelStatusType.Unknown;
            var memorySize = _selectedTemplate.MemorySize;

            if (_selectedSource == ModelSourceType.LocalCheckpoint)
            {
                if (_selectedTemplate.Checkpoint.Unet != null)
                {
                    _selectedTemplate.Checkpoints =
                    [
                        new DiffusionCheckpointModel
                        {
                            Unet = new CheckpointComponent
                            {
                                Name = "Unet",
                                Path = _selectedFile,
                                Type = CheckpointType.LocalFile,
                            }
                        }
                    ];
                }
                else if (_selectedTemplate.Checkpoint.Transformer != null)
                {
                    _selectedTemplate.Checkpoints =
                    [
                        new DiffusionCheckpointModel
                        {
                            Transformer = new CheckpointComponent
                            {
                                Name = "Transformer",
                                Path = _selectedFile,
                                Type = CheckpointType.LocalFile,
                            }
                        }
                    ];
                }
            }

            _selectedTemplate.Checkpoint.Type = _selectedCheckpointType.Value;
            _selectedTemplate.Checkpoint.MemorySize = memorySize;
            _selectedTemplate.Initialize(Settings);
            Settings.DiffusionModels.Add(_selectedTemplate);
            await Settings.SaveAsync();
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
        /// Closes the dialog, releasing the main file subscription.
        /// </summary>
        protected override async Task CloseAsync()
        {
            SetMainComponent(null);
            await base.CloseAsync();
        }


        /// <summary>
        /// Gets a copy of the template with a new model id.
        /// </summary>
        /// <param name="name">The template name.</param>
        private DiffusionModel GetTemplate(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            var template = Settings.Templates.DiffusionTemplates.FirstOrDefault(x => x.Name == name);
            if (template == null)
                return null;

            var modelId = Settings.DiffusionModels.NextId();
            return template.DeepClone(modelId);
        }


        /// <summary>
        /// Fills the pipelines whose templates match the media type and process type, the backend follows the selected pipeline.
        /// </summary>
        /// <param name="mediaType">The media type.</param>
        /// <param name="processType">The process type, any when <c>null</c>.</param>
        private void FilterPipelineOptions(MediaType mediaType, ProcessType? processType)
        {
            PipelineOptions.Clear();
            foreach (var pipelineOption in Settings.Templates.DiffusionTemplateMap)
            {
                var options = pipelineOption.Options
                    .Where(x => IsTemplateMatch(x.Template, mediaType, processType))
                    .ToArray();
                if (options.Length == 0)
                    continue;

                PipelineOptions.Add(new WizardItemModel
                {
                    Name = pipelineOption.Name,
                    Backend = pipelineOption.Backend,
                    Options = options
                });
            }
            SelectedPipelineOption = PipelineOptions.FirstOrDefault();
        }


        /// <summary>
        /// Determines whether a template has the media type and supports the process type.
        /// </summary>
        /// <param name="name">The template name.</param>
        /// <param name="mediaType">The media type.</param>
        /// <param name="processType">The process type, any when <c>null</c>.</param>
        private bool IsTemplateMatch(string name, MediaType mediaType, ProcessType? processType)
        {
            var template = Settings.Templates.DiffusionTemplates.FirstOrDefault(x => x.Name == name);
            if (template == null || template.MediaType != mediaType)
                return false;

            return !processType.HasValue || template.ProcessTypes.Contains(processType.Value);
        }


        /// <summary>
        /// Sets the backend of the selected pipeline, updating the data types and file filter when it changes.
        /// </summary>
        /// <param name="backend">The backend.</param>
        private void SetBackend(BackendType? backend)
        {
            if (!backend.HasValue || backend == _selectedBackend)
                return;

            _selectedBackend = backend;
            CheckpointTypes = [DataType.Default, .. backend.Value.GetDataTypes()];
            SelectedCheckpointType = null;
            FileFilter = backend == BackendType.OnnxRuntime ? $"{AppDefault.ModelFiles}|*.onnx;" : $"{AppDefault.ModelFiles}|*.safetensors;*.gguf;";
        }


        /// <summary>
        /// Lists the files the selected template needs and the shared components Amuse supplies, and selects the Local File source.
        /// </summary>
        private void SetTemplateFiles()
        {
            RequiredFiles.Clear();
            SharedComponents.Clear();
            var files = GetFileModels(_selectedTemplate?.Checkpoint);
            foreach (var file in files.Where(x => x.Component.Type == CheckpointType.LocalFile || x.Component.Type == CheckpointType.LocalFolder).OrderByDescending(x => x.IsMain))
                RequiredFiles.Add(file);

            foreach (var file in files.Where(x => x.SharedComponent != null))
                SharedComponents.Add(file);

            HasSharedComponents = files.Any(x => !x.IsMain);
            SetMainComponent(RequiredFiles.FirstOrDefault(x => x.IsMain)?.Component);
            SelectedSource = ModelSourceType.LocalFile;
        }


        /// <summary>
        /// Gets the components of a checkpoint with their display names, the Unet or Transformer is the main file.
        /// </summary>
        /// <param name="checkpoint">The checkpoint.</param>
        private List<WizardFileModel> GetFileModels(DiffusionCheckpointModel checkpoint)
        {
            var files = new List<WizardFileModel>();
            if (checkpoint == null)
                return files;

            AddFileModel(files, AppDefault.Compute, checkpoint.Compute, false);
            AddFileModel(files, AppDefault.Tokenizer, checkpoint.Tokenizer, false);
            AddFileModel(files, AppDefault.TextEncoder, checkpoint.TextEncoder, false);
            AddFileModel(files, AppDefault.TextEncoder2, checkpoint.TextEncoder2, false);
            AddFileModel(files, AppDefault.TextEncoder3, checkpoint.TextEncoder3, false);
            AddFileModel(files, AppDefault.Unet, checkpoint.Unet, true);
            AddFileModel(files, AppDefault.Transformer, checkpoint.Transformer, checkpoint.Unet == null);
            AddFileModel(files, AppDefault.Transformer2, checkpoint.Transformer2, false);
            AddFileModel(files, AppDefault.AutoEncoder, checkpoint.Vae, false);
            AddFileModel(files, AppDefault.AudioVae, checkpoint.AudioVae, false);
            AddFileModel(files, AppDefault.Vocoder, checkpoint.Vocoder, false);
            AddFileModel(files, AppDefault.Connectors, checkpoint.Connectors, false);
            AddFileModel(files, AppDefault.LatentUpsampler, checkpoint.LatentUpsampler, false);
            AddFileModel(files, AppDefault.LatentUpsamplerTemporal, checkpoint.LatentUpsamplerTemporal, false);
            AddFileModel(files, AppDefault.ConditionEncoder, checkpoint.ConditionEncoder, false);
            AddFileModel(files, AppDefault.AudioTokenizer, checkpoint.AudioTokenizer, false);
            AddFileModel(files, AppDefault.AudioDetokenizer, checkpoint.AudioDetokenizer, false);
            return files;
        }


        /// <summary>
        /// Adds a component to the list when the checkpoint has it, resolving a shared component from the settings.
        /// </summary>
        /// <param name="files">The list to add to.</param>
        /// <param name="label">The display name.</param>
        /// <param name="component">The component.</param>
        /// <param name="isMain">Whether the component is the main model file.</param>
        private void AddFileModel(List<WizardFileModel> files, string label, CheckpointComponent component, bool isMain)
        {
            if (component == null)
                return;

            files.Add(new WizardFileModel
            {
                Label = label,
                Component = component,
                IsMain = isMain,
                SharedComponent = component.Type == CheckpointType.Component
                    ? Settings.Components?.FirstOrDefault(x => x.Key == component.Path)
                    : null
            });
        }


        /// <summary>
        /// Watches the main component path to fill the model name and data type from it.
        /// </summary>
        /// <param name="component">The main component, none when <c>null</c>.</param>
        private void SetMainComponent(CheckpointComponent component)
        {
            if (_mainComponent != null)
                _mainComponent.PropertyChanged -= OnMainComponentPropertyChanged;

            _mainComponent = component;
            if (_mainComponent != null)
                _mainComponent.PropertyChanged += OnMainComponentPropertyChanged;
        }


        /// <summary>
        /// Called when a property of the main component changes.
        /// </summary>
        /// <param name="sender">The main component.</param>
        /// <param name="e">The event args.</param>
        private void OnMainComponentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CheckpointComponent.Path) && _selectedSource == ModelSourceType.LocalFile)
                OnMainFileChanged(_mainComponent.Path);
        }


        /// <summary>
        /// Fills the model name and data type from the main file.
        /// </summary>
        /// <param name="fileName">The main file name.</param>
        private void OnMainFileChanged(string fileName)
        {
            if (string.IsNullOrEmpty(_selectedName) && !string.IsNullOrEmpty(fileName))
                SelectedName = Path.GetFileNameWithoutExtension(fileName);

            SetCheckpointType(fileName);
        }


        /// <summary>
        /// Gets the main file of the selected source.
        /// </summary>
        private string GetMainFile()
        {
            return _selectedSource switch
            {
                ModelSourceType.LocalFile => _mainComponent?.Path,
                ModelSourceType.LocalCheckpoint => _selectedFile,
                _ => null
            };
        }


        /// <summary>
        /// Selects the data type the model file name suggests, keeping the selection when it does not suggest one the backend can load.
        /// </summary>
        /// <param name="fileName">The model file name.</param>
        private void SetCheckpointType(string fileName)
        {
            var checkpointType = Utils.GetDataType(fileName);
            if (checkpointType.HasValue && _checkpointTypes != null && _checkpointTypes.Contains(checkpointType.Value))
                SelectedCheckpointType = checkpointType;
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
            if (Settings.DiffusionModels.Any(x => x.Name.Equals(_selectedName, StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationModelNameExists, _selectedName);

            if (_selectedSource == ModelSourceType.LocalFile)
            {
                var fileError = RequiredFiles.Select(GetFileError).FirstOrDefault(x => x != null);
                if (fileError != null)
                    yield return fileError;
            }
            else if (_selectedSource == ModelSourceType.LocalCheckpoint)
            {
                if (string.IsNullOrEmpty(_selectedFile))
                    yield return AppErrors.ValidationModelFileNameEmpty;
                else if (!File.Exists(_selectedFile))
                    yield return AppErrors.ValidationModelFileNotFound;
            }
            else if (_selectedSource == ModelSourceType.Checkpoint)
            {
                if (_selectedTemplate?.Checkpoint != null)
                {
                    foreach (var checkpoint in _selectedTemplate.Checkpoint.GetComponents())
                    {
                        if (!checkpoint.IsValid(out var checkpointValidation))
                            yield return $"{checkpoint.Name} {checkpointValidation}";
                        else if (_selectedTemplate.Backend == BackendType.StableDiffusionCpp && IsMainFileMissing(checkpoint))
                            yield return $"{checkpoint.Name} {AppErrors.ValidationMainFileEmpty}";
                    }
                }
            }
        }


        /// <summary>
        /// Gets the validation error of a required file, <c>null</c> when it is valid.
        /// </summary>
        /// <param name="file">The required file.</param>
        private static string GetFileError(WizardFileModel file)
        {
            var path = file.Component.Path;
            if (file.IsFolder)
            {
                if (string.IsNullOrEmpty(path))
                    return $"{file.Label}: {AppErrors.ValidationModelFolderNameEmpty}";
                if (!Directory.Exists(path))
                    return $"{file.Label}: {AppErrors.ValidationModelFolderNotFound}";

                return null;
            }

            if (string.IsNullOrEmpty(path))
                return $"{file.Label}: {AppErrors.ValidationModelFileNameEmpty}";
            if (!File.Exists(path))
                return $"{file.Label}: {AppErrors.ValidationModelFileNotFound}";

            return null;
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
