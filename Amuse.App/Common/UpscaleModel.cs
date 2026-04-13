using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.WPF;
using TensorStack.WPF.Services;

namespace Amuse.App.Common
{
    public class UpscaleModel : BaseModel
    {
        private ModelStatusType _status;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Id { get; set; }
        public BackendType Backend { get; set; }
        public string Name { get; set; }
        public bool IsDefault { get; set; }
        public bool IsGated { get; set; }
        public ModelStatusType Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value); }
        }
        public string Link { get; set; }
        public int Channels { get; set; } = 3;
        public int SampleSize { get; set; }
        public int ScaleFactor { get; set; } = 1;
        public int MemorySize { get; set; } = 2;
        public Normalization Normalization { get; set; } = Normalization.ZeroToOne;
        public Normalization OutputNormalization { get; set; } = Normalization.OneToOne;
        public UpscaleInputOptions DefaultOptions { get; set; }
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
                Path = modelFiles.Values.First(x => x.EndsWith(".onnx"));
            }

            if (Status == ModelStatusType.Pending && isValid)
                Status = ModelStatusType.Installed;
            else if (Status == ModelStatusType.Installed && !isValid)
                Status = ModelStatusType.Pending;
            else if (Status == ModelStatusType.Downloading || Status == ModelStatusType.DownloadQueue || Status == ModelStatusType.DownloadFailed || Status == ModelStatusType.Verifying)
                Status = ModelStatusType.Pending;
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
