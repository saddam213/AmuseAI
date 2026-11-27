using Amuse.App.Common;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Controls;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.Media;
using TensorStack.WPF;

namespace Amuse.App
{
    public sealed class Settings : BaseModel, IUIConfiguration
    {
        private Orientation _historyOrientation;
        private bool _isFrameCountInFrames;
        private int _historyItems = 500;
        private double _uiScale = 1;
        private ThemeType _theme;
        private string _uiLanguage;
        private double _volumeInput = 0.1;
        private double _volumeOutput = 0.1;
        private bool _isVolumeInputMute;
        private bool _isVolumeOutputMute;
        private bool _isAutoUpdateEnabled = true;
        private bool _isUpdateAvailable;
        private bool _isInstalledModelsOnly;
        private VendorType[] vendors;

        public Settings()
        {
            Pipelines = [.. Enum.GetValues<PipelineType>().OrderBy(x => x.GetDisplayName())];
            DiffusionPipelines = [.. Pipelines.Where(x => (int)x < 500)];
            LanguagePipelines = [PipelineType.AutoTextPipeline, PipelineType.Gemma4Pipeline, PipelineType.Qwen3Pipeline];
        }

        [AppDefault]
        public int Version { get; set; }
        [AppDefault]
        public bool RunMigrations { get; set; }

        [JsonIgnore]
        public VendorType[] Vendors
        {
            get { return vendors; }
            set { SetProperty(ref vendors, value); }
        }
        public int DefaultDeviceId { get; set; }
        public string DirectoryTemp { get; set; }
        public string DirectoryHistory { get; set; }
        public string DirectoryModel { get; set; }

        [JsonIgnore]
        public string DirectoryDiffusion { get; private set; }

        [JsonIgnore]
        public string DirectoryLoraAdapter { get; private set; }

        [JsonIgnore]
        public string DirectoryControlNet { get; private set; }

        [JsonIgnore]
        public string DirectoryUpscale { get; private set; }

        [JsonIgnore]
        public string DirectoryExtract { get; private set; }

        [JsonIgnore]
        public string DirectoryLangaugeModel { get; private set; }

        public List<ModelSearchPath> ModelSearchPaths { get; set; }

        [JsonIgnore]
        public ModelSearchIndex ModelSearch { get; private set; } = ModelSearchIndex.Empty;

        public int ReadBuffer { get; set; } = 32;
        public int WriteBuffer { get; set; } = 32;
        public bool IsLegacyDeviceDetection { get; set; }
        public bool IsServerDebugEnabled { get; set; } = false;
        public bool IsHistoryRecentItemsEnabled { get; set; } = true;
        public bool IsHistoryAutoSortEnabled { get; set; } = true;
        public bool IsExternalLinksEnabled { get; set; }
        public bool IsExternalLinksAcknowledged { get; set; }
        public bool IsExternalToolsEnabled { get; set; }
        public bool IsExternalToolsAcknowledged { get; set; }
        public bool IsDangerousToolsEnabled { get; set; }
        public bool IsDangerousToolsAcknowledged { get; set; }
        public bool IsExecuteToolsEnabled { get; set; }
        public bool IsExecuteToolsAcknowledged { get; set; }
        public int MaxToolRounds { get; set; } = 16;
        public List<McpServerConfig> McpServers { get; set; }
        public bool IsAppSetupAcknowledged { get; set; }
        public bool IsDisclaimerAccepted { get; set; }
        public bool IsFirstRunComplete { get; set; }
        public string UserAgent { get; set; }

        public double VolumeInput
        {
            get { return _volumeInput; }
            set { SetProperty(ref _volumeInput, value); }
        }

        public double VolumeOutput
        {
            get { return _volumeOutput; }
            set { SetProperty(ref _volumeOutput, value); }
        }

        public bool IsVolumeInputMute
        {
            get { return _isVolumeInputMute; }
            set { SetProperty(ref _isVolumeInputMute, value); }
        }

        public bool IsVolumeOutputMute
        {
            get { return _isVolumeOutputMute; }
            set { SetProperty(ref _isVolumeOutputMute, value); }
        }

        public string UILanguage
        {
            get { return _uiLanguage; }
            set { SetProperty(ref _uiLanguage, value); }
        }

