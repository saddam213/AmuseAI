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
using TensorStack.WPF;

namespace Amuse.App
{
    public sealed class Settings : BaseModel, IUIConfiguration
    {
        private Orientation _historyOrientation;
        private int _historyItems = 500;
        private double _uiScale = 1;
        private string _uiLanguage;
        private double _volumeInput = 0.1;
        private double _volumeOutput = 0.1;
        private bool _isVolumeInputMute;
        private bool _isVolumeOutputMute;
        private bool _isAutoUpdateEnabled = true;
        private bool _isUpdateAvailable;
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

        public int ReadBuffer { get; set; } = 32;
        public int WriteBuffer { get; set; } = 32;
        public string VideoCodec { get; set; } = "mp4v";
        public bool IsLegacyDeviceDetection { get; set; }
        public bool IsServerDebugEnabled { get; set; } = false;
        public bool IsOptimizeDeviceEnabled { get; set; } = false;
        public bool IsOptimizeChannelsEnabled { get; set; } = false;
        public bool IsDeviceQuantizationEnabled { get; set; } = true;
        public bool IsHistoryRecentItemsEnabled { get; set; } = true;
        public bool IsHistoryAutoSortEnabled { get; set; } = true;
        public bool IsDiffusionImagePreviewEnabled { get; set; } = true;
        public bool IsExternalLinksEnabled { get; set; }
        public bool IsExternalLinksAcknowledged { get; set; }

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

        public bool IsAutoUpdateEnabled
        {
            get { return _isAutoUpdateEnabled; }
            set { SetProperty(ref _isAutoUpdateEnabled, value); }
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

        [JsonIgnore]
        public List<DeviceModel> Devices { get; set; }

        [JsonIgnore]
        public TemplateSettings Templates { get; set; }


        public async Task Initialize(string directoryData)
        {
            if (string.IsNullOrEmpty(DirectoryTemp) || !Path.Exists(DirectoryTemp))
                DirectoryTemp = Path.Combine(directoryData, "Temp");
            if (string.IsNullOrEmpty(DirectoryHistory) || !Path.Exists(DirectoryHistory))
                DirectoryHistory = Path.Combine(directoryData, "History");

            Directory.CreateDirectory(DirectoryTemp);
            Directory.CreateDirectory(DirectoryHistory);
            CreateModelDirectory(directoryData);

            var templateSettings = Path.Combine(directoryData, "Templates.json");
            if (File.Exists(templateSettings))
            {
                Templates = await Json.LoadAsync<TemplateSettings>(templateSettings);
            }

            ScanModels();
            Vendors = [.. Environments.Select(x => x.Vendor).Distinct()];
            SettingsManager.Save(this);
        }


        public void InitializeDevices(IReadOnlyList<DeviceModel> devices)
        {
            Devices = devices
                .Where(x => x.Type == DeviceType.GPU)
                .ToList();
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
                var defaultModel = DiffusionModels.FirstOrDefault(x => x.IsDefault);
                if (defaultModel is not null)
                    defaultModel.IsDefault = false;

                pipeline.DiffusionModel.UserQualityMode = pipeline.QualityMode;
                pipeline.DiffusionModel.UserMemoryMode = pipeline.MemoryMode;
                pipeline.DiffusionModel.IsDefault = true;
            }
            if (pipeline.LanguageModel != null)
            {
                var defaultModel = LanguageModels.FirstOrDefault(x => x.IsDefault);
                if (defaultModel is not null)
                    defaultModel.IsDefault = false;

                pipeline.LanguageModel.UserQualityMode = pipeline.QualityMode;
                //pipeline.LanguageModel.UserMemoryMode = pipeline.MemoryMode;
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
            await SettingsManager.SaveAsync(this);
        }


        public void ScanModels()
        {
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


        public void SetTempDirectory(string directory)
        {
            DirectoryTemp = directory;
            Directory.CreateDirectory(directory);
        }


        public void SetHistoryDirectory(string directory)
        {
            DirectoryHistory = directory;
            Directory.CreateDirectory(directory);
        }


        public void SetModelDirectory(string directory)
        {
            DirectoryModel = directory;
            DirectoryDiffusion = Path.Combine(directory, "Diffusion");
            DirectoryLangaugeModel = Path.Combine(directory, "TextEncoder");
            DirectoryUpscale = Path.Combine(directory, "Upscale");
            DirectoryExtract = Path.Combine(directory, "Extract");
            DirectoryControlNet = Path.Combine(directory, "ControlNet");
            DirectoryLoraAdapter = Path.Combine(directory, "LoraAdapter");
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
    }
}
