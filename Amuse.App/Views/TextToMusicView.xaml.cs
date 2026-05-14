using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Audio;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for TextToMusicView.xaml
    /// </summary>
    public partial class TextToMusicView : ViewBaseDiffusion
    {
        private AudioInputStream _sourceAudio;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextToMusicView"/> class.
        /// </summary>
        public TextToMusicView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IDiffusionService diffusionService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<TextToMusicView> logger)
            : base(settings, navigationService, environmentService, downloadService, diffusionService, extractService, upscaleService, historyService, logger)
        {
            AppendLyricCommand = new RelayCommand<string>(AppendLyric, CanAppendLyric);
            AppendLyricExampleCommand = new RelayCommand<string>(AppendLyricExample, CanAppendLyric);
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.TextToMusic;
        public RelayCommand<string> AppendLyricCommand { get; }
        public RelayCommand<string> AppendLyricExampleCommand { get; }

        /// <summary>
        /// Gets or sets the source audio.
        /// </summary>
        public AudioInputStream SourceAudio
        {
            get { return _sourceAudio; }
            set { SetProperty(ref _sourceAudio, value); }
        }


        /// <summary>
        /// On View Open
        /// </summary>
        public override async Task OpenAsync(OpenViewArgs args = null)
        {
            await base.OpenAsync(args);
            if (!IsPipelineLoaded)
                ModelControl.SetPipeline(DiffusionService.Pipeline);
        }


        /// <summary>
        /// Execute thge pipeline.
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation($"[TextToMusic] [Execute] Executing pipeline...");

            try
            {
                Progress.Clear();
                Statistics.Clear();
                ResultAudio = default;
                Statistics.Start();


                // Options
                var options = Options with { };
                if (SourceAudio != null)
                {
                    options.InputAudios = [await SourceAudio.GetAsync(channels:2)];
                }

                // Execute
                var resultTensor = await ExecuteAudioDiffusionAsync(options);

                // Result
                Statistics.Stop();
                ResultAudio = await SaveHistoryAsync(resultTensor, options);

                Logger.LogInformation("[TextToMusic] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[TextToMusic] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[TextToMusic] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Execute Pipeline", ex.Message);
            }
            finally
            {
                Progress.Clear();
            }
        }


        /// <summary>
        /// Executes the pipeline automation.
        /// </summary>
        protected override async Task ExecuteAutomationAsync()
        {
            IsAutomating = true;
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation($"[TextToMusic] [ExecuteAutomation] Executing pipeline...");

            try
            {
                Progress.Clear();
                AutomationProgress.Clear();
                Statistics.Clear();
                ResultAudio = default;
                Statistics.Start();

                AutomationProgress.Indeterminate($"Automation Started");
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Audio, MediaType.Text))
                {
                    // Source
                    //if (!automationJob.InputAudios.IsNullOrEmpty())
                    //    SourceAudio = automationJob.InputAudios[0];

                    // Diffusion
                    var resultTensor = await ExecuteAudioDiffusionAsync(automationJob.DiffusionOptions);

                    // Result
                    ResultAudio = resultTensor;

                    // History
                    if (AutomationOptions.IsHistoryEnabled)
                    {
                        //await SaveHistoryAsync(automationJob.DiffusionOptions);
                    }

                    //await automationJob.SaveAsync(ResultAudio);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"Automation: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[TextToMusic] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[TextToMusic] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = DiffusionService.IsLoaded;
                Logger.LogError(ex, "[TextToMusic] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Execute Automation", ex.Message);
            }
            finally
            {
                Progress.Clear();
                AutomationProgress.Clear();
                IsAutomating = false;
            }
        }


        private void AppendLyric(string lyric)
        {
            if (string.IsNullOrWhiteSpace(Options.Prompt2))
                Options.Prompt2 = $"{lyric}\n";
            else
            {
                Options.Prompt2 += $"\n{lyric}\n";
            }
        }


        private bool CanAppendLyric(string lyric)
        {
            return Options != null;
        }

        private void AppendLyricExample(string example)
        {
            if (!ExamplePrompts.ContainsKey(example))
                return;

            if (example.StartsWith("Prompt"))
            {
                Options.Prompt = ExamplePrompts[example];
            }
            else
            {
                Options.Prompt2 = ExamplePrompts[example];
            }
        }


        /// <summary>
        /// Save history
        /// </summary>
        /// <param name="options">The options.</param>
        private async Task<AudioInputStream> SaveHistoryAsync(AudioInputStream audioStream, DiffusionInputOptions options)
        {
            Logger.LogInformation($"[TextToMusic] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(audioStream, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                Source = View.TextToMusic,
            });
            Logger.LogInformation($"[TextToMusic] [SaveHistory] History saved.");
            return result;
        }


        private static Dictionary<string, string> ExamplePrompts = new Dictionary<string, string>
        {

            {"Prompt1","power pop, catchy melody, male vocals, drums, bass, high energy, bright" },
            {"Prompt2","80s synthwave, driving synth-pop, cinematic, energetic, electric drums, female vocals, high energy, punchy production" },
            {"Prompt3","melancholic piano ballad with soft female vocals, gentle string accompaniment, slow tempo, intimate and heartbreaking atmosphere" },

            {"Lyrics1",@"[Intro]

[Verse]
Woke up early, sun is shining bright,
Got no worries, feeling just right.

[Chorus]
We're dancing on the edge of the world,
With every flag we have unfurled!

[Bridge]
No one can stop us now.

[Chorus]
We're dancing on the edge of the world,
With every flag we have unfurled!

[Outro]" },
            {"Lyrics2",@"[Intro]

[Verse]
Neon lights cut through the haze,
Lost within the digital maze.

[Chorus]
Driving faster through the night,
Everything feels so right.

[Bridge]
The city is asleep, but we are alive.

[Chorus]
Driving faster through the night,
Everything feels so right.

[Outro]" },
            {"Lyrics3",@"[Intro]

[Verse 1]
Walking through the empty streets
Thinking of your gentle touch
Summer nights and softer dreams

[Chorus]
We rise together
Into the light
This is our moment tonight

[Verse 2]
Stars are falling from the sky
Your hand fits perfectly in mine

[Bridge]
If tomorrow never comes
At least we had this

[Chorus]
We rise together
Into the light
This is our moment tonight

[Outro]" },
        };




    }
}