        public double UIScale
        {
            get { return _uiScale; }
            set { SetProperty(ref _uiScale, value); }
        }

        public ThemeType Theme
        {
            get { return _theme; }
            set { SetProperty(ref _theme, value); }
        }

        public int HistoryItems
        {
            get { return _historyItems; }
            set { SetProperty(ref _historyItems, value); }
        }

        public Orientation HistoryOrientation
        {
            get { return _historyOrientation; }
            set { SetProperty(ref _historyOrientation, value); }
        }

        public bool IsFrameCountInFrames
        {
            get { return _isFrameCountInFrames; }
            set { SetProperty(ref _isFrameCountInFrames, value); }
        }

        public bool IsAutoUpdateEnabled
        {
            get { return _isAutoUpdateEnabled; }
            set { SetProperty(ref _isAutoUpdateEnabled, value); }
        }

        public bool IsInstalledModelsOnly
        {
            get { return _isInstalledModelsOnly; }
            set { SetProperty(ref _isInstalledModelsOnly, value); }
        }

        [JsonIgnore]
        public bool IsUpdateAvailable
        {
            get { return _isUpdateAvailable; }
            set { SetProperty(ref _isUpdateAvailable, value); }
        }

        [AppDefault]
        public AccessTokenModel[] AccessTokens { get; set; }

        [AppDefault]
        public ObservableCollection<EnvironmentModel> Environments { get; set; }

        [AppDefault]
        public ObservableCollection<ComponentModel> Components { get; set; }

        [AppDefault]
        public ObservableCollection<DiffusionModel> DiffusionModels { get; set; }

        [AppDefault]
        public ObservableCollection<LanguageModel> LanguageModels { get; set; }

        [AppDefault]
        public ObservableCollection<LoraAdapterModel> LoraAdapterModels { get; set; }

        [AppDefault]
        public ObservableCollection<ControlNetModel> ControlNetModels { get; set; }

        [AppDefault]
        public ObservableCollection<UpscaleModel> UpscaleModels { get; set; }

        [AppDefault]
        public ObservableCollection<ExtractModel> ExtractModels { get; set; }

        [AppDefault]
        public ObservableCollection<PublisherModel> Publishers { get; set; } = [];

        [JsonIgnore]
        public List<DeviceModel> Devices { get; set; }

        [JsonIgnore]
        public TemplateSettings Templates { get; set; }


        public async Task Initialize(string directoryData)
        {
            UserAgent = $"Amuse/{App.AppVersionDisplay}";
            if (string.IsNullOrEmpty(DirectoryTemp) || !Path.Exists(DirectoryTemp))
                DirectoryTemp = Path.Combine(directoryData, "Temp");
            if (string.IsNullOrEmpty(DirectoryHistory) || !Path.Exists(DirectoryHistory))
                DirectoryHistory = Path.Combine(directoryData, "History");

            Directory.CreateDirectory(DirectoryTemp);
            Directory.CreateDirectory(DirectoryHistory);
            MediaManager.Initialize(directoryTemp: DirectoryTemp);
            CreateModelDirectory(directoryData);

            var templateSettings = Path.Combine(directoryData, "Templates.json");
            if (File.Exists(templateSettings))
            {
                Templates = await Json.LoadAsync<TemplateSettings>(templateSettings);
            }

            ResetDownloadStatus();
            ScanModels();
            Vendors = [.. Environments.Select(x => x.Vendor).Distinct()];
            await SaveAsync();
        }


        /// <summary>
        /// Resets the download statuses left by the last session, the download queue is empty at startup.
        /// </summary>
        private void ResetDownloadStatus()
        {
            var models = Components.Cast<IDownloadModel>()
                .Concat(DiffusionModels)
                .Concat(LanguageModels)
                .Concat(LoraAdapterModels)
                .Concat(ControlNetModels)
                .Concat(UpscaleModels)
                .Concat(ExtractModels);
            foreach (var model in models)
            {
                if (model.Status.IsDownloadActive())
                    model.Status = ModelStatusType.Available;
            }
        }


