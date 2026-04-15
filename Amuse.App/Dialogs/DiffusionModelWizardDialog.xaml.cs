// Copyright (c) TensorStack. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common.Common;
using TensorStack.Python.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for DiffusionModelWizardDialog.xaml
    /// </summary>
    public partial class DiffusionModelWizardDialog : DialogControl
    {
        private WizardOptionModel _selectedOption;
        private string _selectedName;
        private string _selectedModelPath;
        private ModelSourceType _selectedSource;
        private DataType _selectedDataType;
        private WizardItemModel _selectedItem;
        private DiffusionCheckpointModel _checkpointModel;
        private DiffusionModel _selectedTemplate;
        private string _selectedVariant;
        private BackendType _selectedbackend = BackendType.Pytorch;

        public DiffusionModelWizardDialog(Settings settings)
        {
            Settings = settings;
            Templates = settings.Templates.DiffusionTemplates;
            Items = settings.Templates.DiffusionTemplateMap;
            Errors = new ObservableCollection<string>();
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            SelectedItem = Items[0];
            SelectedDataType = DataType.Bfloat16;
            SelectedSource = ModelSourceType.HuggingFace;
            CheckpointModel = new DiffusionCheckpointModel();
            CheckpointModel.PropertyChanged += (s, e) => GenerateName();
            ModelSources = [ModelSourceType.HuggingFace, ModelSourceType.Folder, ModelSourceType.SingleFile, ModelSourceType.Checkpoint];
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public List<DiffusionModel> Templates { get; }
        public List<WizardItemModel> Items { get; }
        public ModelSourceType[] ModelSources { get; }
        public DiffusionModel SelectedTemplate =>  _selectedTemplate;
        public WizardItemModel SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                SetProperty(ref _selectedItem, value);
                Reset();
                SelectedOption = _selectedItem.Options?.FirstOrDefault();
            }
        }

        public WizardOptionModel SelectedOption
        {
            get { return _selectedOption; }
            set
            {
                SetProperty(ref _selectedOption, value);
                _selectedTemplate = Templates.FirstOrDefault(x => x.Name == _selectedOption?.Template);
            }
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

        public ModelSourceType SelectedSource
        {
            get { return _selectedSource; }
            set
            {
                SetProperty(ref _selectedSource, value);
                if (_selectedSource == ModelSourceType.Checkpoint || _selectedSource == ModelSourceType.SingleFile)
                    SelectedModelPath = _selectedTemplate?.Path;

                GenerateName();
            }
        }

        public DataType SelectedDataType
        {
            get { return _selectedDataType; }
            set { SetProperty(ref _selectedDataType, value); }
        }

        public string SelectedModelPath
        {
            get { return _selectedModelPath; }
            set { SetProperty(ref _selectedModelPath, value); GenerateName(); }
        }

        public DiffusionCheckpointModel CheckpointModel
        {
            get { return _checkpointModel; }
            set { SetProperty(ref _checkpointModel, value); }
        }

        public BackendType Selectedbackend
        {
            get { return _selectedbackend; }
            set { SetProperty(ref _selectedbackend, value); }
        }


        protected override Task SaveAsync()
        {
            _selectedTemplate.Id = GetNextModelId();
            _selectedTemplate.Name = SelectedName;
            _selectedTemplate.Source = _selectedSource;
            _selectedTemplate.Path = _selectedModelPath;
            _selectedTemplate.Variant = SelectedVariant;
            _selectedTemplate.Backend = _selectedbackend;
            if ((_selectedSource == ModelSourceType.HuggingFace || _selectedSource == ModelSourceType.Checkpoint) && Utils.TryParseHuggingFaceRepo(_selectedModelPath, out var huggingfacePath))
                _selectedTemplate.Path = huggingfacePath;

            if (_selectedSource == ModelSourceType.SingleFile)
            {
                _checkpointModel.TextEncoder = null;
                _checkpointModel.TextEncoder2 = null;
                _checkpointModel.TextEncoder3 = null;
                _checkpointModel.Transformer = null;
                _checkpointModel.Transformer2 = null;
                _checkpointModel.Vae = null;
                _checkpointModel.AudioVae = null;
                _checkpointModel.Vocoder = null;
                _checkpointModel.Connectors = null;
                _selectedTemplate.Checkpoint = _checkpointModel;
            }
            if (_selectedSource == ModelSourceType.Checkpoint)
            {
                _checkpointModel.SingleFile = null;
                _selectedTemplate.Checkpoint = _checkpointModel;
            }

            _selectedTemplate.Initialize(Settings.DirectoryModel);
            Settings.DiffusionModels.Add(_selectedTemplate);
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


        private IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(_selectedName))
                yield return "Name cannot be empty";
            if (!string.IsNullOrWhiteSpace(_selectedName) && Settings.DiffusionModels.Any(x => x.Name.Equals(_selectedName, StringComparison.OrdinalIgnoreCase)))
                yield return $"Model with name '{_selectedName}' already exists";
            if (string.IsNullOrWhiteSpace(_selectedModelPath))
                yield return string.Empty;
            if (!string.IsNullOrWhiteSpace(_selectedModelPath))
            {
                if (_selectedSource == ModelSourceType.Folder && !Directory.Exists(_selectedModelPath))
                    yield return "Model folder not found";
                else if (_selectedSource == ModelSourceType.SingleFile && (string.IsNullOrEmpty(CheckpointModel.SingleFile) || !IsCheckpointValid(CheckpointModel.SingleFile)))
                    yield return "Model file not found";
                else if ((_selectedSource == ModelSourceType.HuggingFace || _selectedSource == ModelSourceType.Checkpoint) && !Utils.TryParseHuggingFaceRepo(_selectedModelPath, out _))
                    yield return "HuggingFace repository not found";

                if (_selectedSource == ModelSourceType.Checkpoint)
                {
                    if (string.IsNullOrEmpty(CheckpointModel.TextEncoder)
                     && string.IsNullOrEmpty(CheckpointModel.TextEncoder2)
                     && string.IsNullOrEmpty(CheckpointModel.TextEncoder3)
                     && string.IsNullOrEmpty(CheckpointModel.Transformer)
                     && string.IsNullOrEmpty(CheckpointModel.Transformer2)
                     && string.IsNullOrEmpty(CheckpointModel.Vae)
                     && string.IsNullOrEmpty(CheckpointModel.AudioVae)
                     && string.IsNullOrEmpty(CheckpointModel.Vocoder)
                     && string.IsNullOrEmpty(CheckpointModel.Connectors))
                        yield return "At least one checkpoint model required";

                    if (!string.IsNullOrEmpty(CheckpointModel.TextEncoder) && !IsCheckpointValid(CheckpointModel.TextEncoder))
                        yield return "TextEncoder checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.TextEncoder2) && !IsCheckpointValid(CheckpointModel.TextEncoder2))
                        yield return "TextEncoder2 checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.TextEncoder3) && !IsCheckpointValid(CheckpointModel.TextEncoder3))
                        yield return "TextEncoder3 checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.Transformer) && !IsCheckpointValid(CheckpointModel.Transformer))
                        yield return "Transformer checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.Transformer2) && !IsCheckpointValid(CheckpointModel.Transformer2))
                        yield return "Transformer2 checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.Vae) && !IsCheckpointValid(CheckpointModel.Vae))
                        yield return "Vae checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.AudioVae) && !IsCheckpointValid(CheckpointModel.AudioVae))
                        yield return "AudioVae checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.Vocoder) && !IsCheckpointValid(CheckpointModel.Vocoder))
                        yield return "Vocoder checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.Connectors) && !IsCheckpointValid(CheckpointModel.Connectors))
                        yield return "Connectors checkpoint file not found";
                }
            }
        }


        private bool IsCheckpointValid(string checkpoint)
        {
            return File.Exists(checkpoint) || Utils.IsCheckpointInstalled(Settings.DirectoryModel, checkpoint) || Utils.IsHuggingFaceLink(checkpoint);
        }


        private int GetNextModelId()
        {
            return Math.Max(Utils.FixedIdRange, Settings.DiffusionModels.Max(x => x.Id)) + 1;
        }


        private void Reset()
        {
            SelectedName = null;
            SelectedVariant = null;
            SelectedModelPath = null;
            SelectedSource = ModelSourceType.HuggingFace;
            CheckpointModel = new DiffusionCheckpointModel();
        }


        private void GenerateName()
        {
            if (!string.IsNullOrWhiteSpace(_selectedModelPath))
            {
                //if (_selectedSource == ModelSourceType.Checkpoint)
                //{
                //    var filename = _checkpointModel.ModelCheckpoint;
                //    if (string.IsNullOrWhiteSpace(filename))
                //        filename = _checkpointModel.VaeCheckpoint;
                //    else if (string.IsNullOrWhiteSpace(filename))
                //        filename = _checkpointModel.TextEncoderCheckpoint;

                //    if (!string.IsNullOrWhiteSpace(filename))
                //        SelectedName = Path.GetFileNameWithoutExtension(filename);
                //}
                //else
                //{
                //    if (File.Exists(_selectedModelPath) || Directory.Exists(_selectedModelPath))
                //    {
                //        SelectedName = Path.GetFileNameWithoutExtension(_selectedModelPath);
                //    }
                //    else
                //    {
                //        SelectedName = Utils.TryParseHuggingFaceRepo(_selectedModelPath, out var huggingfaceRepo)
                //            ? huggingfaceRepo.Split('/', '\\').LastOrDefault()
                //            : Path.GetFileNameWithoutExtension(_selectedModelPath.Split('/', '\\').LastOrDefault());
                //    }
                //}
            }
        }

    }
}
