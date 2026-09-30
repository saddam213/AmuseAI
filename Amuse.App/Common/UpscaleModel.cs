using Amuse.App.Views;
using Amuse.Common;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class UpscaleModel : BaseModel, IDownloadModel
    {
        private BackendType _backend;
        private string _name;
        private PipelineType _pipeline;
        private View[] _viewFilter;
        private int _channels = 3;
        private int _sampleSize;
        private int _scaleFactor = 1;
        private float _memorySize = 2;
        private Normalization _normalization = Normalization.ZeroToOne;
        private Normalization _outputNormalization = Normalization.OneToOne;
        private CheckpointComponent _checkpoint;
        private UpscaleInputOptions _defaultOptions;
        private ModelStatusType _status;
        private string _accessToken;
        private bool _isDefault;
        private string _link;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Id { get; set; }

        public BackendType Backend
        {
            get { return _backend; }
            set { SetProperty(ref _backend, value); }
        }

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        public PipelineType Pipeline
        {
            get { return _pipeline; }
            set { SetProperty(ref _pipeline, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public View[] ViewFilter
        {
            get { return _viewFilter; }
            set { SetProperty(ref _viewFilter, value); }
        }

        public int Channels
        {
            get { return _channels; }
            set { SetProperty(ref _channels, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int SampleSize
        {
            get { return _sampleSize; }
            set { SetProperty(ref _sampleSize, value); }
        }

        public int ScaleFactor
        {
            get { return _scaleFactor; }
            set { SetProperty(ref _scaleFactor, value); }
        }

        public float MemorySize
        {
            get { return _memorySize; }
            set { SetProperty(ref _memorySize, value); }
        }

        public Normalization Normalization
        {
            get { return _normalization; }
            set { SetProperty(ref _normalization, value); }
        }

        public Normalization OutputNormalization
        {
            get { return _outputNormalization; }
            set { SetProperty(ref _outputNormalization, value); }
        }

        public CheckpointComponent Checkpoint
        {
            get { return _checkpoint; }
            set { SetProperty(ref _checkpoint, value); }
        }

        public UpscaleInputOptions DefaultOptions
        {
            get { return _defaultOptions; }
            set { SetProperty(ref _defaultOptions, value); }
        }

        public ModelStatusType Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string AccessToken
        {
            get { return _accessToken; }
            set { SetProperty(ref _accessToken, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsDefault
        {
            get { return _isDefault; }
            set { SetProperty(ref _isDefault, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Link
        {
            get { return _link; }
            set { SetProperty(ref _link, value); }
        }


        public void Initialize(Settings settings)
        {
            Status = GetModelStatus(settings);
        }


        public void Delete(Settings settings)
        {
            var resolvedCheckpoint = Checkpoint.Resolve(settings, settings.DirectoryUpscale);
            if (string.IsNullOrEmpty(resolvedCheckpoint))
                return;

            if (System.IO.File.Exists(resolvedCheckpoint))
            {
                FileHelper.DeleteFile(resolvedCheckpoint);
                FileHelper.DeleteDirectory(System.IO.Path.GetDirectoryName(resolvedCheckpoint), false);
            }
            else if (System.IO.Directory.Exists(resolvedCheckpoint))
            {
                FileHelper.DeleteDirectory(resolvedCheckpoint);
            }
        }


        public string GetDirectory(Settings settings)
        {
            var resolvedCheckpoint = Checkpoint.Resolve(settings, settings.DirectoryUpscale);
            if (string.IsNullOrEmpty(resolvedCheckpoint))
                return null;

            if (System.IO.Directory.Exists(resolvedCheckpoint))
                return resolvedCheckpoint;

            return System.IO.Path.GetDirectoryName(resolvedCheckpoint);
        }


        private ModelStatusType GetModelStatus(Settings settings)
        {
            if (Checkpoint == null)
                return ModelStatusType.Available;

            var isValid = Checkpoint.IsInstalled(settings.DirectoryUpscale, settings.Components);
            if ((Status == ModelStatusType.Available || Status == ModelStatusType.Unknown) && isValid)
                return ModelStatusType.Installed;
            else if (Status == ModelStatusType.Installed && !isValid)
                return ModelStatusType.Unknown;
            else if (Status == ModelStatusType.Downloading || Status == ModelStatusType.DownloadQueue || Status == ModelStatusType.DownloadFailed)
                return ModelStatusType.Available;

            return Status;
        }


        public UpscaleModel DeepClone(int id)
        {
            return new UpscaleModel
            {
                Id = id,
                Backend = Backend,
                Name = Name,
                Pipeline = Pipeline,
                Channels = Channels,
                Normalization = Normalization,
                OutputNormalization = OutputNormalization,
                SampleSize = SampleSize,
                ScaleFactor = ScaleFactor,
                ViewFilter = ViewFilter?.ToArray(),
                Checkpoint = Checkpoint.DeepClone(),
                DefaultOptions = DefaultOptions.DeepClone(),
                MemorySize = MemorySize,
                AccessToken = AccessToken,
                Link = Link
            };
        }
    }
}
