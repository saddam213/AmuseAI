using Amuse.App.Views;
using Amuse.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class DiffusionModel : BaseModel, IGenerateModel
    {
        private BackendType _backend;
        private string _name;
        private string _template;
        private PipelineType _pipeline;
        private string _modelType = "Base";
        private string _variant;
        private VendorType[] _vendor;
        private DataType _baseType;
        private MediaType _mediaType;
        private ProcessType[] _processTypes;
        private View[] _viewFilter;
        private DiffusionCheckpointModel _checkpoint;
        private MemoryProfile[] _memoryProfile;
        private GenerateDefaultOptions _defaultOptions;
        private SizeOption[] _resolutions = [];
        private string _accessToken;
        private bool _isDefault;
        private string _link;
        private ModelStatusType _status;
        private MemoryMode? _userMemoryMode;
        private QualityMode? _userQualityMode;

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

        public string Template
        {
            get { return _template; }
            set { SetProperty(ref _template, value); }
        }

        public PipelineType Pipeline
        {
            get { return _pipeline; }
            set { SetProperty(ref _pipeline, value); }
        }

        public string ModelType
        {
            get { return _modelType; }
            set { SetProperty(ref _modelType, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Variant
        {
            get { return _variant; }
            set { SetProperty(ref _variant, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public VendorType[] Vendor
        {
            get { return _vendor; }
            set { SetProperty(ref _vendor, value); }
        }

        public DataType BaseType
        {
            get { return _baseType; }
            set { SetProperty(ref _baseType, value); }
        }

        public MediaType MediaType
        {
            get { return _mediaType; }
            set { SetProperty(ref _mediaType, value); }
        }

        public ProcessType[] ProcessTypes
        {
            get { return _processTypes; }
            set { SetProperty(ref _processTypes, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public View[] ViewFilter
        {
            get { return _viewFilter; }
            set { SetProperty(ref _viewFilter, value); }
        }

        public DiffusionCheckpointModel Checkpoint
        {
            get { return _checkpoint; }
            set { SetProperty(ref _checkpoint, value); }
        }

        public MemoryProfile[] MemoryProfile
        {
            get { return _memoryProfile; }
            set { SetProperty(ref _memoryProfile, value); }
        }

        public GenerateDefaultOptions DefaultOptions
        {
            get { return _defaultOptions; }
            set { SetProperty(ref _defaultOptions, value); }
        }

        public SizeOption[] Resolutions
        {
            get { return _resolutions; }
            set { SetProperty(ref _resolutions, value); }
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

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MemoryMode? UserMemoryMode
        {
            get { return _userMemoryMode; }
            set { SetProperty(ref _userMemoryMode, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public QualityMode? UserQualityMode
        {
            get { return _userQualityMode; }
            set { SetProperty(ref _userQualityMode, value); }
        }


        public void Initialize(Settings settings)
        {
            Status = GetModelStatus(settings);
        }


        public void Delete(Settings settings)
        {
            foreach (var component in Checkpoint.GetComponents())
            {
                if (component.Type == CheckpointType.Component)
                    continue;

                var resolvedPath = component.Resolve(settings, settings.DirectoryDiffusion);
                if (component.Type == CheckpointType.LocalFile || component.Type == CheckpointType.OnlineFile)
                    FileHelper.DeleteFile(resolvedPath);
                else if (component.Type == CheckpointType.LocalFolder || component.Type == CheckpointType.LocalFolder)
                    FileHelper.DeleteDirectory(resolvedPath);
            }
        }


        public string GetDirectory(Settings settings)
        {
            return System.IO.Path.Combine(settings.DirectoryDiffusion);
        }


        private ModelStatusType GetModelStatus(Settings settings)
        {
            if (Checkpoint == null)
                return ModelStatusType.Available;

            var isValid = Checkpoint.IsInstalled(settings.DirectoryDiffusion, settings.Components);
            if ((Status == ModelStatusType.Available || Status == ModelStatusType.Unknown) && isValid)
                return ModelStatusType.Installed;
            else if (Status == ModelStatusType.Installed && !isValid)
                return ModelStatusType.Unknown;
            else if (Status == ModelStatusType.Downloading || Status == ModelStatusType.DownloadQueue || Status == ModelStatusType.DownloadFailed)
                return ModelStatusType.Available;

            return Status;
        }


        public DiffusionModel DeepClone(int id)
        {
            return new DiffusionModel
            {
                Id = id,
                Backend = Backend,
                Name = Name,
                Template = Template,
                Pipeline = Pipeline,
                ModelType = ModelType,
                Variant = Variant,
                BaseType = BaseType,
                MediaType = MediaType,
                AccessToken = AccessToken,
                Link = Link,
                Vendor = Vendor?.ToArray(),
                Resolutions = Resolutions.Copy(),
                ViewFilter = ViewFilter?.ToArray(),
                Checkpoint = Checkpoint.DeepClone(),
                MemoryProfile = MemoryProfile.Copy(),
                ProcessTypes = ProcessTypes.ToArray(),
                DefaultOptions = DefaultOptions.DeepClone()
            };
        }


        public IEnumerable<CheckpointComponent> GetComponents()
        {
            return _checkpoint.GetComponents();
        }
    }
}
