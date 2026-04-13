using System.IO;
using System.Text.Json.Serialization;
using TensorStack.Common.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class LoraAdapterModel : BaseModel
    {
        private string _weights;
        private ModelStatusType _status;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Id { get; set; }
        public BackendType Backend { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }
        public string Path { get; set; }
        public string Weights
        {
            get { return _weights; }
            set { SetProperty(ref _weights, value); }
        }
        public string Pipeline { get; set; }
        public ModelSourceType Source { get; set; }
        public string[] Triggers { get; set; }
        public bool IsDefault { get; set; }
        public bool IsGated { get; set; }
        public ModelStatusType Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value); }
        }
        public string Link { get; set; }

        public void Initialize(string modelDirectory)
        {
            var isValid = false;
            if (Source == ModelSourceType.Folder)
                isValid = Directory.Exists(Path);
            else if (Source == ModelSourceType.SingleFile)
            {
                isValid = Utils.IsLoraAdapterInstalled(modelDirectory, Path, Weights);
            }
            else if (Source == ModelSourceType.HuggingFace)
            {
                isValid = Utils.IsLoraAdapterInstalled(modelDirectory, Path, Weights);
            }

            if (Status == ModelStatusType.Pending && isValid)
                Status = ModelStatusType.Installed;
            else if (Status == ModelStatusType.Installed && !isValid)
                Status = ModelStatusType.Pending;
            else if (Status == ModelStatusType.Downloading || Status == ModelStatusType.DownloadQueue || Status == ModelStatusType.DownloadFailed || Status == ModelStatusType.Verifying)
                Status = ModelStatusType.Pending;
        }
    }
}
