using System.Text.Json.Serialization;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class DiffusionCheckpointModel : BaseModel
    {
        private string _connectors;
        private string _vocoder;
        private string _audioVae;
        private string _vae;
        private string _transformer2;
        private string _transformer;
        private string _textEncoder3;
        private string _textEncoder2;
        private string _textEncoder;
        private string _singleFile;


        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string SingleFile
        {
            get { return _singleFile; }
            set { SetProperty(ref _singleFile, value == string.Empty ? null : value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string TextEncoder
        {
            get { return _textEncoder; }
            set { SetProperty(ref _textEncoder, value == string.Empty ? null : value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string TextEncoder2
        {
            get { return _textEncoder2; }
            set { SetProperty(ref _textEncoder2, value == string.Empty ? null : value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string TextEncoder3
        {
            get { return _textEncoder3; }
            set { SetProperty(ref _textEncoder3, value == string.Empty ? null : value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Transformer
        {
            get { return _transformer; }
            set { SetProperty(ref _transformer, value == string.Empty ? null : value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Transformer2
        {
            get { return _transformer2; }
            set { SetProperty(ref _transformer2, value == string.Empty ? null : value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Vae
        {
            get { return _vae; }
            set { SetProperty(ref _vae, value == string.Empty ? null : value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string AudioVae
        {
            get { return _audioVae; }
            set { SetProperty(ref _audioVae, value == string.Empty ? null : value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Vocoder
        {
            get { return _vocoder; }
            set { SetProperty(ref _vocoder, value == string.Empty ? null : value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Connectors
        {
            get { return _connectors; }
            set { SetProperty(ref _connectors, value == string.Empty ? null : value); }
        }
    }
}