        public void InitializeDevices(IReadOnlyList<DeviceModel> devices)
        {
            Devices = devices
                .Where(x => x.Type == DeviceType.GPU)
                .ToList();
        }


        /// <summary>
        /// Gets the publisher with the key, null when the key is empty or not listed.
        /// </summary>
        /// <param name="key">The publisher key.</param>
        public PublisherModel GetPublisher(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            return Publishers?.FirstOrDefault(x => x.Key == key);
        }


        public DeviceModel GetDefaultDevice()
        {
            var vendorDevices = Devices.Where(x => Vendors.Contains(x.Vendor));
            return vendorDevices.FirstOrDefault(x => x.Id == DefaultDeviceId) ?? vendorDevices.FirstOrDefault();
        }


        public async Task SetDefaultsAsync(PipelineModel pipeline)
        {
            if (pipeline.DiffusionModel != null)
            {
                var defaultModel = DiffusionModels.FirstOrDefault(x => x.IsDefault && x.ProcessTypes.Contains(pipeline.ProcessType));
                if (defaultModel is not null)
                    defaultModel.IsDefault = false;

                pipeline.DiffusionModel.UserMemoryMode = pipeline.MemoryMode;
                pipeline.DiffusionModel.UserExecutionOptions = pipeline.ExecutionOptions;
                pipeline.DiffusionModel.IsDefault = true;
            }
            if (pipeline.LanguageModel != null)
            {
                var defaultModel = LanguageModels.FirstOrDefault(x => x.IsDefault && x.ProcessTypes.Contains(pipeline.ProcessType));
                if (defaultModel is not null)
                    defaultModel.IsDefault = false;

                pipeline.LanguageModel.UserMemoryMode = pipeline.MemoryMode;
                pipeline.LanguageModel.UserExecutionOptions = pipeline.ExecutionOptions;
                pipeline.LanguageModel.IsDefault = true;
            }
            if (pipeline.UpscaleModel != null)
            {
                var defaultModel = UpscaleModels.FirstOrDefault(x => x.IsDefault);
                if (defaultModel is not null)
                    defaultModel.IsDefault = false;

                pipeline.UpscaleModel.IsDefault = true;
            }
            if (pipeline.ExtractModel != null)
            {
                var defaultModel = ExtractModels.FirstOrDefault(x => x.IsDefault);
                if (defaultModel is not null)
                    defaultModel.IsDefault = false;

                pipeline.ExtractModel.IsDefault = true;
            }

            DefaultDeviceId = pipeline.Device.Id;
            await SaveAsync();
        }


        /// <summary>
        /// Refreshes the installed state of every model, the model search folders are read again first.
        /// </summary>
        public void ScanModels()
        {
            ModelSearch = new ModelSearchIndex(DirectoryModel, ModelSearchPaths, GetCheckpointComponents());

            foreach (var component in Components)
                component.Initialize(this);

            foreach (var diffusionModel in DiffusionModels)
                diffusionModel.Initialize(this);

            foreach (var languageModel in LanguageModels)
                languageModel.Initialize(this);

            foreach (var loraAdapterModel in LoraAdapterModels)
                loraAdapterModel.Initialize(this);

            foreach (var controlNetModel in ControlNetModels)
                controlNetModel.Initialize(this);

            foreach (var upscaleModel in UpscaleModels)
                upscaleModel.Initialize(this);

            foreach (var extractModel in ExtractModels)
                extractModel.Initialize(this);
        }


        /// <summary>
        /// Changes the key of a shared component in every model checkpoint that uses it.
        /// </summary>
        /// <param name="oldKey">The current key.</param>
        /// <param name="newKey">The new key.</param>
        public void RenameComponentKey(string oldKey, string newKey)
        {
            foreach (var checkpoint in GetCheckpointComponents().Where(x => x.Type == CheckpointType.Component && x.Path == oldKey))
                checkpoint.Path = newKey;
        }


