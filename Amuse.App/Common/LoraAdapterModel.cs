using Amuse.App.Views;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class LoraAdapterModel : BaseModel, IDownloadModel
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

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public View[] ViewFilter { get; set; }
        public bool IsGated { get; set; }
        public ModelStatusType Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value); }
        }
        public string Link { get; set; }


        public void Initialize(string modelDirectory)
        {
            Status = HuggingFace.ModelStatus(this, modelDirectory);
        }


        public void Delete(string modelDirectory)
        {
            HuggingFace.ModelDelete(this, modelDirectory);
        }


        public string GetDirectory(string modelDirectory)
        {
            return HuggingFace.ModelDirectory(this, modelDirectory);
        }


        public LoraAdapterModel DeepClone(int id)
        {
            return new LoraAdapterModel
            {
                Id = id,
                Name = Name,
                Key = Key,
                Path = Path,
                Pipeline = Pipeline,
                Weights = Weights,
                Triggers = Triggers?.ToArray(),
                Source = Source,
                Backend = Backend,
                IsGated = IsGated,
                Link = Link,
                ViewFilter = ViewFilter?.ToArray(),
            };
        }
    }
}
