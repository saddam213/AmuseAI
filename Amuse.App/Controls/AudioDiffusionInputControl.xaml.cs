using Amuse.App.Common;
using Amuse.App.Views;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for AudioDiffusionInputControl.xaml
    /// </summary>
    public partial class AudioDiffusionInputControl : BaseControl
    {
        private bool _isAudioInputEnabled;
        private SchedulerInputOptions[] _schedulers;
        private InputTabOption _selectedOption;
        private bool _isSpeechPipeline;
        private bool _isWhisperPipeline;
        private bool _isSchedulerSupported;
        private bool _isStrengthSupported;
        private bool _isNegativePromptSupported;

        public AudioDiffusionInputControl()
        {
            Durations = GetDurations();
            KeyScales = GetKeyScales();
            TimeSignatures = GetTimeSignatures();
            SeedCommand = new RelayCommand<bool>(GenerateSeed);
            AddTriggerWordCommand = new AsyncRelayCommand<string>(AddTriggerWordAsync);
            InitializeComponent();
        }

        public static readonly DependencyProperty PipelineProperty = DependencyProperty.Register(nameof(Pipeline), typeof(PipelineModel), typeof(AudioDiffusionInputControl), new PropertyMetadata<AudioDiffusionInputControl, PipelineModel>((c, o, n) => c.OnPipelineChanged(o, n)));
        public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(GenerateInputOptions), typeof(AudioDiffusionInputControl));
        public static readonly DependencyProperty AutomationOptionsProperty = DependencyProperty.Register(nameof(AutomationOptions), typeof(AutomationOptions), typeof(AudioDiffusionInputControl));
        public static readonly DependencyProperty IsExecutingProperty = DependencyProperty.Register(nameof(IsExecuting), typeof(bool), typeof(AudioDiffusionInputControl));
        public static readonly DependencyProperty IsAutomatingProperty = DependencyProperty.Register(nameof(IsAutomating), typeof(bool), typeof(AudioDiffusionInputControl));
        public static readonly DependencyProperty AutomationProgressProperty = DependencyProperty.Register(nameof(AutomationProgress), typeof(ProgressInfo), typeof(AudioDiffusionInputControl));
        public static readonly DependencyProperty IsMultipleResultProperty = DependencyProperty.Register(nameof(IsMultipleResult), typeof(bool), typeof(AudioDiffusionInputControl));

        public View ViewType { get; set; }
        public ProcessType ProcessType { get; set; }
        public RelayCommand<bool> SeedCommand { get; }
        public AsyncRelayCommand<string> AddTriggerWordCommand { get; }

        public PipelineModel Pipeline
        {
            get { return (PipelineModel)GetValue(PipelineProperty); }
            set { SetValue(PipelineProperty, value); }
        }

        public GenerateInputOptions Options
        {
            get { return (GenerateInputOptions)GetValue(OptionsProperty); }
            set { SetValue(OptionsProperty, value); }
        }

        public AutomationOptions AutomationOptions
        {
            get { return (AutomationOptions)GetValue(AutomationOptionsProperty); }
            set { SetValue(AutomationOptionsProperty, value); }
        }

        public ProgressInfo AutomationProgress
        {
            get { return (ProgressInfo)GetValue(AutomationProgressProperty); }
            set { SetValue(AutomationProgressProperty, value); }
        }

        public bool IsExecuting
        {
            get { return (bool)GetValue(IsExecutingProperty); }
            set { SetValue(IsExecutingProperty, value); }
        }

        public bool IsAutomating
        {
            get { return (bool)GetValue(IsAutomatingProperty); }
            set { SetValue(IsAutomatingProperty, value); }
        }

        public bool IsMultipleResult
        {
            get { return (bool)GetValue(IsMultipleResultProperty); }
            set { SetValue(IsMultipleResultProperty, value); }
        }

        public bool IsAudioInputEnabled
        {
            get { return _isAudioInputEnabled; }
            set { SetProperty(ref _isAudioInputEnabled, value); }
        }

        public SchedulerInputOptions[] Schedulers
        {
            get { return _schedulers; }
            set { SetProperty(ref _schedulers, value); }
        }

        public InputTabOption SelectedOption
        {
            get { return _selectedOption; }
            set { SetProperty(ref _selectedOption, value); }
        }

        public bool IsSpeechPipeline
        {
            get { return _isSpeechPipeline; }
            set { SetProperty(ref _isSpeechPipeline, value); }
        }

        public bool IsWhisperPipeline
        {
            get { return _isWhisperPipeline; }
            set { SetProperty(ref _isWhisperPipeline, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the scheduler and advanced options are used, audio.cpp and speech pipelines do not use them.
        /// </summary>
        public bool IsSchedulerSupported
        {
            get { return _isSchedulerSupported; }
            set { SetProperty(ref _isSchedulerSupported, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the strength is used, audio.cpp only uses it for Stable Audio input audio.
        /// </summary>
        public bool IsStrengthSupported
        {
            get { return _isStrengthSupported; }
            set { SetProperty(ref _isStrengthSupported, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the pipeline accepts a negative prompt, it is shown while guidance is above 1.
        /// </summary>
        public bool IsNegativePromptSupported
        {
            get { return _isNegativePromptSupported; }
            set { SetProperty(ref _isNegativePromptSupported, value); }
        }


        private Task OnPipelineChanged(PipelineModel oldPipeline, PipelineModel newPipeline)
        {
            if (newPipeline is null || newPipeline.DiffusionModel is null)
            {
                return Task.CompletedTask;
            }

            var oldModel = oldPipeline?.DiffusionModel;
            var oldOptions = oldModel?.DefaultOptions;
            var newModel = newPipeline?.DiffusionModel;
            var newOptions = newModel?.DefaultOptions;

            if (oldModel == newModel)
            {
                // TODO if has lora changed
                Options.LoraOptions = newPipeline.LoraAdapterModel?.Select(x => new LoraOptionModel { Name = x.Name, Key = x.Key, Strength = 1f }).ToList();
                return Task.CompletedTask;
            }

            var previousOptions = Options;
            Options = new GenerateInputOptions
            {
                // Keep
                Prompt = previousOptions?.Prompt,
                Prompt2 = previousOptions?.Prompt2,
                NegativePrompt = previousOptions?.NegativePrompt,
                Seed = previousOptions?.Seed ?? 0,
                LoraOptions = newPipeline.LoraAdapterModel?.Select(x => new LoraOptionModel { Name = x.Name, Key = x.Key, Strength = 1f }).ToList(),
                Strength = previousOptions?.Strength ?? newOptions.Strength,

                // Update
                Steps = newOptions.Steps,
                Steps2 = newOptions.Steps2,
                GuidanceScale = newOptions.GuidanceScale,
                GuidanceScale2 = newOptions.GuidanceScale2,
                IsVaeSlicingEnabled = newOptions.IsVaeSlicingEnabled,
                IsVaeTilingEnabled = newOptions.IsVaeTilingEnabled,

                Language = newOptions.Language,
                Task = newOptions.Task,

                // AceStep
                Duration = newOptions.Duration,
                Bpm = newOptions.BPM,
                Keyscale = null,
                TimeSignature = null,
                TrackName = null,
                Instruction = null,

                //Supertonic
                Speed = newOptions.Speed,
                SilenceDuration = newOptions.SilenceDuration,

                //Whisper
                MinLength = newOptions.MinLength,
                MaxLength = newOptions.MaxLength,
                Beams = newOptions.Beams,
                LengthPenalty = newOptions.LengthPenalty,
                NoRepeatNgramSize = newOptions.NoRepeatNgramSize,
                Temperature = newOptions.Temperature,
                TopK = newOptions.TopK,
                TopP = newOptions.TopP,
                ChunkSize = newOptions.ChunkSize,
                EarlyStopping = newOptions.EarlyStopping
            };

            var isAudioCpp = newModel.Backend == BackendType.AudioCpp;
            IsWhisperPipeline = newModel.Pipeline == PipelineType.WhisperPipeline;
            IsSpeechPipeline = newModel.Pipeline == PipelineType.SupertonicPipeline
                || newModel.Pipeline == PipelineType.VoxtralTTSPipeline
                || newModel.Pipeline == PipelineType.Qwen3TTSPipeline
                || newModel.Pipeline == PipelineType.ChatterboxPipeline
                || newModel.Pipeline == PipelineType.OmniVoicePipeline;
            IsSchedulerSupported = !isAudioCpp && !IsSpeechPipeline && !IsWhisperPipeline;
            IsStrengthSupported = !isAudioCpp || newModel.Pipeline == PipelineType.StableAudioPipeline;
            IsNegativePromptSupported = newModel.Pipeline == PipelineType.StableAudioPipeline
                || newModel.Pipeline == PipelineType.MossSoundEffectPipeline
                || newModel.Pipeline == PipelineType.LongCatAudioPipeline;

            //Schedulers
            if (!newOptions.Schedulers.IsNullOrEmpty())
            {
                Schedulers = newOptions.Schedulers.Copy();
                Options.SchedulerOptions = Schedulers.FirstOrDefault(x => x.Scheduler == newOptions.Scheduler);
            }

            // Automation
            AutomationOptions = new AutomationOptions
            {
                ViewType = ViewType
            };

            return Task.CompletedTask;
        }


        private void GenerateSeed(bool random)
        {
            Options.Seed = random ? 0 : Random.Shared.Next();
        }


        private Task AddTriggerWordAsync(string triggerWord)
        {
            if (string.IsNullOrEmpty(Options.Prompt))
            {
                Options.Prompt = triggerWord;
            }
            else
            {
                Options.Prompt += $", {triggerWord}";
            }
            return Task.CompletedTask;
        }
        public List<ComboBoxOption> Durations { get; set; }
        public List<ComboBoxOption> KeyScales { get; set; }
        public List<ComboBoxOption> Languages { get; set; }
        public List<ComboBoxOption> TimeSignatures { get; set; }


        private List<ComboBoxOption> GetDurations()
        {
            return
            [
                new ComboBoxOption { Label = "Auto", FloatValue = 0.0f },
                new ComboBoxOption { Label = "30 Seconds", FloatValue = 30.0f },
                new ComboBoxOption { Label = "1 Minute", FloatValue = 60.0f },
                new ComboBoxOption { Label = "2 Minute", FloatValue = 120.0f },
                new ComboBoxOption { Label = "3 Minute", FloatValue = 180.0f },
                new ComboBoxOption { Label = "4 Minute", FloatValue = 240.0f },
                new ComboBoxOption { Label = "5 Minute", FloatValue = 300.0f },
                new ComboBoxOption { Label = "6 Minute", FloatValue = 360.0f },
                new ComboBoxOption { Label = "7 Minute", FloatValue = 420.0f },
                new ComboBoxOption { Label = "8 Minute", FloatValue = 480.0f },
                new ComboBoxOption { Label = "9 Minute", FloatValue = 540.0f},
                new ComboBoxOption { Label = "10 Minute", FloatValue = 600.0f},
            ];
        }


        private List<ComboBoxOption> GetTimeSignatures()
        {
            return
            [
                new ComboBoxOption { Label = "Auto", Value = null },
                new ComboBoxOption { Label = "2/4 time (marches, polka)", Value = "2" },
                new ComboBoxOption { Label = "3/4 time (waltzes, ballads)", Value = "3" },
                new ComboBoxOption { Label = "4/4 time (pop, rock, hip-hop)", Value = "4" },
                new ComboBoxOption { Label = "6/8 time (compound time, folk dances)", Value = "6" },
            ];
        }






        private List<ComboBoxOption> GetKeyScales()
        {
            string[] notes = ["A", "B", "C", "D", "E", "F", "G"];
            string[] accidentals = ["", "#", "b", "♯", "♭"]; //  # empty + ASCII sharp/flat + Unicode sharp/flat
            string[] modes = ["major", "minor"];

            // Generate all valid keyscales: 7 notes × 5 accidentals × 2 modes = 70 combinations
            // Examples: "C major", "F# minor", "B♭ major"
            var results = new List<ComboBoxOption> { new ComboBoxOption { Label = "Auto", Value = null }, };
            foreach (var note in notes)
                foreach (var acc in accidentals)
                    foreach (var mode in modes)
                    {
                        var value = $"{note}{acc} {mode}";
                        results.Add(new ComboBoxOption { Label = value, Value = value });
                    }

            return results;
        }

    }


    public class ComboBoxOption
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public int IntValue { get; set; }
        public float FloatValue { get; set; }
    }

}
