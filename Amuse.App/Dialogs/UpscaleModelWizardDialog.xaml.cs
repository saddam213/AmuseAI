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
    /// Interaction logic for UpscaleModelWizardDialog.xaml
    /// </summary>
    public partial class UpscaleModelWizardDialog : DialogControl
    {
        private static readonly string[] _imageExtensions = [".pth", ".safetensors", ".gguf", ".onnx"];
        private readonly IModelDownloadService _downloadService;
        private static readonly string[] _videoExtensions = [".safetensors", ".gguf"];

        private PipelineType? _selectedPipeline;
        private CheckpointType _selectedSource;
        private string _selectedFile;
        private string _selectedLink;
        private string _selectedName;
        private string _suggestedName;
        private int _selectedScaleFactor;
        private CheckpointComponent _selectedVae;
        private UpscaleModel _upscaleModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpscaleModelWizardDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="downloadService">The model download service.</param>
        public UpscaleModelWizardDialog(Settings settings, IModelDownloadService downloadService)
        {
            Settings = settings;
            _downloadService = downloadService;
            PipelineOptions = new ObservableCollection<PipelineType>();
            Errors = new ObservableCollection<string>();
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            SelectedSource = CheckpointType.LocalFile;
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public ObservableCollection<PipelineType> PipelineOptions { get; }
        public CheckpointType[] VaeCheckpointTypes { get; } = [CheckpointType.OnlineFile, CheckpointType.LocalFile, CheckpointType.Component];

        public string FileFilter
        {
            get { return DownloadLink.GetFileFilter(AppDefault.ModelFiles, GetFileExtensions()); }
        }

        public UpscaleModel UpscaleModel
        {
            get { return _upscaleModel; }
            private set { SetProperty(ref _upscaleModel, value); }
        }

        public PipelineType? SelectedPipeline
        {
            get { return _selectedPipeline; }
            set
            {
                SetProperty(ref _selectedPipeline, value);
                NotifyPropertyChanged(nameof(FileFilter));
                NotifyPropertyChanged(nameof(IsVideoUpscaler));
                SelectedScaleFactor = GetTemplate()?.ScaleFactor ?? 2;
                SelectedVae = GetTemplate()?.Vae?.DeepClone() ?? new CheckpointComponent { Name = "VAE", Type = CheckpointType.Component };
                SuggestFromFile(GetSourceFile());
            }
        }

        public bool IsVideoUpscaler
        {
            get { return _selectedPipeline.HasValue && _selectedPipeline != PipelineType.UpscalePipeline; }
        }

        public CheckpointType SelectedSource
        {
            get { return _selectedSource; }
            set
            {
                SetProperty(ref _selectedSource, value);
                NotifyPropertyChanged(nameof(IsLocalFile));
                NotifyPropertyChanged(nameof(IsOnlineFile));
                SuggestFromFile(GetSourceFile());
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
            set { SetProperty(ref _selectedFile, value); SuggestFromFile(value); }
        }

        public string SelectedLink
        {
            get { return _selectedLink; }
            set { SetProperty(ref _selectedLink, value); SuggestFromFile(DownloadLink.GetFileName(value, GetFileExtensions())); }
        }

        public string SelectedName
        {
            get { return _selectedName; }
            set { SetProperty(ref _selectedName, value); }
        }

        public int SelectedScaleFactor
        {
            get { return _selectedScaleFactor; }
            set { SetProperty(ref _selectedScaleFactor, value); }
        }

        public CheckpointComponent SelectedVae
        {
            get { return _selectedVae; }
            set { SetProperty(ref _selectedVae, value); }
        }


        /// <summary>
        /// Shows the wizard with the upscale pipelines, selecting the image upscaler.
        /// </summary>
        public Task<bool> AddAsync()
        {
            PipelineOptions.Clear();
            var pipelines = Settings.UpscaleModels
                .Select(x => x.Pipeline)
                .Prepend(PipelineType.UpscalePipeline)
                .Distinct();
            foreach (var pipeline in pipelines)
                PipelineOptions.Add(pipeline);

            SelectedPipeline = PipelineType.UpscalePipeline;
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Adds the upscaler from the selected pipeline, file and name, the other settings come from a built-in upscaler of the same pipeline and backend.
        /// </summary>
        protected override async Task SaveAsync()
        {
            var modelId = Settings.UpscaleModels.NextId();
            var model = GetTemplate()?.DeepClone(modelId) ?? CreateModel(modelId);
            model.Name = _selectedName.Trim();
            model.Backend = GetBackend();
            model.Pipeline = _selectedPipeline.Value;
            model.ScaleFactor = _selectedScaleFactor;
            model.Status = _selectedSource == CheckpointType.OnlineFile ? ModelStatusType.Available : ModelStatusType.Unknown;
            model.Checkpoint = CreateCheckpoint();
            model.Vae = IsVideoUpscaler ? _selectedVae : null;
            model.Link = null;
            model.Metadata = null;
            UpscaleModel = model;

            Settings.UpscaleModels.Add(UpscaleModel);
            UpscaleModel.Initialize(Settings);
            await Settings.SaveAsync();
            await _downloadService.PromptDownloadAsync(UpscaleModel);
            await base.SaveAsync();
        }


        /// <summary>
        /// Determines whether the upscaler can be saved, listing the validation errors.
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
        /// Gets the file extensions the selected pipeline can load.
        /// </summary>
        private string[] GetFileExtensions()
        {
            return IsVideoUpscaler ? _videoExtensions : _imageExtensions;
        }


        /// <summary>
        /// Gets the backend that loads the selected file, ONNX Runtime for an .onnx image upscaler and StableDiffusion.cpp for the rest.
        /// </summary>
        private BackendType GetBackend()
        {
            if (IsVideoUpscaler)
                return BackendType.StableDiffusionCpp;

            var extension = Path.GetExtension(GetSourceFile() ?? string.Empty);
            return extension.Equals(".onnx", StringComparison.OrdinalIgnoreCase) ? BackendType.OnnxRuntime : BackendType.StableDiffusionCpp;
        }


        /// <summary>
        /// Gets the built-in upscaler the settings are taken from, one of the same pipeline and backend.
        /// </summary>
        private UpscaleModel GetTemplate()
        {
            if (!_selectedPipeline.HasValue)
                return null;

            var backend = GetBackend();
            var models = Settings.UpscaleModels.Where(x => x.Id <= Utils.FixedIdRange && x.Pipeline == _selectedPipeline.Value);
            return models.FirstOrDefault(x => x.Backend == backend) ?? models.FirstOrDefault();
        }


        /// <summary>
        /// Creates an image upscaler with tiling, when there is no built-in upscaler to take the settings from.
        /// </summary>
        /// <param name="modelId">The model id.</param>
        private static UpscaleModel CreateModel(int modelId)
        {
            return new UpscaleModel
            {
                Id = modelId,
                DefaultOptions = new UpscaleInputOptions
                {
                    IsTileEnabled = true,
                    TileOverlap = 16,
                    TileSize = 512
                }
            };
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
                    Name = "Upscale",
                    Type = CheckpointType.OnlineFile,
                    DownloadFiles = [DownloadLink.GetDownloadLink(_selectedLink)]
                };
            }

            return new CheckpointComponent
            {
                Name = "Upscale",
                Type = CheckpointType.LocalFile,
                Path = _selectedFile
            };
        }


        /// <summary>
        /// Gets the file of the selected source.
        /// </summary>
        private string GetSourceFile()
        {
            return _selectedSource == CheckpointType.OnlineFile ? DownloadLink.GetFileName(_selectedLink, GetFileExtensions()) : _selectedFile;
        }


        /// <summary>
        /// Fills the name and scale factor from a file name, the name only when the user has not typed one of their own.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        private void SuggestFromFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return;

            var scaleFactor = DownloadLink.GetScaleFactor(fileName);
            if (scaleFactor.HasValue)
                SelectedScaleFactor = scaleFactor.Value;

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
            else if (Settings.UpscaleModels.Any(x => x.Name.Equals(_selectedName.Trim(), StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationModelNameExists, _selectedName.Trim());

            if (_selectedScaleFactor <= 0)
                yield return string.Format(AppErrors.ValidationGreaterThanZero, AppDefault.ScaleFactor);

            if (IsVideoUpscaler && _selectedVae?.IsValid(out var vaeValidation) == false)
                yield return vaeValidation;

            var fileExtensions = GetFileExtensions();
            if (_selectedSource == CheckpointType.OnlineFile)
            {
                if (string.IsNullOrWhiteSpace(_selectedLink))
                    yield return AppErrors.ValidationDownloadLinkEmpty;
                else if (DownloadLink.GetFileName(_selectedLink, fileExtensions) == null)
                    yield return string.Format(AppErrors.ValidationDownloadLinkInvalid, string.Join(", ", fileExtensions));
            }
            else
            {
                if (string.IsNullOrEmpty(_selectedFile))
                    yield return AppErrors.ValidationModelFileNameEmpty;
                else if (!File.Exists(_selectedFile))
                    yield return AppErrors.ValidationModelFileNotFound;
                else if (!DownloadLink.IsSupportedFile(_selectedFile, fileExtensions))
                    yield return string.Format(AppErrors.UpscaleCheckpointFormat, string.Join(", ", fileExtensions), GetBackend());
            }
        }
    }
}
