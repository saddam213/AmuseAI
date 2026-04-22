using Amuse.App.Views;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class ControlNetModel : BaseModel, IDownloadModel
    {
        private ModelStatusType _status;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Id { get; set; }
        public BackendType Backend { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public ModelSourceType Source { get; set; }
        public string Pipeline { get; set; }
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


        public ControlNetModel DeepClone(int id)
        {
            return new ControlNetModel
            {
                Id = id,
                Name = Name,
                Path = Path,
                Pipeline = Pipeline,
                Backend = Backend,
                Source = Source,
                IsGated = IsGated,
                Link = Link,
                ViewFilter = ViewFilter?.ToArray()
            };
        }
    }
}
