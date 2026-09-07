// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
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
        private BackendType _selectedBackend;
        private WizardItemModel _selectedPipelineOption;
        private WizardOptionModel _selectedTemplateOption;
        private LanguageModel _selectedTemplate;
        private ModelSourceType _selectedSource;
        private string _selectedFile;
        private string _selectedPath;
        private string _selectedName;
        private string _selectedVariant;
        private string _fileFilter;

        public LanguageModelWizardDialog(Settings settings)
        {
            Settings = settings;
            PipelineOptions = new ObservableCollection<WizardItemModel>();
            SelectedBackend = BackendType.HuggingFace;
            Backends = [ BackendType.HuggingFace];
            Errors = new ObservableCollection<string>();
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            SelectedSource = ModelSourceType.LocalFolder;
            ModelSources = [ModelSourceType.LocalFolder, ModelSourceType.Checkpoint];
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public ModelSourceType[] ModelSources { get; }
        public ObservableCollection<WizardItemModel> PipelineOptions { get; }
        public BackendType[] Backends { get; }

        public BackendType SelectedBackend
        {
            get { return _selectedBackend; }
            set { SetProperty(ref _selectedBackend, value); FilterPipelineOptions(); SetFileFilter(); }
        }

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
                Reset();
                SetProperty(ref _selectedTemplateOption, value);
                SelectedTemplate = GetTemplate(_selectedTemplateOption?.Template);
            }
        }

        public LanguageModel SelectedTemplate
        {
            get { return _selectedTemplate; }
            set { SetProperty(ref _selectedTemplate, value); }
        }

        public ModelSourceType SelectedSource
        {
            get { return _selectedSource; }
            set { SetProperty(ref _selectedSource, value); }
        }

        public string SelectedFile
        {
            get { return _selectedFile; }
            set { SetProperty(ref _selectedFile, value); SetModelName(); }
        }

        public string SelectedPath
        {
            get { return _selectedPath; }
            set { SetProperty(ref _selectedPath, value); SetModelName(); }
        }

        public string SelectedName
        {
            get { return _selectedName; }
            set { SetProperty(ref _selectedName, value); }
        }

        public string SelectedVariant
        {
            get { return _selectedVariant; }
            set { SetProperty(ref _selectedVariant, value); }
        }

        public string FileFilter
        {
            get { return _fileFilter; }
            set { SetProperty(ref _fileFilter, value); }
        }


        protected override Task SaveAsync()
        {
            _selectedTemplate.Name = _selectedName;
            _selectedTemplate.Variant = _selectedVariant;

            if (_selectedSource == ModelSourceType.LocalFolder)
            {
                if (_selectedTemplate.Checkpoint.TextModel != null)
                {
                    _selectedTemplate.Checkpoint.TextModel = new CheckpointComponent
                    {
                        Name = "TextModel",
                        Path = _selectedPath,
                        Type = CheckpointType.LocalFolder,
                    };
                }
            }

            _selectedTemplate.ModelParams = _selectedTemplateOption.ModelParams;
            _selectedTemplate.DefaultOptions.MaxLength = _selectedTemplateOption.ModelMaxLength;
            _selectedTemplate.MemoryProfile = Utils.GetMemoryProfile(_selectedTemplate.ModelParams);
            _selectedTemplate.Initialize(Settings);
            Settings.LanguageModels.Add(_selectedTemplate);
            return base.SaveAsync();
        }


        protected override bool CanExecuteSave()
        {
            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        protected override Task CancelAsync()
        {
            return base.CancelAsync();
        }


        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        private void Reset()
        {
            SelectedSource = ModelSourceType.LocalFolder;
        }


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


        private void SetModelName()
        {
            if (string.IsNullOrEmpty(_selectedPath))
                return;

            if (_selectedSource == ModelSourceType.LocalFolder)
            {
                SelectedName = Path.GetFileName(_selectedPath);
            }
            else if (_selectedSource == ModelSourceType.LocalFile)
            {
                SelectedName = Path.GetFileNameWithoutExtension(_selectedPath);
            }
        }


        private void SetFileFilter()
        {
            FileFilter = _selectedBackend == BackendType.OnnxRuntime ? "Model Files|*.onnx;" : "Model Files|*.safetensors;*.gguf;";
        }


        private void FilterPipelineOptions()
        {
            PipelineOptions.Clear();
            foreach (var pipelineOption in Settings.Templates.LanguageTemplateMap.Where(x => x.Backend == _selectedBackend))
            {
                PipelineOptions.Add(pipelineOption);
            }
            SelectedPipelineOption = PipelineOptions.FirstOrDefault();
        }


        private IEnumerable<string> GetValidationErrors()
        {
            if (_selectedPipelineOption == null)
                yield return "Pipeline cannot be empty";

            if (_selectedTemplateOption == null)
                yield return "Type cannot be empty";

            // Name
            if (string.IsNullOrWhiteSpace(_selectedName))
                yield return "Model name cannot be empty";
            if (Settings.LanguageModels.Any(x => x.Name.Equals(_selectedName, StringComparison.OrdinalIgnoreCase)))
                yield return $"Model with name '{_selectedName}' already exists";

            if (_selectedSource == ModelSourceType.LocalFolder)
            {
                if (string.IsNullOrEmpty(_selectedPath))
                    yield return "Model folder name cannot be empty";
            }
            else if (_selectedSource == ModelSourceType.Checkpoint)
            {
                // Checkpoint
                if (_selectedTemplate?.Checkpoint != null)
                {
                    foreach (var checkpoint in _selectedTemplate.Checkpoint.GetComponents())
                    {
                        if (!checkpoint.IsValid(out var checkpointValidation))
                            yield return $"{checkpoint.Name} {checkpointValidation}";
                    }
                }
            }
        }
    }
}
