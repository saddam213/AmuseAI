using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
    /// Interaction logic for AudioToTextView.xaml
    /// </summary>
    public partial class AudioToTextView : ViewBaseDiffusion
    {
        private const int LiveSampleRate = 16000;
        private const int LiveChannels = 1;
        private AudioInputStream _sourceAudio;
        private string _previewResult;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioToTextView"/> class.
        /// </summary>
        public AudioToTextView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IAudioService audioService, ILanguageService languageService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<AudioToTextView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, audioService, languageService, extractService, upscaleService, historyService, logger)
        {
            LiveCommand = new AsyncRelayCommand(() => ExecutePipelineAsync(ExecuteLiveAsync), CanExecuteLive);
            InitializeComponent();
        }

        public AsyncRelayCommand LiveCommand { get; }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.AudioToText;

        /// <summary>
        /// Gets or sets the source audio.
        /// </summary>
        public AudioInputStream SourceAudio
        {
            get { return _sourceAudio; }
            set { SetProperty(ref _sourceAudio, value); ExecuteCommand.RaiseCanExecuteChanged(); }
        }

        /// <summary>
        /// Gets or sets the preview result.
        /// </summary>
        public string PreviewResult
        {
            get { return _previewResult; }
            set { SetProperty(ref _previewResult, value); }
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
            await LiveInputControl.StopAsync();
            await InputAudioControl.StopAsync();
            await base.CloseAsync();
        }


        /// <summary>
        /// Transcribes the microphone live, the text is shown as it is recognised until the microphone is stopped.
        /// The live input control owns the microphone: stopping it completes the transcript with the audio already captured.
        /// </summary>
        private async Task ExecuteLiveAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[AudioToText] [ExecuteLive] Executing live pipeline...");

            try
            {
                Progress.Clear();
                Statistics.Clear();
                ResultText = default;
                PreviewResult = default;
                Statistics.Start();

                // Microphone
                var options = Options with { };
                var transcript = new StringBuilder();
                var sampleRate = GenerateService.DefaultOptions.SampleRate > 0 ? GenerateService.DefaultOptions.SampleRate : LiveSampleRate;
                var inputAudio = LiveInputControl.CaptureAsync(sampleRate, LiveChannels);
                Logger.LogInformation("[AudioToText] [ExecuteLive] Microphone: {Microphone}, SampleRate: {SampleRate}, Channels: {Channels}", LiveInputControl.SelectedDevice?.Name, sampleRate, LiveChannels);

                // Transcribe
                Progress.Indeterminate(AppDefault.ProgressListening);
                await foreach (var text in GenerateService.GenerateTextStreamAsync(options, inputAudio))
                {
                    transcript.Append(text);
                    PreviewResult = transcript.ToString();
                }

                // Result
                Statistics.Stop();
                ResultText = new TextResult(new TextInput(transcript.ToString()));
                if (transcript.Length > 0)
                    await SaveHistoryAsync(options);

                Logger.LogInformation("[AudioToText] [ExecuteLive] Executing live pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[AudioToText] [ExecuteLive] Executing live pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[AudioToText] [ExecuteLive] An exception occurred executing live pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync(AppErrors.ExecutePipelineTitle, ex.Message);
            }
            finally
            {
                // The microphone is stopped however the pipeline ended
                await LiveInputControl.StopAsync();
                Progress.Clear();
            }
        }


        /// <summary>
        /// Determines whether the microphone can be transcribed live, the microphone is the input so no source audio is needed.
        /// </summary>
        private bool CanExecuteLive()
        {
            return IsPipelineLoaded && base.CanExecute();
        }


        /// <summary>
        /// Determines whether this process can execute, the source audio is transcribed so one is required.
        /// </summary>
        protected override bool CanExecute()
        {
            return base.CanExecute() && _sourceAudio != null;
        }


        /// <summary>
        /// Execute thge pipeline.
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation($"[AudioToText] [Execute] Executing pipeline...");

            try
            {
                Progress.Clear();
                Statistics.Clear();
                ResultText = default;
                PreviewResult = default;
                Statistics.Start();

                // Options
                var options = Options with { };
                if (_sourceAudio != null)
                {
                    options.InputAudios = [_sourceAudio];
                }

                // Execute
                var textResult = await ExecuteTextDiffusionAsync(options);

                // Result
                Statistics.Stop();

                ResultText = textResult;

                // History
                await SaveHistoryAsync(options);
                Logger.LogInformation("[AudioToText] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[AudioToText] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[AudioToText] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation($"[AudioToText] [ExecuteAutomation] Executing pipeline...");

            try
            {
                Progress.Clear();
                AutomationProgress.Clear();
                Statistics.Clear();
                ResultText = default;
                PreviewResult = default;
                Statistics.Start();
                CancellationTokenSource = new CancellationTokenSource();

                AutomationProgress.Indeterminate(AppDefault.AutomationStarted);
                var cancellationToken = CancellationTokenSource.Token;
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Text, MediaType.Audio))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    PreviewResult = default;

                    // Source
                    if (!automationJob.AudioStreams.IsNullOrEmpty())
                        SourceAudio = automationJob.AudioStreams[0];

                    // Diffusion
                    automationJob.GenerateOptions.InputAudios = [_sourceAudio];
                    var textResult = await ExecuteTextDiffusionAsync(automationJob.GenerateOptions);

                    // Result
                    ResultText = textResult;

                    // History
                    if (AutomationOptions.IsHistoryEnabled)
                    {
                        await SaveHistoryAsync(automationJob.GenerateOptions);
                    }

                    await automationJob.SaveAsync(ResultText.Result);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"{AppDefault.Automation}: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[AudioToText] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[AudioToText] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[AudioToText] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
        /// Determines whether this process can execute automations, a seed automation transcribes the source audio so one is required, the others supply their own.
        /// </summary>
        protected override bool CanExecuteAutomation()
        {
            if (AutomationOptions?.Type == AutomationType.Seed && _sourceAudio is null)
                return false;

            return base.CanExecute() && AutomationOptions?.IsValid() == true;
        }


        /// <summary>
        /// Save history
        /// </summary>
        /// <param name="options">The options.</param>
        private async Task<TextInput> SaveHistoryAsync(GenerateInputOptions options)
        {
            Logger.LogInformation($"[AudioToText] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(ResultText.Result, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                Source = View.AudioToText,
            });
            Logger.LogInformation($"[AudioToText] [SaveHistory] History saved.");
            return result;
        }


        /// <summary>
        /// Called when progress is received from a pipeline
        /// </summary>
        /// <param name="progress">The progress.</param>
        protected override void OnProgress(PipelineProgress progress)
        {
            if (!bool.TryParse(progress.Subkey, out var isReset))
            {
                base.OnProgress(progress);
                return;
            }

            PreviewResult = isReset
                ? progress.Message
                : PreviewResult + progress.Message;

            if (GenerateService.IsExecuting)
            {
                if (progress.Maximum > 0)
                    Progress.Update(progress.Value, progress.Maximum, string.Format(AppDefault.ProgressStep, progress.Key, progress.Subkey, progress.ElapsedKey, progress.Timestamp, progress.Elapsed, progress.Value, progress.Maximum));
                else
                    Progress.Indeterminate(AppDefault.ProgressTransformer);
            }
        }
    }
}
