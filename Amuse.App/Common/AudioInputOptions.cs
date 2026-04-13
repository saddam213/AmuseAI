using TensorStack.TextGeneration.Common;
using TensorStack.TextGeneration.Pipelines.Whisper;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class AudioInputOptions : BaseModel
    {
        public int Seed { get; set; }

        //Supertonic
        public string VoiceStyle { get; set; } = "Female1";
        public int Steps { get; set; } = 10;
        public float Speed { get; set; } = 1.1f;
        public float SilenceDuration { get; set; } = 0.3f;


        //Whisper
        public LanguageType Language { get; set; } = LanguageType.EN;
        public TaskType Task { get; set; } = TaskType.Transcribe;
        public int MinLength { get; set; } = 20;
        public int MaxLength { get; set; } = 512;
        public int NoRepeatNgramSize { get; set; } = 3;
        public int Beams { get; set; } = 0;
        public int TopK { get; set; } = 50;
        public float TopP { get; set; } = 0.95f;
        public float Temperature { get; set; } = 1.0f;
        public float LengthPenalty { get; set; } = 1.0f;
        public EarlyStopping EarlyStopping { get; set; } = EarlyStopping.BestBeam;
        public int DiversityLength { get; set; } = 1;
        public int ChunkSize { get; set; } = 5;
    }
}
