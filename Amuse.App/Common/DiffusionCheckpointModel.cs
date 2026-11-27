using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class DiffusionCheckpointModel : BaseModel, ICheckpointModel
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
        public CheckpointComponent Compute { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent Tokenizer { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent TextEncoder { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent TextEncoder2 { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent TextEncoder3 { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent Unet { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent Transformer { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent Transformer2 { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent Vae { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent AudioVae { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent Vocoder { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent Connectors { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent LatentUpsampler { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent LatentUpsamplerTemporal { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent ConditionEncoder { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent AudioTokenizer { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CheckpointComponent AudioDetokenizer { get; set; }


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
            if (Compute != null) yield return Compute;
            if (Tokenizer != null) yield return Tokenizer;
            if (TextEncoder != null) yield return TextEncoder;
            if (TextEncoder2 != null) yield return TextEncoder2;
            if (TextEncoder3 != null) yield return TextEncoder3;
            if (Unet != null) yield return Unet;
            if (Transformer != null) yield return Transformer;
            if (Transformer2 != null) yield return Transformer2;
            if (Vae != null) yield return Vae;
            if (AudioVae != null) yield return AudioVae;
            if (Vocoder != null) yield return Vocoder;
            if (Connectors != null) yield return Connectors;
            if (LatentUpsampler != null) yield return LatentUpsampler;
            if (LatentUpsamplerTemporal != null) yield return LatentUpsamplerTemporal;
            if (ConditionEncoder != null) yield return ConditionEncoder;
            if (AudioTokenizer != null) yield return AudioTokenizer;
            if (AudioDetokenizer != null) yield return AudioDetokenizer;
        }


        public DiffusionCheckpointModel DeepClone()
        {
            return new DiffusionCheckpointModel
            {
                Type = Type,
                MemorySize = MemorySize,
                Compute = Compute?.DeepClone(),
                Tokenizer = Tokenizer?.DeepClone(),
                TextEncoder = TextEncoder?.DeepClone(),
                TextEncoder2 = TextEncoder2?.DeepClone(),
                TextEncoder3 = TextEncoder3?.DeepClone(),
                Unet = Unet?.DeepClone(),
                Transformer = Transformer?.DeepClone(),
                Transformer2 = Transformer2?.DeepClone(),
                Vae = Vae?.DeepClone(),
                AudioVae = AudioVae?.DeepClone(),
                Vocoder = Vocoder?.DeepClone(),
                Connectors = Connectors?.DeepClone(),
                LatentUpsampler = LatentUpsampler?.DeepClone(),
                LatentUpsamplerTemporal = LatentUpsamplerTemporal?.DeepClone(),
                ConditionEncoder = ConditionEncoder?.DeepClone(),
                AudioTokenizer = AudioTokenizer?.DeepClone(),
                AudioDetokenizer = AudioDetokenizer?.DeepClone()
            };
        }

    }
}
