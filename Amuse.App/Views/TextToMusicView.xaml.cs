using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Media.Audio;
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
        private AudioInputStream _referenceAudio;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextToMusicView"/> class.
        /// </summary>
        public TextToMusicView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IAudioService audioService, ILanguageService languageService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<TextToMusicView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, audioService, languageService, extractService, upscaleService, historyService, logger)
        {
            LyricTemplateCommand = new RelayCommand(AddLyricTemplate, CanAddLyricTemplate);
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.TextToMusic;
        public RelayCommand LyricTemplateCommand { get; }

        /// <summary>
        /// Gets or sets the reference audio that conditions the timbre and style of the music (ACE-Step), the tasks that start from a source audio are in the AudioToMusic view.
        /// </summary>
        public AudioInputStream ReferenceAudio
        {
            get { return _referenceAudio; }
            set { SetProperty(ref _referenceAudio, value); }
        }


        /// <summary>
        /// On View Open
        /// </summary>
        public override async Task OpenAsync(OpenViewArgs args = null)
        {
            await base.OpenAsync(args);
            if (!IsPipelineLoaded)
                ModelControl.SetPipeline(GenerateService.Pipeline);
        }


        /// <summary>
        /// On View Close
        /// </summary>
        public override async Task CloseAsync()
        {
            await ResultControl.StopAsync();
            await ReferenceAudioControl.StopAsync();
            await base.CloseAsync();
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
                var options = Options with { ReferenceAudio = GetReferenceAudio() };

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
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[TextToMusic] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.ExecutePipelineTitle, ex.Message);
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
                CancellationTokenSource = new CancellationTokenSource();

                AutomationProgress.Indeterminate(AppDefault.AutomationStarted);
                var cancellationToken = CancellationTokenSource.Token;
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Audio, MediaType.Text))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    //// Source
                    //if (!automationJob.InputTexts.IsNullOrEmpty())
                    //    Options.Prompt2 = automationJob.InputTexts[0].Text;

                    // Options
                    var options = automationJob.GenerateOptions with { ReferenceAudio = GetReferenceAudio() };

                    // Diffusion
                    var resultTensor = await ExecuteAudioDiffusionAsync(options);

                    // Result
                    ResultAudio = !AutomationOptions.IsHistoryEnabled
                        ? resultTensor
                        : await SaveHistoryAsync(resultTensor, options);

                    await automationJob.SaveAsync(ResultAudio);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"{AppDefault.Automation}: {automationJob.Id}/{automationJob.Count}");
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
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[TextToMusic] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.ExecuteAutomationTitle, ex.Message);
            }
            finally
            {
                Progress.Clear();
                AutomationProgress.Clear();
                IsAutomating = false;
                CancellationTokenSource?.Dispose();
                CancellationTokenSource = null;
            }
        }


        /// <summary>
        /// Gets the reference audio when the model takes one, null otherwise.
        /// </summary>
        private AudioInputStream GetReferenceAudio()
        {
            return InputControl.IsReferenceAudioSupported ? _referenceAudio : default;
        }


        /// <summary>
        /// Adds the song template of the model to the lyrics, after any lyrics that are already there.
        /// </summary>
        private void AddLyricTemplate()
        {
            var template = CurrentPipeline?.DiffusionModel?.DefaultOptions?.Lyrics;
            if (string.IsNullOrEmpty(template))
                return;

            Options.Prompt2 = string.IsNullOrWhiteSpace(Options.Prompt2)
                ? template
                : string.Concat(Options.Prompt2.TrimEnd(), Environment.NewLine, Environment.NewLine, template);
        }


        /// <summary>
        /// Determines whether the song template can be added.
        /// </summary>
        private bool CanAddLyricTemplate()
        {
            return Options != null;
        }


        /// <summary>
        /// Save history
        /// </summary>
        /// <param name="options">The options.</param>
        private async Task<AudioInputStream> SaveHistoryAsync(AudioInputStream audioStream, GenerateInputOptions options)
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
    }
}
