using TensorStack.Python.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class PipelineModel : BaseModel
    {
        private DeviceModel _device;
        private DiffusionModel _diffusionModel;
        private ControlNetModel _controlNetModel;
        private ExtractModel _extractModel;
        private LoraAdapterModel[] _loraAdapterModel;
        private UpscaleModel _upscaleModel;
        private AudioModel _audioModel;
        private MemoryMode _memoryMode;
        private ProcessType _processType;
        private QualityMode _qualityMode;

        public DeviceModel Device
        {
            get { return _device; }
            set { SetProperty(ref _device, value); }
        }

        public DiffusionModel DiffusionModel
        {
            get { return _diffusionModel; }
            set { SetProperty(ref _diffusionModel, value); }
        }

        public ControlNetModel ControlNetModel
        {
            get { return _controlNetModel; }
            set { SetProperty(ref _controlNetModel, value); }
        }

        public ExtractModel ExtractModel
        {
            get { return _extractModel; }
            set { SetProperty(ref _extractModel, value); }
        }

        public LoraAdapterModel[] LoraAdapterModel
        {
            get { return _loraAdapterModel; }
            set { SetProperty(ref _loraAdapterModel, value); }
        }

        public UpscaleModel UpscaleModel
        {
            get { return _upscaleModel; }
            set { SetProperty(ref _upscaleModel, value); }
        }

        public AudioModel AudioModel
        {
            get { return _audioModel; }
            set { SetProperty(ref _audioModel, value); }
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

        public QualityMode QualityMode
        {
            get { return _qualityMode; }
            set { SetProperty(ref _qualityMode, value); }
        }


        public bool IsLoadRequired(PipelineModel pipeline)
        {
            return pipeline is null
                || pipeline.Device != _device
                || pipeline.DiffusionModel != _diffusionModel
                || pipeline.MemoryMode != _memoryMode
                || pipeline.QualityMode != _qualityMode;
        }


        public bool IsReloadRequired(PipelineModel pipeline)
        {
            if (pipeline is null || pipeline.DiffusionModel != _diffusionModel)
                return false;

            // ProcessType, LoraAdapters and ControlNet are the only options that can be modified
            return pipeline.ProcessType != _processType
                || pipeline.ControlNetModel != _controlNetModel
                || pipeline.LoraAdapterModel.HasChanged(_loraAdapterModel);
        }
    }
}
