using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Audio;
using TensorStack.Common;
using TensorStack.Common.Pipeline;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for TextToAudioView.xaml
    /// </summary>
    public partial class TextToAudioView : ViewBaseModel
    {
        private AudioInput _resultAudio;
        private AudioInputOptions _options;
        private TextInput _sourceText;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextToAudioView"/> class.
        /// </summary>
        public TextToAudioView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IDownloadService downloadService, IHistoryService historyService, Services.IAudioService audioService, ILogger<TextToAudioView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            AudioService = audioService;
            Options = new AudioInputOptions();
            SourceText = new TextInput(string.Empty);
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.TextToAudio;

        /// <summary>
        /// Gets the Audio service.
        /// </summary>
        public Services.IAudioService AudioService { get; }

        /// <summary>
        /// Gets or sets the result audio.
        /// </summary>
        public AudioInput ResultAudio
        {
            get { return _resultAudio; }
            set { SetProperty(ref _resultAudio, value); }
        }

        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        public AudioInputOptions Options
        {
            get { return _options; }
            set { SetProperty(ref _options, value); }
        }

        public TextInput SourceText
        {
            get { return _sourceText; }
            set { SetProperty(ref _sourceText, value); }
        }



        /// <summary>
        /// On view opened
        /// </summary>
        /// <param name="args">The arguments.</param>
        public override Task OpenAsync(OpenViewArgs args = null)
        {
            IsPipelineLoaded = AudioService.IsLoaded && AudioService.Pipeline.AudioModel == CurrentPipeline?.AudioModel;
            ModelControl.SetPipeline(AudioService.Pipeline);
            return base.OpenAsync(args);
        }


        /// <summary>
        /// Load pipeline
        /// </summary>
        protected override async Task<bool> LoadPipelineAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[TextToAudio] [LoadPipeline] Loading pipeline...");

            try
            {
                Progress.Indeterminate($"Loading {CurrentPipeline.AudioModel.Name}...");

                await AudioService.LoadAsync(CurrentPipeline);
                Options = new AudioInputOptions
                {
                    Steps = Options?.Steps ?? 10,
                    Speed = Options?.Speed ?? 1.1f,
                    VoiceStyle = Options?.VoiceStyle ?? CurrentPipeline.AudioModel.Prefixes.FirstOrDefault()
                };
                await Settings.SetDefaultsAsync(CurrentPipeline);

                Logger.LogInformation("[TextToAudio] [LoadPipeline] Pipeline successfully loaded, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("[TextToAudio] [LoadPipeline] Loading canceled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[TextToAudio] [LoadPipeline] An exception occurred loading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Load Pipeline", ex.Message);
                return false;
            }
            finally
            {
                Progress.Clear();
            }
        }


        /// <summary>
        /// Unload pipeline
        /// </summary>
        protected override async Task<bool> UnloadPipelineAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[TextToAudio] [UnloadPipeline] Unloading pipeline...");

            try
            {
                await AudioService.UnloadAsync();
                Logger.LogInformation("[TextToAudio] [UnloadPipeline] Pipeline unloaded successfully, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[TextToAudio] [UnloadPipeline] An exception occurred unloading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Unload Pipeline", ex.Message);
                return false;
            }
        }


        /// <summary>
        /// Execute the pipeline.
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[TextToAudio] [Execute] Executing pipeline...");

            try
            {
                Progress.Clear();
                Statistics.Clear();
                ResultAudio = default;
                Statistics.Start();

                // Run Audio
                var resultTensor = await AudioService.ExecuteAsync(new SupertonicRequest
                {
                    InputText = _sourceText.Text,
                    Seed = Options.Seed,
                    SilenceDuration = Options.SilenceDuration,
                    Speed = Options.Speed,
                    Steps = Options.Steps,
                    VoiceStyle = Options.VoiceStyle,
                }, ProgressCallback);

                Statistics.Stop();

                // Set Result
                ResultAudio = new AudioInput(resultTensor);

                // History
                await HistoryService.AddAsync(_resultAudio, new AudioHistory
                {
                    Options = _options,
                    Model = CurrentPipeline.AudioModel.Name,
                    Source = View.TextToAudio
                });

                Logger.LogInformation("[TextToAudio] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[TextToAudio] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = AudioService.IsLoaded;
                Logger.LogError(ex, "[TextToAudio] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Execute Pipeline", ex.Message);
            }
            finally
            {
                Progress.Clear();
            }
        }


        /// <summary>
        /// Determines whether this instance can execute.
        /// </summary>
        protected override bool CanExecute()
        {
            return !string.IsNullOrEmpty(_sourceText?.Text) && AudioService.IsLoaded && !AudioService.IsExecuting;
        }


        /// <summary>
        /// Cancel the process.
        /// </summary>
        protected override async Task CancelAsync()
        {
            await AudioService.CancelAsync();
        }


        /// <summary>
        /// Determines whether this instance can cancel.
        /// </summary>
        protected override bool CanCancel()
        {
            return AudioService.CanCancel;
        }


        /// <summary>
        /// Called when Audio model changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="pipeline">The pipeline.</param>
        protected async void OnSelectedModelChanged(object sender, PipelineModel pipeline)
        {
            try
            {
                IsPipelineLoaded = false;
                CurrentPipeline = pipeline;
                if (pipeline?.AudioModel == null)
                {
                    await UnloadPipelineAsync();
                }
                else
                {
                    Progress.Indeterminate($"Loading {pipeline.AudioModel.Name}...");

                    if (!await DownloadModels(pipeline))
                        return; // Canceled/Failed to download models

                    if (!await LoadPipelineAsync())
                        return; // Canceled/Failed to load pipeline

                    IsPipelineLoaded = true;
                }
            }
            finally
            {
                Progress.Clear();
                Statistics.Clear();
            }
        }


        /// <summary>
        /// Called when progress is received from a C# pipeline
        /// </summary>
        /// <param name="progress">The progress.</param>
        protected override void OnProgress(RunProgress progress)
        {
            if (progress.Maximum > 1)
                Progress.Update(progress.Value, progress.Maximum, $"Batch {progress.Value}/{progress.Maximum}");
            else
                Progress.Indeterminate("Processing Audio...");

            Logger.LogDebug("[{View}] [OnProgress] Step: {Value}/{Max}, Elapsed: {Elapsed:c}", ViewName, progress.Value, progress.Maximum, progress.Elapsed);
        }
    }
}