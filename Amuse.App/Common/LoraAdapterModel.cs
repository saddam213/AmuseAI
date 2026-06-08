using Amuse.App.Views;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class LoraAdapterModel : BaseModel, IDownloadModel
    {
        private BackendType _backend;
        private string _name;
        private PipelineType _pipeline;
        private string _key;
        private string[] _triggers;
        private View[] _viewFilter;
        private CheckpointComponent _checkpoint;
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

        public string Key
        {
            get { return _key; }
            set { SetProperty(ref _key, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[] Triggers
        {
            get { return _triggers; }
            set { SetProperty(ref _triggers, value); }
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
            var resolvedCheckpoint = Checkpoint.Resolve(settings.DirectoryLoraAdapter, settings.Components);
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
            var resolvedCheckpoint = Checkpoint.Resolve(settings.DirectoryLoraAdapter, settings.Components);
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

            var isValid = Checkpoint.IsInstalled(settings.DirectoryLoraAdapter, settings.Components);
            if (Status == ModelStatusType.Available && isValid)
                return ModelStatusType.Installed;
            else if (Status == ModelStatusType.Installed && !isValid)
                return ModelStatusType.Available;
            else if (Status == ModelStatusType.Downloading || Status == ModelStatusType.DownloadQueue || Status == ModelStatusType.DownloadFailed)
                return ModelStatusType.Available;

            return Status;
        }


        public LoraAdapterModel DeepClone(int id)
        {
            return new LoraAdapterModel
            {
                Id = id,
                Backend = Backend,
                Name = Name,
                Pipeline = Pipeline,
                Key = Key,
                Triggers = Triggers?.ToArray(),
                ViewFilter = ViewFilter?.ToArray(),
                Checkpoint = Checkpoint.DeepClone(),
                AccessToken = AccessToken,
                Link = Link
            };
        }
    }
}