        /// <summary>
        /// Gets the checkpoints of every model, including the checkpoint sets that are not current.
        /// </summary>
        private IEnumerable<CheckpointComponent> GetCheckpointComponents()
        {
            return Components.Select(x => x.Checkpoint)
                .Concat(DiffusionModels.SelectMany(x => x.GetCheckpoints()).SelectMany(x => x.GetComponents()))
                .Concat(LanguageModels.SelectMany(x => x.GetCheckpoints()).SelectMany(x => x.GetComponents()))
                .Concat(LoraAdapterModels.Select(x => x.Checkpoint))
                .Concat(ControlNetModels.Select(x => x.Checkpoint))
                .Concat(UpscaleModels.SelectMany(x => x.GetComponents()))
                .Concat(ExtractModels.Select(x => x.Checkpoint));
        }


        public void SetTempDirectory(string directory)
        {
            DirectoryTemp = directory;
            Directory.CreateDirectory(directory);
            MediaManager.Initialize(directoryTemp: directory);
        }


        public void SetHistoryDirectory(string directory)
        {
            DirectoryHistory = directory;
            Directory.CreateDirectory(directory);
        }


        public void SetModelDirectory(string directory)
        {
            DirectoryModel = directory;
            DirectoryDiffusion = Path.Combine(directory, ModelFolders.Diffusion);
            DirectoryLangaugeModel = Path.Combine(directory, ModelFolders.LLM);
            DirectoryUpscale = Path.Combine(directory, ModelFolders.Upscale);
            DirectoryExtract = Path.Combine(directory, ModelFolders.Extract);
            DirectoryControlNet = Path.Combine(directory, ModelFolders.ControlNet);
            DirectoryLoraAdapter = Path.Combine(directory, ModelFolders.LoraAdapter);
            CreateModelDirectories();
        }


        private void CreateModelDirectory(string directoryData)
        {
            if (string.IsNullOrEmpty(DirectoryModel) || !Path.Exists(DirectoryModel))
                DirectoryModel = Path.Combine(directoryData, "Models");

            SetModelDirectory(DirectoryModel);
        }


        private void CreateModelDirectories()
        {
            Directory.CreateDirectory(DirectoryModel);
            Directory.CreateDirectory(DirectoryDiffusion);
            Directory.CreateDirectory(DirectoryUpscale);
            Directory.CreateDirectory(DirectoryExtract);
            Directory.CreateDirectory(DirectoryControlNet);
            Directory.CreateDirectory(DirectoryLoraAdapter);
            Directory.CreateDirectory(DirectoryLangaugeModel);
        }


        public bool DeleteTempDirectory()
        {
            try
            {
                return FileHelper.DeleteDirectory(Path.Combine(DirectoryTemp, "EBWebView"));
            }
            catch (Exception)
            {
                return false;
            }
        }


        [JsonIgnore]
        public PipelineType[] Pipelines { get; }

        [JsonIgnore]
        public PipelineType[] DiffusionPipelines { get; }

        [JsonIgnore]
        public PipelineType[] LanguagePipelines { get; }


        /// <summary>
        /// Gets the pipelines that support LoRA, ordered by display name.
        /// </summary>
        public PipelineType[] GetLoraPipelines()
        {
            return [.. Pipelines.Where(IsLoraPipeline)];
        }


        /// <summary>
        /// Gets the pipelines that support ControlNet, those with ControlNets already or with a ControlNet template, ordered by display name.
        /// </summary>
        public PipelineType[] GetControlNetPipelines()
        {
            return [.. Pipelines.Where(pipeline => ControlNetModels?.Any(x => x.Pipeline == pipeline) == true
                || Templates?.DiffusionTemplates?.Any(x => x.Pipeline == pipeline && x.ProcessTypes?.Contains(ProcessType.ImageControlNet) == true) == true)];
        }


        /// <summary>
        /// Determines whether a pipeline supports LoRA, it has LoRAs already or an image or video template on a backend that loads them.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        private bool IsLoraPipeline(PipelineType pipeline)
        {
            if (LoraAdapterModels?.Any(x => x.Pipeline == pipeline) == true)
                return true;

            return Templates?.DiffusionTemplates?.Any(x => x.Pipeline == pipeline
                && x.Backend != BackendType.OnnxRuntime
                && (x.MediaType == MediaType.Image || x.MediaType == MediaType.Video)) == true;
        }


        public void Save()
        {
            SettingsManager.Save(this);
        }


        public async Task SaveAsync()
        {
            await SettingsManager.SaveAsync(this);
        }
    }
}
