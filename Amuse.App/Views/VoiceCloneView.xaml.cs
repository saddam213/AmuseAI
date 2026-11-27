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
using TensorStack.WPF.Controls;
using TensorStack.WPF.Dialogs;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for VoiceCloneView.xaml, speech in the voice of a reference recording (Fish Audio, Higgs Audio, OmniVoice, VoxCPM2, VoxMel).
    /// </summary>
    public partial class VoiceCloneView : ViewBaseDiffusion
    {
        private TextInput _sourceText;
        private AudioInputStream _voiceAudio;

        /// <summary>
        /// Initializes a new instance of the <see cref="VoiceCloneView"/> class.
        /// </summary>
        public VoiceCloneView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IAudioService audioService, ILanguageService languageService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<VoiceCloneView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, audioService, languageService, extractService, upscaleService, historyService, logger)
        {
            _sourceText = new TextInput(string.Empty);
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.VoiceClone;

        public TextInput SourceText
        {
            get { return _sourceText; }
            set { SetProperty(ref _sourceText, value); }
        }

        /// <summary>
        /// Gets or sets the voice reference, a recording of the voice to speak in.
        /// </summary>
        public AudioInputStream VoiceAudio
        {
            get { return _voiceAudio; }
            set { SetProperty(ref _voiceAudio, value); }
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
            await VoiceAudioControl.StopAsync();
            await base.CloseAsync();
        }


        /// <summary>
        /// Execute the pipeline.
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[VoiceClone] [Execute] Executing pipeline...");

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
                Logger.LogInformation("[VoiceClone] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[VoiceClone] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[VoiceClone] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.ExecutePipelineTitle, ex.Message);
            }
            finally
            {
                Progress.Clear();
            }
        }


        /// <summary>
        /// Executes the pipeline automation, each job speaks its text in the voice of the reference.
        /// </summary>
        protected override async Task ExecuteAutomationAsync()
        {
            if (!await ValidateInputsAsync())
                return;

            IsAutomating = true;
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[VoiceClone] [ExecuteAutomation] Executing pipeline...");

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

                    if (!automationJob.InputTexts.IsNullOrEmpty())
                        SourceText = automationJob.InputTexts[0];

                    var options = CreateOptions(automationJob.GenerateOptions);
                    var resultTensor = await ExecuteAudioDiffusionAsync(options);

                    ResultAudio = !AutomationOptions.IsHistoryEnabled
                        ? resultTensor
                        : await SaveHistoryAsync(resultTensor, options);

                    await automationJob.SaveAsync(ResultAudio);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"{AppDefault.Automation}: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[VoiceClone] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[VoiceClone] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[VoiceClone] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
        /// Determines whether this process can execute automations, every job clones the voice reference so one is required.
        /// </summary>
        protected override bool CanExecuteAutomation()
        {
            if (_voiceAudio is null)
                return false;

            return base.CanExecuteAutomation();
        }


        /// <summary>
        /// Validates the inputs, a voice reference is required and Fish Audio, OmniVoice and VoxMel also need its transcript.
        /// </summary>
        /// <returns><c>true</c> if the speech can be generated, otherwise the missing input is shown.</returns>
        private async Task<bool> ValidateInputsAsync()
        {
            var message = default(string);
            if (_voiceAudio is null)
                message = AppErrors.VoiceReferenceRequired;
            else if (InputControl.IsReferenceTextRequired && string.IsNullOrWhiteSpace(Options.ReferenceText))
                message = AppErrors.ReferenceTextRequired;

            if (message is null)
                return true;

            Logger.LogInformation("[VoiceClone] [ValidateInputs] {Message}", message);
            await DialogService.ShowMessageAsync(AppErrors.MissingInputTitle, message, MessageDialogType.Ok, MessageBoxIconType.Warning, MessageBoxStyleType.Warning);
            return false;
        }


        /// <summary>
        /// Creates the options of a run, the text to speak and the voice reference.
        /// </summary>
        /// <param name="generateOptions">The generate options.</param>
        private GenerateInputOptions CreateOptions(GenerateInputOptions generateOptions)
        {
            var options = generateOptions with { Prompt = _sourceText.Text };
            options.InputAudios = [_voiceAudio];
            return options;
        }


        /// <summary>
        /// Save history
        /// </summary>
        /// <param name="audioStream">The audio stream.</param>
        /// <param name="options">The options.</param>
        private async Task<AudioInputStream> SaveHistoryAsync(AudioInputStream audioStream, GenerateInputOptions options)
        {
            Logger.LogInformation("[VoiceClone] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(audioStream, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                Source = View.VoiceClone,
            });
            Logger.LogInformation("[VoiceClone] [SaveHistory] History saved.");
            return result;
        }
    }
}
