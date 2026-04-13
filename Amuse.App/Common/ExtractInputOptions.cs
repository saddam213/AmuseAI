using System.Text.Json.Serialization;
using TensorStack.Extractors.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class ExtractInputOptions : BaseModel
    {
        private bool _isTileEnabled;
        private int tileSize;
        private int tileOverlap;
        private bool isInverted;
        private bool mergeInput;
        private bool isTransparent;
        private BackgroundMode mode;
        private int detections;
        private float bodyConfidence;
        private float jointConfidence;
        private float colorAlpha;
        private float jointRadius;
        private float boneRadius;
        private float boneThickness;
        private bool isTileSupported;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsTileEnabled
        {
            get { return _isTileEnabled; }
            set { SetProperty(ref _isTileEnabled, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int TileSize
        {
            get { return tileSize; }
            set { SetProperty(ref tileSize, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int TileOverlap
        {
            get { return tileOverlap; }
            set { SetProperty(ref tileOverlap, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsInverted
        {
            get { return isInverted; }
            set { SetProperty(ref isInverted, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool MergeInput
        {
            get { return mergeInput; }
            set { SetProperty(ref mergeInput, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsTransparent
        {
            get { return isTransparent; }
            set { SetProperty(ref isTransparent, value); }
        }


        // Background
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public BackgroundMode Mode
        {
            get { return mode; }
            set { SetProperty(ref mode, value); }
        }


        // Pose
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Detections
        {
            get { return detections; }
            set { SetProperty(ref detections, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float BodyConfidence
        {
            get { return bodyConfidence; }
            set { SetProperty(ref bodyConfidence, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float JointConfidence
        {
            get { return jointConfidence; }
            set { SetProperty(ref jointConfidence, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float ColorAlpha
        {
            get { return colorAlpha; }
            set { SetProperty(ref colorAlpha, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float JointRadius
        {
            get { return jointRadius; }
            set { SetProperty(ref jointRadius, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float BoneRadius
        {
            get { return boneRadius; }
            set { SetProperty(ref boneRadius, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float BoneThickness
        {
            get { return boneThickness; }
            set { SetProperty(ref boneThickness, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsTileSupported
        {
            get { return isTileSupported; }
            set { SetProperty(ref isTileSupported, value); }
        }
    }
}
