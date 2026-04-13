using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Audio;
using TensorStack.Common;
using TensorStack.TextGeneration.Common;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for AudioToTextView.xaml
    /// </summary>
    public partial class AudioToTextView : ViewBaseModel
    {
        private AudioInput _sourceAudio;
        private AudioInputOptions _options;
        private bool _isMultipleResult;
        private int _selectedBeam;
        private string _previewResult;
        private TextInput _result;
        private readonly IProgress<GenerateProgress> _generateProgress;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioToTextView"/> class.
        /// </summary>
        public AudioToTextView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IHistoryService historyService, IDownloadService downloadService, Services.IAudioService audioService, ILogger<AudioToTextView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            AudioService = audioService;
            Results = new ObservableCollection<TextInput>();
            _generateProgress = new Progress<GenerateProgress>(OnGenerateProgress);
            InitializeComponent();
        }


        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.AudioToText;

        /// <summary>
        /// Gets the audio service.
        /// </summary>
        public Services.IAudioService AudioService { get; }

        /// <summary>
        /// Gets the results.
        /// </summary>
        public ObservableCollection<TextInput> Results { get; }

        /// <summary>
        /// Gets the result.
        /// </summary>
        public TextInput Result
        {
            get { return _result; }
            set { SetProperty(ref _result, value); }
        }

        /// <summary>
        /// Gets or sets the source audio.
        /// </summary>
        public AudioInput SourceAudio
        {
            get { return _sourceAudio; }
            set { SetProperty(ref _sourceAudio, value); ExecuteCommand.RaiseCanExecuteChanged(); }
        }

        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        public AudioInputOptions Options
        {
            get { return _options; }
            set { SetProperty(ref _options, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to show multiple results.
        /// </summary>
        public bool IsMultipleResult
        {
            get { return _isMultipleResult; }
            set { SetProperty(ref _isMultipleResult, value); }
        }

        /// <summary>
        /// Gets or sets the selected beam result.
        /// </summary>
        public int SelectedBeam
        {
            get { return _selectedBeam; }
            set { SetProperty(ref _selectedBeam, value); }
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
            Logger.LogInformation("[AudioToText] [LoadPipeline] Loading pipeline...");

            try
            {
                Progress.Indeterminate($"Loading {CurrentPipeline.AudioModel.Name}...");
                await AudioService.LoadAsync(CurrentPipeline);
                Options = _options ?? AudioService.DefaultOptions;
                await Settings.SetDefaultsAsync(CurrentPipeline);
                Logger.LogInformation("[AudioToText] [LoadPipeline] Pipeline successfully loaded, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("[AudioToText] [LoadPipeline] Loading canceled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[AudioToText] [LoadPipeline] An exception occurred loading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation("[AudioToText] [UnloadPipeline] Unloading pipeline...");

            try
            {
                await AudioService.UnloadAsync();
                Logger.LogInformation("[AudioToText] [UnloadPipeline] Pipeline unloaded successfully, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[AudioToText] [UnloadPipeline] An exception occurred unloading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation("[AudioToText] [Execute] Executing pipeline...");

            try
            {
                Progress.Clear();
                Statistics.Clear();
                Results.Clear();
                Result = null;
                Statistics.Start();
                PreviewResult = null;
                Progress.Indeterminate();

                // Run Audio
                var beamResults = await AudioService.ExecuteAsync(new WhisperRequest
                {
                    AudioInput = _sourceAudio,
                    Beams = _options.Beams,
                    DiversityLength = _options.DiversityLength,
                    EarlyStopping = _options.EarlyStopping,
                    Language = _options.Language,
                    LengthPenalty = _options.LengthPenalty,
                    MaxLength = _options.MaxLength,
                    MinLength = _options.MinLength,
                    NoRepeatNgramSize = _options.NoRepeatNgramSize,
                    Seed = _options.Seed,
                    Task = _options.Task,
                    Temperature = _options.Temperature,
                    TopK = _options.TopK,
                    TopP = _options.TopP,
                    ChunkSize = _options.ChunkSize,
                }, _generateProgress);

                Statistics.Stop();

                Result = beamResults.FirstOrDefault();
                foreach (var beamResult in beamResults)
                {
                    Results.Add(beamResult);
                }

                // History
                await HistoryService.AddAsync(Result, new TextHistory
                {
                    Model = CurrentPipeline.AudioModel.Name,
                    Source = View.AudioToText,
                    AudioOptions = _options
                });

                SelectedBeam = 0;
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
                IsPipelineLoaded = AudioService.IsLoaded;
                Logger.LogError(ex, "[AudioToText] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                await DialogService.ShowErrorAsync("Execute Pipeline", ex.Message);
            }
            finally
            {
                Progress.Clear();
                PreviewResult = null;
            }
        }


        /// <summary>
        /// Determines whether this instance can execute.
        /// </summary>
        protected override bool CanExecute()
        {
            return _sourceAudio is not null && AudioService.IsLoaded && !AudioService.IsExecuting;
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
        /// Called when generate progress received.
        /// </summary>
        /// <param name="progress">The progress.</param>
        private void OnGenerateProgress(GenerateProgress progress)
        {
            if (progress.IsReset)
            {
                PreviewResult = progress.Result;
                return;
            }
            PreviewResult += progress.Result;
        }

    }
}