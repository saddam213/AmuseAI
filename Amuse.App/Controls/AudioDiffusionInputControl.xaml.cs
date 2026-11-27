using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Views;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TensorStack.Common;
using TensorStack.Media.Audio;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Dialogs;
using TensorStack.WPF.Services;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for AudioDiffusionInputControl.xaml
    /// </summary>
    public partial class AudioDiffusionInputControl : BaseControl
    {
        private const string TaskCover = "cover";
        private const string TaskCoverNoFsq = "cover-nofsq";
        private const string TaskRepaint = "repaint";
        private const string TaskLego = "lego";
        private const string TaskExtract = "extract";
        private const string TaskComplete = "complete";
        private const string TaskInitAudio = "init_audio";
        private const string TaskInpaintAudio = "inpaint_audio";
        private static readonly HashSet<string> SourceAudioTasks = [TaskCover, TaskCoverNoFsq, TaskRepaint, TaskLego, TaskExtract, TaskComplete, TaskInitAudio, TaskInpaintAudio];
        private bool _isAudioInputEnabled;
        private InputTabOption _selectedOption;
        private bool _isSpeechPipeline;
        private bool _isStrengthSupported;
        private bool _isNegativePromptSupported;
        private string[] _tasks = [];
        private string _taskDescription;
        private bool _isReferenceAudioSupported;
        private bool _isRangeSupported;
        private bool _isTrackNameSupported;
        private bool _isDurationSupported;
        private bool _isReferenceTextSupported;
        private bool _isReferenceTextRequired;
        private bool _isInstructionSupported;
        private bool _isEmotionSupported;
        private string _rangeText;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioDiffusionInputControl"/> class.
        /// </summary>
        public AudioDiffusionInputControl()
        {
            Durations = GetDurations();
            KeyScales = GetKeyScales();
            TimeSignatures = GetTimeSignatures();
            Emotions = GetEmotions();
            SeedCommand = new RelayCommand<bool>(GenerateSeed);
            AddTriggerWordCommand = new AsyncRelayCommand<string>(AddTriggerWordAsync);
            SelectRangeCommand = new AsyncRelayCommand(SelectRangeAsync, CanSelectRange);
            InitializeComponent();
        }

        public static readonly DependencyProperty SourceAudioProperty = DependencyProperty.Register(nameof(SourceAudio), typeof(AudioInputStream), typeof(AudioDiffusionInputControl), new PropertyMetadata<AudioDiffusionInputControl, AudioInputStream>((c, n) => c.OnSourceAudioChanged(n)));

        public static readonly DependencyProperty PipelineProperty = DependencyProperty.Register(nameof(Pipeline), typeof(PipelineModel), typeof(AudioDiffusionInputControl), new PropertyMetadata<AudioDiffusionInputControl, PipelineModel>((c, o, n) => c.OnPipelineChanged(o, n)));
        public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(GenerateInputOptions), typeof(AudioDiffusionInputControl));
        public static readonly DependencyProperty AutomationOptionsProperty = DependencyProperty.Register(nameof(AutomationOptions), typeof(AutomationOptions), typeof(AudioDiffusionInputControl));
        public static readonly DependencyProperty IsExecutingProperty = DependencyProperty.Register(nameof(IsExecuting), typeof(bool), typeof(AudioDiffusionInputControl));
        public static readonly DependencyProperty IsAutomatingProperty = DependencyProperty.Register(nameof(IsAutomating), typeof(bool), typeof(AudioDiffusionInputControl));
        public static readonly DependencyProperty AutomationProgressProperty = DependencyProperty.Register(nameof(AutomationProgress), typeof(ProgressInfo), typeof(AudioDiffusionInputControl));

        public View ViewType { get; set; }
        public ProcessType ProcessType { get; set; }
        public RelayCommand<bool> SeedCommand { get; }
        public AsyncRelayCommand<string> AddTriggerWordCommand { get; }
        public AsyncRelayCommand SelectRangeCommand { get; }

        /// <summary>
        /// Gets or sets the source audio of the view, the range of it a task regenerates is chosen in the crop dialog.
        /// </summary>
        public AudioInputStream SourceAudio
        {
            get { return (AudioInputStream)GetValue(SourceAudioProperty); }
            set { SetValue(SourceAudioProperty, value); }
        }

        public string RangeText
        {
            get { return _rangeText; }
            set { SetProperty(ref _rangeText, value); }
        }

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

        public bool IsAudioInputEnabled
        {
            get { return _isAudioInputEnabled; }
            set { SetProperty(ref _isAudioInputEnabled, value); }
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

        /// <summary>
        /// Gets or sets the tasks of the pipeline this view runs: the music views split them into tasks with and without a source audio.
        /// </summary>
        public string[] Tasks
        {
            get { return _tasks; }
            set { SetProperty(ref _tasks, value); }
        }

        public string TaskDescription
        {
            get { return _taskDescription; }
            set { SetProperty(ref _taskDescription, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a reference audio conditions the timbre and style of the music (ACE-Step).
        /// </summary>
        public bool IsReferenceAudioSupported
        {
            get { return _isReferenceAudioSupported; }
            set { SetProperty(ref _isReferenceAudioSupported, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the task regenerates a range of the source audio and needs one (ACE-Step repaint, Stable Audio inpaint audio).
        /// </summary>
        public bool IsRangeSupported
        {
            get { return _isRangeSupported; }
            set { SetProperty(ref _isRangeSupported, value); }
        }

        public bool IsTrackNameSupported
        {
            get { return _isTrackNameSupported; }
            set { SetProperty(ref _isTrackNameSupported, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the task takes a duration, the other source audio tasks keep the length of the source.
        /// </summary>
        public bool IsDurationSupported
        {
            get { return _isDurationSupported; }
            set { SetProperty(ref _isDurationSupported, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether voice cloning takes the transcript of the voice reference (Fish Audio, Higgs Audio, OmniVoice, VoxMel), VoxCPM2 clones from the audio alone.
        /// </summary>
        public bool IsReferenceTextSupported
        {
            get { return _isReferenceTextSupported; }
            set { SetProperty(ref _isReferenceTextSupported, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether voice cloning needs the transcript of the voice reference (Fish Audio, OmniVoice, VoxMel).
        /// </summary>
        public bool IsReferenceTextRequired
        {
            get { return _isReferenceTextRequired; }
            set { SetProperty(ref _isReferenceTextRequired, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the speech takes a voice or style instruction (OmniVoice, Qwen3 TTS).
        /// </summary>
        public bool IsInstructionSupported
        {
            get { return _isInstructionSupported; }
            set { SetProperty(ref _isInstructionSupported, value); }
        }

        public bool IsEmotionSupported
        {
            get { return _isEmotionSupported; }
            set { SetProperty(ref _isEmotionSupported, value); }
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
                Options.LoraOptions = CreateLoraOptions(newPipeline, Options.LoraOptions);
                return Task.CompletedTask;
            }

            Tasks = GetTasks(newModel);
            var previousOptions = Options;
            Options = new GenerateInputOptions
            {
                // Keep
                Prompt = previousOptions?.Prompt,
                Prompt2 = previousOptions?.Prompt2,
                NegativePrompt = previousOptions?.NegativePrompt,
                Seed = previousOptions?.Seed ?? 0,
                LoraOptions = CreateLoraOptions(newPipeline, previousOptions?.LoraOptions),
                Strength = previousOptions?.Strength ?? newOptions.Strength,

                // Update
                Steps = newOptions.Steps,
                Steps2 = newOptions.Steps2,
                GuidanceScale = newOptions.GuidanceScale,
                GuidanceScale2 = newOptions.GuidanceScale2,
                IsVaeSlicingEnabled = newOptions.IsVaeSlicingEnabled,
                IsVaeTilingEnabled = newOptions.IsVaeTilingEnabled,

                Language = newOptions.Language,
                Task = Tasks.Contains(newOptions.Task) ? newOptions.Task : Tasks.FirstOrDefault(),

                // AceStep
                Duration = newOptions.Duration,
                Bpm = ViewType == View.AudioToMusic ? 0 : newOptions.BPM,
                Keyscale = null,
                TimeSignature = null,
                TrackName = null,
                Instruction = null,

                //Supertonic
                Speed = newOptions.Speed,
                SilenceDuration = newOptions.SilenceDuration,

                //Speech recognition
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
            IsSpeechPipeline = newModel.Pipeline == PipelineType.SupertonicPipeline
                || newModel.Pipeline == PipelineType.VoxtralTTSPipeline
                || newModel.Pipeline == PipelineType.Qwen3TTSPipeline
                || newModel.Pipeline == PipelineType.ChatterboxPipeline
                || newModel.Pipeline == PipelineType.OmniVoicePipeline
                || newModel.Pipeline == PipelineType.FishAudioPipeline
                || newModel.Pipeline == PipelineType.HiggsAudioTTSPipeline
                || newModel.Pipeline == PipelineType.MagpieTTSPipeline
                || newModel.Pipeline == PipelineType.NeuTTSPipeline
                || newModel.Pipeline == PipelineType.TinyVocaPipeline
                || newModel.Pipeline == PipelineType.VoxCPMPipeline
                || newModel.Pipeline == PipelineType.VoxMelPipeline;
            IsNegativePromptSupported = newModel.Pipeline == PipelineType.StableAudioPipeline
                || newModel.Pipeline == PipelineType.MossSoundEffectPipeline;
            IsReferenceTextRequired = isAudioCpp && (newModel.Pipeline == PipelineType.FishAudioPipeline || newModel.Pipeline == PipelineType.OmniVoicePipeline || newModel.Pipeline == PipelineType.VoxMelPipeline);
            IsReferenceTextSupported = IsReferenceTextRequired || (isAudioCpp && newModel.Pipeline == PipelineType.HiggsAudioTTSPipeline);
            IsInstructionSupported = isAudioCpp && ViewType == View.TextToAudio && (newModel.Pipeline == PipelineType.OmniVoicePipeline || newModel.Pipeline == PipelineType.Qwen3TTSPipeline);
            IsEmotionSupported = isAudioCpp && ViewType == View.TextToAudio && newModel.Pipeline == PipelineType.NeuTTSPipeline;

            if (previousOptions != null)
                previousOptions.PropertyChanged -= OnOptionsPropertyChanged;

            Options.PropertyChanged += OnOptionsPropertyChanged;
            UpdateTaskSupport();
            UpdateRangeText();

            // Automation
            AutomationOptions = new AutomationOptions
            {
                ViewType = ViewType
            };

            return Task.CompletedTask;
        }


        /// <summary>
        /// Called when an option changes, the task decides which of the task inputs are shown.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void OnOptionsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GenerateInputOptions.Task))
                UpdateTaskSupport();
            else if (e.PropertyName == nameof(GenerateInputOptions.HasRange))
                UpdateRangeText();
        }


        /// <summary>
        /// Called when the source audio changes, a range chosen in the previous audio does not apply to the new one.
        /// </summary>
        /// <param name="sourceAudio">The source audio.</param>
        private Task OnSourceAudioChanged(AudioInputStream sourceAudio)
        {
            ClearRange();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Selects the range of the source audio the task regenerates, the current range is selected when the dialog opens.
        /// </summary>
        private async Task SelectRangeAsync()
        {
            var range = await DialogService.SelectAudioRangeAsync(SourceAudio, new CropAudioOptions
            {
                Range = Options.HasRange ? new AudioRange(Options.RangeStart, Options.RangeEnd) : default
            });
            if (range == null)
                return;

            Options.RangeStart = (float)range.Start;
            Options.RangeEnd = (float)range.End;
        }


        /// <summary>
        /// Determines whether a range can be selected, it is a range of the source audio.
        /// </summary>
        private bool CanSelectRange()
        {
            return Options != null && SourceAudio != null;
        }


        /// <summary>
        /// Clears the range.
        /// </summary>
        private void ClearRange()
        {
            if (Options == null)
                return;

            Options.RangeStart = 0;
            Options.RangeEnd = 0;
        }


        /// <summary>
        /// Updates the range text, the start and end of the range in seconds.
        /// </summary>
        private void UpdateRangeText()
        {
            RangeText = Options?.HasRange == true
                ? string.Format(AppDefault.AudioRangeFormat, Options.RangeStart, Options.RangeEnd)
                : AppDefault.AudioRangeNotSet;
        }


        /// <summary>
        /// Updates the inputs the selected task uses: the source audio range, track name, duration and strength.
        /// ACE-Step and Stable Audio take a source audio in the AudioToMusic view, the TextToMusic view generates from text with an optional ACE-Step reference audio.
        /// </summary>
        private void UpdateTaskSupport()
        {
            var model = Pipeline?.DiffusionModel;
            var task = Options?.Task;
            var isAudioCpp = model?.Backend == BackendType.AudioCpp;
            var isAceStep = isAudioCpp && model.Pipeline == PipelineType.AceStepPipeline;
            var isStableAudio = isAudioCpp && model.Pipeline == PipelineType.StableAudioPipeline;
            var isSourceAudioView = ViewType == View.AudioToMusic;
            var isAceStepEdit = isSourceAudioView && isAceStep;
            var isStableAudioEdit = isSourceAudioView && isStableAudio;

            IsReferenceAudioSupported = isAceStep;
            IsRangeSupported = (isAceStepEdit && task == TaskRepaint) || (isStableAudioEdit && task == TaskInpaintAudio);
            IsTrackNameSupported = isAceStepEdit && (task == TaskLego || task == TaskExtract || task == TaskComplete);
            IsDurationSupported = isAceStepEdit && task == TaskComplete;
            IsStrengthSupported = !isAudioCpp
                || (isStableAudioEdit && task == TaskInitAudio)
                || (isAceStepEdit && (task == TaskCover || task == TaskCoverNoFsq || task == TaskRepaint || task == TaskLego));
            TaskDescription = isSourceAudioView ? GetTaskDescription(task) : default;
        }


        /// <summary>
        /// Gets the tasks of the model for this view, the AudioToMusic view runs the tasks that take a source audio and the TextToMusic view the others.
        /// </summary>
        /// <param name="model">The model.</param>
        private string[] GetTasks(DiffusionModel model)
        {
            var tasks = model.DefaultOptions?.Tasks ?? [];
            if (ViewType == View.AudioToMusic)
                return tasks.Where(SourceAudioTasks.Contains).ToArray();
            if (ViewType == View.TextToMusic)
                return tasks.Where(x => !SourceAudioTasks.Contains(x)).ToArray();

            return tasks;
        }


        /// <summary>
        /// Gets the description of a task that takes a source audio.
        /// </summary>
        /// <param name="task">The task.</param>
        private static string GetTaskDescription(string task)
        {
            return task switch
            {
                TaskCover => AppDefault.MusicTaskCover,
                TaskCoverNoFsq => AppDefault.MusicTaskCoverNoFsq,
                TaskRepaint => AppDefault.MusicTaskRepaint,
                TaskLego => AppDefault.MusicTaskLego,
                TaskExtract => AppDefault.MusicTaskExtract,
                TaskComplete => AppDefault.MusicTaskComplete,
                TaskInitAudio => AppDefault.MusicTaskInitAudio,
                TaskInpaintAudio => AppDefault.MusicTaskInpaintAudio,
                _ => default
            };
        }


        /// <summary>
        /// Creates the LoRA options for the pipeline, keeping the strength of adapters that are still selected.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="previousOptions">The previous LoRA options.</param>
        private static List<LoraOptionModel> CreateLoraOptions(PipelineModel pipeline, List<LoraOptionModel> previousOptions)
        {
            return pipeline.LoraAdapterModel?
                .Select(x => new LoraOptionModel
                {
                    Name = x.Name,
                    Key = x.Key,
                    Strength = previousOptions?.FirstOrDefault(p => p.Key == x.Key)?.Strength ?? 1f
                })
                .ToList();
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
        public List<ComboBoxOption> Emotions { get; set; }


        private List<ComboBoxOption> GetDurations()
        {
            return
            [
                new ComboBoxOption { Label = AppDefault.Auto, FloatValue = 0.0f },
                new ComboBoxOption { Label = string.Format(AppDefault.DurationSecondsFormat, 30), FloatValue = 30.0f },
                new ComboBoxOption { Label = AppDefault.DurationOneMinute, FloatValue = 60.0f },
                new ComboBoxOption { Label = string.Format(AppDefault.DurationMinutesFormat, 2), FloatValue = 120.0f },
                new ComboBoxOption { Label = string.Format(AppDefault.DurationMinutesFormat, 3), FloatValue = 180.0f },
                new ComboBoxOption { Label = string.Format(AppDefault.DurationMinutesFormat, 4), FloatValue = 240.0f },
                new ComboBoxOption { Label = string.Format(AppDefault.DurationMinutesFormat, 5), FloatValue = 300.0f },
                new ComboBoxOption { Label = string.Format(AppDefault.DurationMinutesFormat, 6), FloatValue = 360.0f },
                new ComboBoxOption { Label = string.Format(AppDefault.DurationMinutesFormat, 7), FloatValue = 420.0f },
                new ComboBoxOption { Label = string.Format(AppDefault.DurationMinutesFormat, 8), FloatValue = 480.0f },
                new ComboBoxOption { Label = string.Format(AppDefault.DurationMinutesFormat, 9), FloatValue = 540.0f},
                new ComboBoxOption { Label = string.Format(AppDefault.DurationMinutesFormat, 10), FloatValue = 600.0f},
            ];
        }


        /// <summary>
        /// Gets the speech emotions (NeuTTS), auto leaves the emotion to the model.
        /// </summary>
        private List<ComboBoxOption> GetEmotions()
        {
            return
            [
                new ComboBoxOption { Label = AppDefault.Auto, Value = null },
                new ComboBoxOption { Label = AppDefault.EmotionNeutral, Value = "neutral" },
                new ComboBoxOption { Label = AppDefault.EmotionHappy, Value = "happy" },
                new ComboBoxOption { Label = AppDefault.EmotionSad, Value = "sad" },
                new ComboBoxOption { Label = AppDefault.EmotionAngry, Value = "angry" },
                new ComboBoxOption { Label = AppDefault.EmotionFearful, Value = "fearful" },
                new ComboBoxOption { Label = AppDefault.EmotionDisgusted, Value = "disgusted" },
                new ComboBoxOption { Label = AppDefault.EmotionSurprised, Value = "surprised" },
            ];
        }


        private List<ComboBoxOption> GetTimeSignatures()
        {
            return
            [
                new ComboBoxOption { Label = AppDefault.Auto, Value = null },
                new ComboBoxOption { Label = AppDefault.TimeSignature24, Value = "2" },
                new ComboBoxOption { Label = AppDefault.TimeSignature34, Value = "3" },
                new ComboBoxOption { Label = AppDefault.TimeSignature44, Value = "4" },
                new ComboBoxOption { Label = AppDefault.TimeSignature68, Value = "6" },
            ];
        }






        private List<ComboBoxOption> GetKeyScales()
        {
            string[] notes = ["A", "B", "C", "D", "E", "F", "G"];
            string[] accidentals = ["", "#", "b", "♯", "♭"]; //  # empty + ASCII sharp/flat + Unicode sharp/flat
            string[] modes = ["major", "minor"];
            string[] modeLabels = [AppDefault.KeyScaleMajor, AppDefault.KeyScaleMinor];

            // Generate all valid keyscales: 7 notes × 5 accidentals × 2 modes = 70 combinations
            // Examples: "C major", "F# minor", "B♭ major"
            var results = new List<ComboBoxOption> { new ComboBoxOption { Label = AppDefault.Auto, Value = null }, };
            foreach (var note in notes)
                foreach (var acc in accidentals)
                    for (int i = 0; i < modes.Length; i++)
                    {
                        var value = $"{note}{acc} {modes[i]}";
                        results.Add(new ComboBoxOption { Label = string.Format(AppDefault.KeyScaleFormat, $"{note}{acc}", modeLabels[i]), Value = value });
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
