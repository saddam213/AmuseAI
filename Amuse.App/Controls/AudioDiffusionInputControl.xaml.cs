using Amuse.App.Common;
using Amuse.App.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TensorStack.Python.Common;
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
        private DiffusionInputOption _selectedOption;

        public AudioDiffusionInputControl()
        {
            Durations = GetDurations();
            KeyScales = GetKeyScales();
            Languages = GetLanguages();
            TimeSignatures = GetTimeSignatures();
            SeedCommand = new RelayCommand<bool>(GenerateSeed);
            AddTriggerWordCommand = new AsyncRelayCommand<string>(AddTriggerWordAsync);
            InitializeComponent();
        }

        public static readonly DependencyProperty PipelineProperty = DependencyProperty.Register(nameof(Pipeline), typeof(PipelineModel), typeof(AudioDiffusionInputControl), new PropertyMetadata<AudioDiffusionInputControl, PipelineModel>((c, o, n) => c.OnPipelineChanged(o, n)));
        public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(DiffusionInputOptions), typeof(AudioDiffusionInputControl));
        public static readonly DependencyProperty AutomationOptionsProperty = DependencyProperty.Register(nameof(AutomationOptions), typeof(AutomationOptions), typeof(AudioDiffusionInputControl));
        public static readonly DependencyProperty IsExecutingProperty = DependencyProperty.Register(nameof(IsExecuting), typeof(bool), typeof(AudioDiffusionInputControl));
        public static readonly DependencyProperty IsAutomatingProperty = DependencyProperty.Register(nameof(IsAutomating), typeof(bool), typeof(AudioDiffusionInputControl));
        public static readonly DependencyProperty AutomationProgressProperty = DependencyProperty.Register(nameof(AutomationProgress), typeof(ProgressInfo), typeof(AudioDiffusionInputControl));

        public View ViewType { get; set; }
        public ProcessType ProcessType { get; set; }
        public RelayCommand<bool> SeedCommand { get; }
        public AsyncRelayCommand<string> AddTriggerWordCommand { get; }

        public PipelineModel Pipeline
        {
            get { return (PipelineModel)GetValue(PipelineProperty); }
            set { SetValue(PipelineProperty, value); }
        }

        public DiffusionInputOptions Options
        {
            get { return (DiffusionInputOptions)GetValue(OptionsProperty); }
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

        public SchedulerInputOptions[] Schedulers
        {
            get { return _schedulers; }
            set { SetProperty(ref _schedulers, value); }
        }

        public DiffusionInputOption SelectedOption
        {
            get { return _selectedOption; }
            set { SetProperty(ref _selectedOption, value); }
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
            Options = new DiffusionInputOptions
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

                Task = "text2music", // "text2music"`, `"cover"`, `"repaint"`, `"extract"`, `"lego"`
                Duration = 0,
                Bpm = 80,
                VocalLanguage = "unknown",
                Keyscale = null,
                TimeSignature = null,
                TrackName = null,
                Instruction = null,
            };

            //Schedulers
            Schedulers = newOptions.Schedulers.GetSchedulers().Select(SchedulerInputOptions.Create).ToArray();
            Options.SchedulerOptions = Schedulers.FirstOrDefault(x => x.Scheduler == newOptions.Scheduler);

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


        private List<ComboBoxOption> GetLanguages()
        {
            return
        [
            new ComboBoxOption { Label = "Auto", Value = "unknown" },
            new ComboBoxOption { Label = "English", Value = "en" },
            new ComboBoxOption { Label = "Korean (한국어)", Value = "ko" },
            new ComboBoxOption { Label = "Japanese (日本語)", Value = "ja" },
            new ComboBoxOption { Label = "Chinese (中文)", Value = "zh" },
            new ComboBoxOption { Label = "Cantonese (粵語)", Value = "yue" },
            new ComboBoxOption { Label = "Arabic", Value = "ar" },
            new ComboBoxOption { Label = "Azerbaijani", Value = "az" },
            new ComboBoxOption { Label = "Bulgarian", Value = "bg" },
            new ComboBoxOption { Label = "Bengali", Value = "bn" },
            new ComboBoxOption { Label = "Catalan", Value = "ca" },
            new ComboBoxOption { Label = "Czech", Value = "cs" },
            new ComboBoxOption { Label = "Danish", Value = "da" },
            new ComboBoxOption { Label = "German", Value = "de" },
            new ComboBoxOption { Label = "Greek", Value = "el" },
            new ComboBoxOption { Label = "Spanish", Value = "es" },
            new ComboBoxOption { Label = "Persian", Value = "fa" },
            new ComboBoxOption { Label = "Finnish", Value = "fi" },
            new ComboBoxOption { Label = "French", Value = "fr" },
            new ComboBoxOption { Label = "Hebrew", Value = "he" },
            new ComboBoxOption { Label = "Hindi", Value = "hi" },
            new ComboBoxOption { Label = "Croatian", Value = "hr" },
            new ComboBoxOption { Label = "Haitian Creole", Value = "ht" },
            new ComboBoxOption { Label = "Hungarian", Value = "hu" },
            new ComboBoxOption { Label = "Indonesian", Value = "id" },
            new ComboBoxOption { Label = "Icelandic", Value = "is" },
            new ComboBoxOption { Label = "Italian", Value = "it" },
            new ComboBoxOption { Label = "Latin", Value = "la" },
            new ComboBoxOption { Label = "Lithuanian", Value = "lt" },
            new ComboBoxOption { Label = "Malay", Value = "ms" },
            new ComboBoxOption { Label = "Nepali", Value = "ne" },
            new ComboBoxOption { Label = "Dutch", Value = "nl" },
            new ComboBoxOption { Label = "Norwegian", Value = "no" },
            new ComboBoxOption { Label = "Punjabi", Value = "pa" },
            new ComboBoxOption { Label = "Polish", Value = "pl" },
            new ComboBoxOption { Label = "Portuguese", Value = "pt" },
            new ComboBoxOption { Label = "Romanian", Value = "ro" },
            new ComboBoxOption { Label = "Russian", Value = "ru" },
            new ComboBoxOption { Label = "Sanskrit", Value = "sa" },
            new ComboBoxOption { Label = "Slovak", Value = "sk" },
            new ComboBoxOption { Label = "Serbian", Value = "sr" },
            new ComboBoxOption { Label = "Swedish", Value = "sv" },
            new ComboBoxOption { Label = "Swahili", Value = "sw" },
            new ComboBoxOption { Label = "Tamil", Value = "ta" },
            new ComboBoxOption { Label = "Telugu", Value = "te" },
            new ComboBoxOption { Label = "Thai", Value = "th" },
            new ComboBoxOption { Label = "Tagalog", Value = "tl" },
            new ComboBoxOption { Label = "Turkish", Value = "tr" },
            new ComboBoxOption { Label = "Ukrainian", Value = "uk" },
            new ComboBoxOption { Label = "Urdu", Value = "ur" },
            new ComboBoxOption { Label = "Vietnamese", Value = "vi" }
        ];
        }

    }


    public class ComboBoxOption
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public int IntValue { get; set; }
        public float FloatValue { get; set; }
    }

    public enum AudioDiffusionInputOption
    {
        Options = 0,
        Advanced = 1,
        Automation = 2,
    }
}
