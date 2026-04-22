using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TensorStack.Common.Common;
using TensorStack.WPF;
using TensorStack.WPF.Services;

namespace Amuse.App.Common
{
    public class AudioModel : BaseModel, IDownloadModel
    {
        private ModelStatusType _status;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Id { get; set; }
        public BackendType Backend { get; set; }
        public string Name { get; set; }
        public string Pipeline { get; set; } = "OnnxRuntime";
        public bool IsDefault { get; set; }
        public bool IsGated { get; set; }
        public ModelStatusType Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value); }
        }
        public string Link { get; set; }
        public AudioModelType Type { get; set; }
        public string Version { get; set; }
        public int MinLength { get; set; }
        public int MaxLength { get; set; }
        public string[] Prefixes { get; set; }
        public string[] UrlPaths { get; set; }

        [JsonIgnore]
        public string Path { get; set; }


        public void Initialize(string modelDirectory)
        {
            var isValid = false;
            var directory = System.IO.Path.Combine(modelDirectory, Name);
            var modelFiles = FileHelper.GetUrlFileMapping(UrlPaths, directory);
            if (modelFiles.Values.All(File.Exists))
            {
                isValid = true;
                Path = directory;
            }

            if (Status == ModelStatusType.Pending && isValid)
                Status = ModelStatusType.Installed;
            else if (Status == ModelStatusType.Installed && !isValid)
                Status = ModelStatusType.Pending;
            else if (Status == ModelStatusType.Downloading || Status == ModelStatusType.DownloadQueue || Status == ModelStatusType.DownloadFailed || Status == ModelStatusType.Verifying)
                Status = ModelStatusType.Pending;
        }


        public void Delete(string modelDirectory)
        {
            FileHelper.DeleteDirectory(System.IO.Path.GetDirectoryName(Path));
        }


        public string GetDirectory(string modelDirectory)
        {
            return System.IO.Path.GetDirectoryName(Path);
        }


        public async Task<bool> DownloadAsync(string modelDirectory)
        {
            var directory = System.IO.Path.Combine(modelDirectory, Name);
            if (await DialogService.DownloadAsync($"Download '{Name}' model?", UrlPaths, directory))
                Initialize(modelDirectory);

            return Status == ModelStatusType.Installed;
        }
    }
}
