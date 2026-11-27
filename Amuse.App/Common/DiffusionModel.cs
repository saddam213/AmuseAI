using Amuse.App.Views;
using Amuse.Common;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common;

namespace Amuse.App.Common
{
    public sealed class DiffusionModel : GenerateModelBase<DiffusionCheckpointModel>, IGenerateModel
    {
        private BackendType _backend;
        private string _name;
        private string _template;
        private PipelineType _pipeline;
        private string _modelType = "Base";
        private VendorType[] _vendor;
        private MediaType _mediaType;
        private ProcessType[] _processTypes;
        private View[] _viewFilter;
        private GenerateDefaultOptions _defaultOptions;
        private SizeOption[] _resolutions = [];
        private bool _isDefault;
        private bool _isNew;
        private bool _isRecommended;
        private string _link;
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

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public VendorType[] Vendor
        {
            get { return _vendor; }
            set { SetProperty(ref _vendor, value); }
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

        /// <summary>
        /// Gets the directory the checkpoint sets are installed in.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public override string GetDirectory(Settings settings)
        {
            return settings.DirectoryDiffusion;
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
                MediaType = MediaType,
                Link = Link,
                Metadata = Metadata?.DeepClone(),
                Vendor = Vendor?.ToArray(),
                Resolutions = Resolutions.Copy(),
                ViewFilter = ViewFilter?.ToArray(),
                Checkpoints = [.. Checkpoints.Select(x => x.DeepClone())],
                CurrentCheckpointType = CurrentCheckpointType,
                ProcessTypes = ProcessTypes.ToArray(),
                DefaultOptions = DefaultOptions.DeepClone()
            };
        }
    }
}
