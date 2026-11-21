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
    public sealed class LanguageModel : BaseModel, IGenerateModel
    {
        private BackendType _backend;
        private string _name;
        private string _template;
        private PipelineType _pipeline;
        private string _modelType = "Base";
        private int _modelSize;
        private double _modelParams;
        private string _variant;
        private VendorType[] _vendor;
        private DataType _baseType;
        private MediaType _mediaType;
        private ProcessType[] _processTypes;
        private View[] _viewFilter;
        private LanguageCheckpointModel _checkpoint;
        private float _memorySize;
        private GenerateDefaultOptions _defaultOptions;
        private string _accessToken;
        private bool _isDefault;
        private bool _isNew;
        private bool _isRecommended;
        private string _link;
        private ModelStatusType _status;
        private MemoryMode? _userMemoryMode;
        private ExecutionOptionsModel _userExecutionOptions;

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

        public int ModelSize
        {
            get { return _modelSize; }
            set { SetProperty(ref _modelSize, value); }
        }

        public double ModelParams
        {
            get { return _modelParams; }
            set { SetProperty(ref _modelParams, value); }
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

        public LanguageCheckpointModel Checkpoint
        {
            get { return _checkpoint; }
            set { SetProperty(ref _checkpoint, value); }
        }

        public float MemorySize
        {
            get { return _memorySize; }
            set { SetProperty(ref _memorySize, value); }
        }

        public GenerateDefaultOptions DefaultOptions
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

        /// <summary>
        /// Gets or sets a value indicating whether this model is new, shown on the Home view.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsNew
        {
            get { return _isNew; }
            set { SetProperty(ref _isNew, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this model is recommended, shown on the Home view.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsRecommended
        {
            get { return _isRecommended; }
            set { SetProperty(ref _isRecommended, value); }
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
        public ExecutionOptionsModel UserExecutionOptions
        {
            get { return _userExecutionOptions; }
            set { SetProperty(ref _userExecutionOptions, value); }
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

                var resolvedPath = component.Resolve(settings, settings.DirectoryLangaugeModel);
                if (component.Type == CheckpointType.LocalFile || component.Type == CheckpointType.OnlineFile)
                    FileHelper.DeleteFile(resolvedPath);
                else if (component.Type == CheckpointType.LocalFolder || component.Type == CheckpointType.LocalFolder)
                    FileHelper.DeleteDirectory(resolvedPath);
            }
        }


        public string GetDirectory(Settings settings)
        {
            return System.IO.Path.Combine(settings.DirectoryLangaugeModel);
        }


        private ModelStatusType GetModelStatus(Settings settings)
        {
            if (Checkpoint == null)
                return ModelStatusType.Available;

            var isValid = Checkpoint.IsInstalled(settings.DirectoryLangaugeModel, settings.Components);
            if (Status == ModelStatusType.Available && isValid)
                return ModelStatusType.Installed;
            else if (Status == ModelStatusType.Installed && !isValid)
                return ModelStatusType.Unknown;
            else if (Status == ModelStatusType.Downloading || Status == ModelStatusType.DownloadQueue || Status == ModelStatusType.DownloadFailed)
                return ModelStatusType.Available;

            return Status;
        }


        public LanguageModel DeepClone(int id)
        {
            return new LanguageModel
            {
                Id = id,
                Backend = Backend,
                Name = Name,
                Template = Template,
                Pipeline = Pipeline,
                ModelType = ModelType,
                ModelSize = ModelSize,
                ModelParams = ModelParams,
                Variant = Variant,
                BaseType = BaseType,
                MediaType = MediaType,
                AccessToken = AccessToken,
                Link = Link,
                Vendor = Vendor?.ToArray(),
                ViewFilter = ViewFilter?.ToArray(),
                Checkpoint = Checkpoint.DeepClone(),
                MemorySize = MemorySize,
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
