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
    /// Interaction logic for LoraModelWizardDialog.xaml
    /// </summary>
    public partial class LoraModelWizardDialog : DialogControl
    {
        private static readonly string[] _fileExtensions = [".safetensors", ".gguf"];

        private readonly IModelDownloadService _downloadService;
        private PipelineType? _selectedPipeline;
        private CheckpointType _selectedSource;
        private string _selectedFile;
        private string _selectedLink;
        private string _selectedName;
        private string _suggestedName;
        private string _selectedTriggers;
        private LoraAdapterModel _loraModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoraModelWizardDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="downloadService">The model download service.</param>
        public LoraModelWizardDialog(Settings settings, IModelDownloadService downloadService)
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

        public LoraAdapterModel LoraModel
        {
            get { return _loraModel; }
            private set { SetProperty(ref _loraModel, value); }
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

        public string SelectedTriggers
        {
            get { return _selectedTriggers; }
            set { SetProperty(ref _selectedTriggers, value); }
        }


        /// <summary>
        /// Shows the wizard with the pipelines that support LoRA, selecting the first one.
        /// </summary>
        public Task<bool> AddAsync()
        {
            SetPipelineOptions(null);
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Shows the wizard with the pipelines that support LoRA, selecting a pipeline.
        /// </summary>
        /// <param name="pipelineType">The pipeline to select.</param>
        public Task<bool> AddAsync(PipelineType pipelineType)
        {
            SetPipelineOptions(pipelineType);
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Adds the LoRA from the selected pipeline, file and name.
        /// </summary>
        protected override async Task SaveAsync()
        {
            var modelId = Settings.LoraAdapterModels.NextId();
            var name = _selectedName.Trim();
            LoraModel = new LoraAdapterModel
            {
                Id = modelId,
                Name = name,
                Pipeline = _selectedPipeline.Value,
                Key = CreateKey(name, modelId),
                Triggers = GetTriggers(),
                Status = _selectedSource == CheckpointType.OnlineFile ? ModelStatusType.Available : ModelStatusType.Unknown,
                Checkpoint = CreateCheckpoint()
            };

            Settings.LoraAdapterModels.Add(LoraModel);
            LoraModel.Initialize(Settings);
            await Settings.SaveAsync();
            await _downloadService.PromptDownloadAsync(LoraModel);
            await base.SaveAsync();
        }


        /// <summary>
        /// Determines whether the LoRA can be saved, listing the validation errors.
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
        /// Fills the pipelines that support LoRA, adding the pipeline to select when it is not one of them.
        /// </summary>
        /// <param name="pipelineType">The pipeline to select, the first when <c>null</c>.</param>
        private void SetPipelineOptions(PipelineType? pipelineType)
        {
            PipelineOptions.Clear();
            foreach (var pipeline in Settings.GetLoraPipelines())
                PipelineOptions.Add(pipeline);

            if (pipelineType.HasValue && !PipelineOptions.Contains(pipelineType.Value))
                PipelineOptions.Add(pipelineType.Value);

            var imagePipeline = PipelineOptions.Where(IsImagePipeline).Cast<PipelineType?>().FirstOrDefault();
            SelectedPipeline = pipelineType ?? imagePipeline ?? PipelineOptions.Cast<PipelineType?>().FirstOrDefault();
        }


        /// <summary>
        /// Determines whether a pipeline has an image template.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        private bool IsImagePipeline(PipelineType pipeline)
        {
            return Settings.Templates?.DiffusionTemplates?.Any(x => x.Pipeline == pipeline && x.MediaType == MediaType.Image) == true;
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
                    Name = "LoraAdapter",
                    Type = CheckpointType.OnlineFile,
                    DownloadFiles = [DownloadLink.GetDownloadLink(_selectedLink)]
                };
            }

            return new CheckpointComponent
            {
                Name = "LoraAdapter",
                Type = CheckpointType.LocalFile,
                Path = _selectedFile
            };
        }


        /// <summary>
        /// Gets the trigger words from the comma separated input, <c>null</c> when there are none.
        /// </summary>
        private string[] GetTriggers()
        {
            if (string.IsNullOrWhiteSpace(_selectedTriggers))
                return null;

            var triggers = _selectedTriggers
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return triggers.Length == 0 ? null : triggers;
        }


        /// <summary>
        /// Creates the LoRA key from the letters and digits of its name and its id.
        /// </summary>
        /// <param name="name">The LoRA name.</param>
        /// <param name="modelId">The LoRA id.</param>
        private static string CreateKey(string name, int modelId)
        {
            return $"{new string([.. name.Where(char.IsLetterOrDigit)])}{modelId}".ToLower();
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
            else if (Settings.LoraAdapterModels.Any(x => x.Name.Equals(_selectedName.Trim(), StringComparison.OrdinalIgnoreCase)))
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
            }
        }
    }
}
