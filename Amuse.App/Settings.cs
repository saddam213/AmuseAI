using Amuse.App.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Controls;
using TensorStack.Common;
using TensorStack.WPF;

namespace Amuse.App
{
    public class Settings : BaseModel, IUIConfiguration
    {
        private Orientation _historyOrientation;
        private int _historyItems = 500;
        private double _uiScale = 1;
        private double _volumeInput = 0.1;
        private double _volumeOutput = 0.1;
        private bool _isVolumeInputMute;
        private bool _isVolumeOutputMute;
        private bool _isUpdateEnabled = false;
        private bool _isUpdateAvailable;

        [AppDefault]
        public int Version { get; set; }
        public VendorType[] Vendors { get; set; }
        public int DefaultDeviceId { get; set; }
        public string DirectoryTemp { get; set; }
        public string DirectoryModel { get; set; }
        public string DirectoryHistory { get; set; }
        public string SecureToken { get; set; }
        public int ReadBuffer { get; set; } = 32;
        public int WriteBuffer { get; set; } = 32;
        public string VideoCodec { get; set; } = "mp4v";
        public bool IsLegacyDeviceDetection { get; set; }
        public bool IsServerDebugEnabled { get; set; } = false;
        public bool IsOptimizeDeviceEnabled { get; set; } = false;
        public bool IsOptimizeChannelsEnabled { get; set; } = false;
        public bool IsDeviceQuantizationEnabled { get; set; } = false;

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

        public bool IsUpdateEnabled
        {
            get { return _isUpdateEnabled; }
            set { SetProperty(ref _isUpdateEnabled, value); }
        }

        [JsonIgnore]
        public bool IsUpdateAvailable
        {
            get { return _isUpdateAvailable; }
            set { SetProperty(ref _isUpdateAvailable, value); }
        }

        [AppDefault]
        public ObservableCollection<EnvironmentModel> Environments { get; set; }

        [AppDefault]
        public ObservableCollection<UpscaleModel> UpscaleModels { get; set; }

        [AppDefault]
        public ObservableCollection<AudioModel> AudioModels { get; set; }

        [AppDefault]
        public ObservableCollection<DiffusionModel> DiffusionModels { get; set; }

        [AppDefault]
        public ObservableCollection<LoraAdapterModel> LoraAdapterModels { get; set; }

        [AppDefault]
        public ObservableCollection<ControlNetModel> ControlNetModels { get; set; }

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
            if (string.IsNullOrEmpty(DirectoryModel) || !Path.Exists(DirectoryModel))
                DirectoryModel = Path.Combine(directoryData, "Models");
            if (string.IsNullOrEmpty(DirectoryHistory) || !Path.Exists(DirectoryHistory))
                DirectoryHistory = Path.Combine(directoryData, "History");

            var templateSettings = Path.Combine(App.DirectoryData, "Templates.json");
            if (File.Exists(templateSettings))
            {
                Templates = await Json.LoadAsync<TemplateSettings>(templateSettings);
            }

            Directory.CreateDirectory(DirectoryTemp);
            Directory.CreateDirectory(DirectoryModel);
            Directory.CreateDirectory(DirectoryHistory);

            ScanModels();
            SettingsManager.Save(this);
        }


        public void InitializeDevices(IReadOnlyList<DeviceModel> devices)
        {
            Devices = devices
                .Where(x => x.Type == DeviceType.GPU && !string.IsNullOrEmpty(x.HardwareVendor) && Vendors.Contains(x.Vendor))
                .ToList();
        }


        public DeviceModel GetDefaultDevice()
        {
            return Devices.FirstOrDefault(x => x.Id == DefaultDeviceId) ?? Devices.FirstOrDefault();
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
            if (pipeline.AudioModel != null)
            {
                var defaultModel = AudioModels.FirstOrDefault(x => x.IsDefault);
                if (defaultModel is not null)
                    defaultModel.IsDefault = false;

                pipeline.AudioModel.IsDefault = true;
            }

            DefaultDeviceId = pipeline.Device.Id;
            await SettingsManager.SaveAsync(this);
        }


        public HashSet<string> GetPipelines()
        {
            var pipelines = new HashSet<string>(
            [
                "ChromaPipeline",
                "CogVideoXPipeline",
                "FluxPipeline",
                "Flux2Pipeline",
                "Flux2KleinPipeline",
                "HeliosPipeline",
                "Kandinsky5Pipeline",
                "LTXPipeline",
                "LTX20Pipeline",
                "LTX23Pipeline",
                "QwenImagePipeline",
                "SkyReelsV2Pipeline",
                "StableDiffusion3Pipeline",
                "StableDiffusionXLPipeline",
                "WanPipeline",
                "ZImagePipeline"
            ]);

            foreach (var pipeline in DiffusionModels.Select(x => x.Pipeline).Distinct())
            {
                pipelines.Add(pipeline);
            }
            return pipelines;
        }


        public void ScanModels()
        {
            var upscaleDirectory = Path.Combine(DirectoryModel, "Upscale");
            foreach (var upscaleModel in UpscaleModels)
                upscaleModel.Initialize(upscaleDirectory);
            var extractDirectory = Path.Combine(DirectoryModel, "Extract");
            foreach (var extractModel in ExtractModels)
                extractModel.Initialize(extractDirectory);
            var audioDirectory = Path.Combine(DirectoryModel, "Audio");
            foreach (var audioModel in AudioModels)
                audioModel.Initialize(audioDirectory);
            foreach (var diffusionModel in DiffusionModels)
                diffusionModel.Initialize(DirectoryModel);
            foreach (var loraAdapterModel in LoraAdapterModels)
                loraAdapterModel.Initialize(DirectoryModel);
            foreach (var controlNetModel in ControlNetModels)
                controlNetModel.Initialize(DirectoryModel);
        }
    }
}
