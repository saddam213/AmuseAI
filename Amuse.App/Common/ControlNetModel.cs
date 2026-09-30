using Amuse.App.Views;
using Amuse.Common;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class ControlNetModel : BaseModel, IDownloadModel
    {
        private string _name;
        private PipelineType _pipeline;
        private bool _invert;
        private int _layerCount;
        private bool _disableProjections;
        private View[] _viewFilter;
        private CheckpointComponent _checkpoint;
        private ModelStatusType _status;
        private string _accessToken;
        private bool _isDefault;
        private string _link;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Id { get; set; }

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

        public CheckpointComponent Checkpoint
        {
            get { return _checkpoint; }
            set { SetProperty(ref _checkpoint, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Invert
        {
            get { return _invert; }
            set { SetProperty(ref _invert, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int LayerCount
        {
            get { return _layerCount; }
            set { SetProperty(ref _layerCount, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool DisableProjections
        {
            get { return _disableProjections; }
            set { SetProperty(ref _disableProjections, value); }
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
            var resolvedCheckpoint = Checkpoint.Resolve(settings, settings.DirectoryControlNet);
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
            var resolvedCheckpoint = Checkpoint.Resolve(settings, settings.DirectoryControlNet);
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

            var isValid = Checkpoint.IsInstalled(settings.DirectoryControlNet,  settings.Components);
            if ((Status == ModelStatusType.Available || Status == ModelStatusType.Unknown) && isValid)
                return ModelStatusType.Installed;
            else if (Status == ModelStatusType.Installed && !isValid)
                return ModelStatusType.Unknown;
            else if (Status == ModelStatusType.Downloading || Status == ModelStatusType.DownloadQueue || Status == ModelStatusType.DownloadFailed)
                return ModelStatusType.Available;

            return Status;
        }


        public ControlNetModel DeepClone(int id)
        {
            return new ControlNetModel
            {
                Id = id,
                Name = Name,
                Pipeline = Pipeline,
                AccessToken = AccessToken,
                Link = Link,
                ViewFilter = ViewFilter?.ToArray(),
                Invert = Invert,
                LayerCount = LayerCount,
                DisableProjections = DisableProjections,
                Checkpoint = Checkpoint.DeepClone()
            };
        }
    }
}
