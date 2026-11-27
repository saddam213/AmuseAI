using Amuse.App.Views;
using Amuse.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common;
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
        private CheckpointComponent _vae;
        private UpscaleInputOptions _defaultOptions;
        private ModelStatusType _status;
        private bool _isDefault;
        private string _link;
        private ModelMetadata _metadata;

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
            set { SetProperty(ref _pipeline, value); NotifyPropertyChanged(nameof(IsVideoUpscaler)); }
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

        /// <summary>
        /// Gets or sets the VAE checkpoint, used by video upscalers such as SeedVR2.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent Vae
        {
            get { return _vae; }
            set { SetProperty(ref _vae, value); }
        }

        /// <summary>
        /// Gets a value indicating whether this is a video upscaler, SeedVR2 or RelayVSR.
        /// </summary>
        [JsonIgnore]
        public bool IsVideoUpscaler => _pipeline == PipelineType.SeedVR2Pipeline || _pipeline == PipelineType.RelayVSRPipeline;

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

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ModelMetadata Metadata
        {
            get { return _metadata; }
            set { SetProperty(ref _metadata, value); }
        }


        public void Initialize(Settings settings)
        {
            _metadata?.Initialize(settings);
            Status = GetModelStatus(settings);
        }


        public void Delete(Settings settings)
        {
            DeleteCheckpoint(settings, Vae);
            DeleteCheckpoint(settings, Checkpoint);
        }


        /// <summary>
        /// Gets the model checkpoints, shared components are downloaded separately.
        /// </summary>
        public IEnumerable<CheckpointComponent> GetComponents()
        {
            if (Checkpoint is not null)
                yield return Checkpoint;
            if (Vae is not null)
                yield return Vae;
        }


        /// <summary>
        /// Deletes the files of a checkpoint, from the model directory and from the model search folders, shared components are kept for other models.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="checkpoint">The checkpoint.</param>
        private static void DeleteCheckpoint(Settings settings, CheckpointComponent checkpoint)
        {
            if (checkpoint == null || checkpoint.Type == CheckpointType.Component)
                return;

            checkpoint.DeleteFiles(settings, settings.DirectoryUpscale);
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

            var isValid = Checkpoint.IsInstalled(settings, settings.DirectoryUpscale)
                && (Vae == null || Vae.IsInstalled(settings, settings.DirectoryUpscale));
            if ((Status == ModelStatusType.Available || Status == ModelStatusType.Unknown) && isValid)
                return ModelStatusType.Installed;
            else if (Status == ModelStatusType.Installed && !isValid)
                return ModelStatusType.Unknown;

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
                Vae = Vae?.DeepClone(),
                DefaultOptions = DefaultOptions.DeepClone(),
                MemorySize = MemorySize,
                Link = Link,
                Metadata = Metadata?.DeepClone()
            };
        }
    }
}
