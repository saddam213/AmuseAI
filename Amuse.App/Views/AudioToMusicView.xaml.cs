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
using TensorStack.WPF.Dialogs;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for AudioToMusicView.xaml, the music tasks that start from a source audio (ACE-Step cover, repaint, lego, extract and complete, Stable Audio init and inpaint audio).
    /// </summary>
    public partial class AudioToMusicView : ViewBaseDiffusion
    {
        private AudioInputStream _sourceAudio;
        private AudioInputStream _referenceAudio;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioToMusicView"/> class.
        /// </summary>
        public AudioToMusicView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IAudioService audioService, ILanguageService languageService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<AudioToMusicView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, audioService, languageService, extractService, upscaleService, historyService, logger)
        {
            LyricTemplateCommand = new RelayCommand(AddLyricTemplate, CanAddLyricTemplate);
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.AudioToMusic;
        public RelayCommand LyricTemplateCommand { get; }

        /// <summary>
        /// Gets or sets the source audio the task starts from.
        /// </summary>
        public AudioInputStream SourceAudio
        {
            get { return _sourceAudio; }
            set { SetProperty(ref _sourceAudio, value); }
        }

        /// <summary>
        /// Gets or sets the reference audio that conditions the timbre and style (ACE-Step).
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
            await SourceAudioControl.StopAsync();
            await ReferenceAudioControl.StopAsync();
            await base.CloseAsync();
        }


        /// <summary>
        /// Execute the pipeline.
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[AudioToMusic] [Execute] Executing pipeline...");

            try
            {
                Progress.Clear();
                Statistics.Clear();
                ResultAudio = default;
                if (!await ValidateInputsAsync())
                    return;

                Statistics.Start();
                var options = CreateOptions(Options);
                var resultTensor = await ExecuteAudioDiffusionAsync(options);

                Statistics.Stop();
                ResultAudio = await SaveHistoryAsync(resultTensor, options);
                Logger.LogInformation("[AudioToMusic] [Execute] Executing pipeline complete, Task: {Task}, Elapsed: {Elapsed:c}", options.Task, Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[AudioToMusic] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[AudioToMusic] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.ExecutePipelineTitle, ex.Message);
            }
            finally
            {
                Progress.Clear();
            }
        }


        /// <summary>
        /// Executes the pipeline automation, each job runs the task on the source audio with a new seed.
        /// </summary>
        protected override async Task ExecuteAutomationAsync()
        {
            if (!await ValidateInputsAsync())
                return;

            IsAutomating = true;
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[AudioToMusic] [ExecuteAutomation] Executing pipeline...");

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
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Audio, MediaType.Audio))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var options = CreateOptions(automationJob.GenerateOptions);
                    var resultTensor = await ExecuteAudioDiffusionAsync(options);

                    ResultAudio = !AutomationOptions.IsHistoryEnabled
                        ? resultTensor
                        : await SaveHistoryAsync(resultTensor, options);

                    await automationJob.SaveAsync(ResultAudio);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"{AppDefault.Automation}: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[AudioToMusic] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[AudioToMusic] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[AudioToMusic] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
        /// Determines whether this process can execute automations, the jobs run on the source audio so one is required.
        /// </summary>
        protected override bool CanExecuteAutomation()
        {
            if (_sourceAudio is null)
                return false;

            return base.CanExecuteAutomation();
        }


        /// <summary>
        /// Validates the inputs of the task, every task needs a source audio and repaint and inpaint audio need the range to regenerate.
        /// </summary>
        /// <returns><c>true</c> if the task can run, otherwise the missing input is shown.</returns>
        private async Task<bool> ValidateInputsAsync()
        {
            var message = default(string);
            if (_sourceAudio is null)
                message = AppErrors.SourceAudioRequired;
            else if (InputControl.IsRangeSupported && !Options.HasRange)
                message = AppErrors.AudioRangeRequired;

            if (message is null)
                return true;

            Logger.LogInformation("[AudioToMusic] [ValidateInputs] {Message}", message);
            await DialogService.ShowMessageAsync(AppErrors.MissingInputTitle, message, MessageDialogType.Ok, MessageBoxIconType.Warning, MessageBoxStyleType.Warning);
            return false;
        }


        /// <summary>
        /// Creates the options of a run: the source audio, the reference audio when the model takes one, and the length of the source when the task keeps it.
        /// </summary>
        /// <param name="generateOptions">The generate options.</param>
        private GenerateInputOptions CreateOptions(GenerateInputOptions generateOptions)
        {
            var options = generateOptions with { };
            options.InputAudios = [_sourceAudio];
            options.ReferenceAudio = InputControl.IsReferenceAudioSupported ? _referenceAudio : default;
            if (!InputControl.IsDurationSupported)
                options.Duration = (float)_sourceAudio.Duration.TotalSeconds;

            return options;
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
        /// <param name="audioStream">The audio stream.</param>
        /// <param name="options">The options.</param>
        private async Task<AudioInputStream> SaveHistoryAsync(AudioInputStream audioStream, GenerateInputOptions options)
        {
            Logger.LogInformation("[AudioToMusic] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(audioStream, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                Source = View.AudioToMusic,
            });
            Logger.LogInformation("[AudioToMusic] [SaveHistory] History saved.");
            return result;
        }
    }
}
