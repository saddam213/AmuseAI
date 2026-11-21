
using Amuse.App.Views;
using Amuse.Common;
using TensorStack.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class PipelineModel : BaseModel
    {
        private DeviceModel _device;
        private DiffusionModel _diffusionModel;
        private LanguageModel _languageModel;
        private ControlNetModel _controlNetModel;
        private ExtractModel _extractModel;
        private LoraAdapterModel[] _loraAdapterModel;
        private UpscaleModel _upscaleModel;
        private MemoryMode _memoryMode;
        private ProcessType _processType;
        private View _viewType;
        private ExecutionOptionsModel _executionOptions;

        public DeviceModel Device
        {
            get { return _device; }
            set { SetProperty(ref _device, value); }
        }

        public DiffusionModel DiffusionModel
        {
            get { return _diffusionModel; }
            set { SetProperty(ref _diffusionModel, value); NotifyBackendChanged(); }
        }

        public LanguageModel LanguageModel
        {
            get { return _languageModel; }
            set { SetProperty(ref _languageModel, value); NotifyBackendChanged(); }
        }

        public IGenerateModel GenerateModel
        {
            get
            {
                if (DiffusionModel is not null)
                    return DiffusionModel;

                return LanguageModel;
            }
        }

        public BackendType Backend => GenerateModel?.Backend ?? UpscaleModel?.Backend ?? ExtractModel?.Backend ?? default;

        public PipelineType PipelineType => GenerateModel?.Pipeline ?? UpscaleModel?.Pipeline ?? ExtractModel?.Pipeline ?? default;

        public bool IsUpscaleOnly => GenerateModel is null && UpscaleModel is not null;

        public bool IsExtractOnly => GenerateModel is null && UpscaleModel is null && ExtractModel is not null;

        public ControlNetModel ControlNetModel
        {
            get { return _controlNetModel; }
            set { SetProperty(ref _controlNetModel, value); }
        }

        public ExtractModel ExtractModel
        {
            get { return _extractModel; }
            set { SetProperty(ref _extractModel, value); NotifyBackendChanged(); }
        }

        public LoraAdapterModel[] LoraAdapterModel
        {
            get { return _loraAdapterModel; }
            set { SetProperty(ref _loraAdapterModel, value); }
        }

        public UpscaleModel UpscaleModel
        {
            get { return _upscaleModel; }
            set { SetProperty(ref _upscaleModel, value); NotifyBackendChanged(); }
        }

        public ProcessType ProcessType
        {
            get { return _processType; }
            set { SetProperty(ref _processType, value); }
        }

        public MemoryMode MemoryMode
        {
            get { return _memoryMode; }
            set { SetProperty(ref _memoryMode, value); }
        }

        public View ViewType
        {
            get { return _viewType; }
            set { SetProperty(ref _viewType, value); }
        }

        public ExecutionOptionsModel ExecutionOptions
        {
            get { return _executionOptions; }
            set { SetProperty(ref _executionOptions, value); }
        }


        /// <summary>
        /// Notifies the properties computed from the generate, upscale and extract models.
        /// </summary>
        private void NotifyBackendChanged()
        {
            NotifyPropertyChanged(nameof(GenerateModel));
            NotifyPropertyChanged(nameof(Backend));
            NotifyPropertyChanged(nameof(PipelineType));
            NotifyPropertyChanged(nameof(IsUpscaleOnly));
            NotifyPropertyChanged(nameof(IsExtractOnly));
        }


        public bool IsLoadRequired(PipelineModel pipeline)
        {
            return pipeline is null
                || pipeline.Device != _device
                || pipeline.DiffusionModel != _diffusionModel
                || pipeline.LanguageModel != _languageModel
                || pipeline.MemoryMode != _memoryMode
                || pipeline.ExecutionOptions != _executionOptions;
        }


        public bool IsReloadRequired(PipelineModel pipeline)
        {
            if (pipeline is null || pipeline.DiffusionModel != _diffusionModel || pipeline.LanguageModel != _languageModel)
                return false;

            // ProcessType, LoraAdapters and ControlNet are the only options that can be modified
            return pipeline.ViewType != _viewType
                || pipeline.ProcessType != _processType
                || pipeline.ControlNetModel != _controlNetModel
                || pipeline.LoraAdapterModel.HasChanged(_loraAdapterModel);
        }
    }
}
