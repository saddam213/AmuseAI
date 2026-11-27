using System.Text.Json.Serialization;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class ComponentModel : BaseModel, IDownloadModel
    {
        private string _name;
        private string _key;
        private string _link;
        private ModelMetadata _metadata;
        private ModelStatusType _status;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Id { get; set; }

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        public string Key
        {
            get { return _key; }
            set { SetProperty(ref _key, value); }
        }

        public CheckpointComponent Checkpoint { get; set; }

        public ModelStatusType Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value); }
        }

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
            Checkpoint?.DeleteFiles(settings, null);
        }


        public string GetDirectory(Settings settings)
        {
            var resolvedCheckpoint = Checkpoint.Resolve(settings, null);
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

            var isValid = Checkpoint.IsInstalled(settings, settings.DirectoryModel);
            if ((Status == ModelStatusType.Available || Status == ModelStatusType.Unknown) && isValid)
                return ModelStatusType.Installed;
            else if (Status == ModelStatusType.Installed && !isValid)
                return ModelStatusType.Unknown;

            return Status;
        }


        public ComponentModel DeepClone(int id)
        {
            return new ComponentModel
            {
                Id = id,
                Name = Name,
                Key = Key,
                Link = Link,
                Metadata = Metadata?.DeepClone(),
                Checkpoint = Checkpoint.DeepClone()
            };
        }
    }
}
