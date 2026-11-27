using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class LanguageCheckpointModel : BaseModel, ICheckpointModel
    {
        private DataType _type;
        private float _memorySize;
        private bool _isInstalled;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DataType Type
        {
            get { return _type; }
            set { SetProperty(ref _type, value); NotifyPropertyChanged(nameof(QualityDescription)); }
        }

        [JsonIgnore]
        public string QualityDescription => _type.GetQualityDescription();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float MemorySize
        {
            get { return _memorySize; }
            set { SetProperty(ref _memorySize, value); }
        }

        [JsonIgnore]
        public bool IsInstalled
        {
            get { return _isInstalled; }
            private set { SetProperty(ref _isInstalled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent TextModel { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent TextModel2 { get; set; }


        public bool IsValid()
        {
            foreach (var component in GetComponents())
            {
                if (!component.IsValid())
                    return false;
            }
            return true;
        }


        /// <summary>
        /// Checks the files of every component are installed and updates <see cref="IsInstalled"/>.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="modelDirectory">The model directory.</param>
        public bool CheckInstall(Settings settings, string modelDirectory)
        {
            IsInstalled = GetComponents().All(x => x.IsInstalled(settings, modelDirectory));
            return _isInstalled;
        }


        public IEnumerable<CheckpointComponent> GetComponents()
        {
            if (TextModel != null) yield return TextModel;
            if (TextModel2 != null) yield return TextModel2;
        }


        public LanguageCheckpointModel DeepClone()
        {
            return new LanguageCheckpointModel
            {
                Type = Type,
                MemorySize = MemorySize,
                TextModel = TextModel?.DeepClone(),
                TextModel2 = TextModel2?.DeepClone()
            };
        }

    }
}
