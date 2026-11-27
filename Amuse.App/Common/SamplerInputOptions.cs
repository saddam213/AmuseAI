using Amuse.Common;
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TensorStack.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed record SamplerInputOptions : BaseRecord
    {
        private SamplerType _sampler;
        private SchedulerType _scheduler;
        private float _flowShift;
        private float _eta;
        private float _imageGuidance;
        private int _shiftedTimestep;
        private string _sigmas;
        private string _extraArgs;
        private string _skipLayers;
        private float _skipLayerScale;
        private float _skipLayerStart;
        private float _skipLayerEnd;

        public SamplerType Sampler
        {
            get { return _sampler; }
            set { SetProperty(ref _sampler, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public SchedulerType Scheduler
        {
            get { return _scheduler; }
            set { SetProperty(ref _scheduler, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float FlowShift
        {
            get { return _flowShift; }
            set { SetProperty(ref _flowShift, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float Eta
        {
            get { return _eta; }
            set { SetProperty(ref _eta, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float ImageGuidance
        {
            get { return _imageGuidance; }
            set { SetProperty(ref _imageGuidance, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ShiftedTimestep
        {
            get { return _shiftedTimestep; }
            set { SetProperty(ref _shiftedTimestep, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Sigmas
        {
            get { return _sigmas; }
            set { SetProperty(ref _sigmas, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string ExtraArgs
        {
            get { return _extraArgs; }
            set { SetProperty(ref _extraArgs, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string SkipLayers
        {
            get { return _skipLayers; }
            set { SetProperty(ref _skipLayers, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float SkipLayerScale
        {
            get { return _skipLayerScale; }
            set { SetProperty(ref _skipLayerScale, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float SkipLayerStart
        {
            get { return _skipLayerStart; }
            set { SetProperty(ref _skipLayerStart, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float SkipLayerEnd
        {
            get { return _skipLayerEnd; }
            set { SetProperty(ref _skipLayerEnd, value); }
        }


        /// <summary>
        /// Creates the options sent to the backend host.
        /// </summary>
        /// <returns>SamplerOptions.</returns>
        public SamplerOptions ToClientOptions()
        {
            return new SamplerOptions
            {
                Sampler = Sampler,
                Scheduler = Scheduler,
                FlowShift = FlowShift,
                Eta = Eta,
                ImageGuidance = ImageGuidance,
                ShiftedTimestep = ShiftedTimestep,
                Sigmas = ParseFloats(Sigmas),
                ExtraArgs = ExtraArgs,
                SkipLayers = ParseInts(SkipLayers),
                SkipLayerScale = SkipLayerScale,
                SkipLayerStart = SkipLayerStart,
                SkipLayerEnd = SkipLayerEnd
            };
        }


        /// <summary>
        /// Parses a comma separated list of numbers, an empty or invalid list is not sent.
        /// </summary>
        /// <param name="text">The list text.</param>
        /// <returns>The values, or null.</returns>
        private static float[] ParseFloats(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var tokens = text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var values = new float[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                    return null;
            }
            return values;
        }


        /// <summary>
        /// Parses a comma separated list of whole numbers, an empty or invalid list is not sent.
        /// </summary>
        /// <param name="text">The list text.</param>
        /// <returns>The values, or null.</returns>
        private static int[] ParseInts(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var tokens = text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var values = new int[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
                    return null;
            }
            return values;
        }


        /// <summary>
        /// Returns the scheduler display name.
        /// </summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
            return _sampler.GetDisplayName();
        }

        public bool Equals(SamplerInputOptions other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
