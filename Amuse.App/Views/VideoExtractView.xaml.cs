using Amuse.App.Common;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TensorStack.Common.Pipeline;
using TensorStack.Video;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for VideoExtractView.xaml
    /// </summary>
    public partial class VideoExtractView : ViewBaseModel
    {
        private VideoInputStream _sourceVideo;
        private VideoInputStream _resultVideo;
        private VideoInputStream _compareVideo;
        private ExtractInputOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoExtractView"/> class.
        /// </summary>
        public VideoExtractView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IDownloadService downloadService, IHistoryService historyService, IExtractService extractService, ILogger<VideoExtractView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            ExtractService = extractService;
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.VideoExtract;

        /// <summary>
        /// Gets the extract service.
        /// </summary>
        public IExtractService ExtractService { get; }

        /// <summary>
        /// Gets or sets the result video.
        /// </summary>
        public VideoInputStream ResultVideo
        {
            get { return _resultVideo; }
            set { SetProperty(ref _resultVideo, value); }
        }

        /// <summary>
        /// Gets or sets the compare video.
        /// </summary>
        public VideoInputStream CompareVideo
        {
            get { return _compareVideo; }
            set { SetProperty(ref _compareVideo, value); }
        }

        /// <summary>
        /// Gets or sets the source video.
        /// </summary>
        public VideoInputStream SourceVideo
        {
            get { return _sourceVideo; }
            set { SetProperty(ref _sourceVideo, value); }
        }

        /// <summary>
        /// Gets or sets the options.
        /// </summary>
        public ExtractInputOptions Options
        {
            get { return _options; }
            set { SetProperty(ref _options, value); }
        }


        /// <summary>
        /// On view opened
        /// </summary>
        /// <param name="args">The arguments.</param>
        public override Task OpenAsync(OpenViewArgs args = null)
        {
            ModelControl.SetPipeline(ExtractService.Pipeline);
            IsPipelineLoaded = ExtractService.IsLoaded && ExtractService.Pipeline.ExtractModel == CurrentPipeline?.ExtractModel;
            return base.OpenAsync(args);
        }


        /// <summary>
        /// Load pipeline
        /// </summary>
        protected override async Task<bool> LoadPipelineAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[VideoExtract] [LoadPipeline] Loading pipeline...");

            try
            {
                Progress.Indeterminate($"Loading {CurrentPipeline.ExtractModel.Name}...");

                await ExtractService.LoadAsync(CurrentPipeline);
                await Settings.SetDefaultsAsync(CurrentPipeline);

                Logger.LogInformation("[VideoExtract] [LoadPipeline] Pipeline successfully loaded, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("[VideoExtract] [LoadPipeline] Loading canceled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[VideoExtract] [LoadPipeline] An exception occurred loading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation("[VideoExtract] [UnloadPipeline] Unloading pipeline...");

            try
            {
                await ExtractService.UnloadAsync();
                Logger.LogInformation("[VideoExtract] [UnloadPipeline] Pipeline unloaded successfully, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[VideoExtract] [UnloadPipeline] An exception occurred unloading pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation("[VideoExtract] [Execute] Executing pipeline...");

            try
            {
                await ResultControl.ClearAsync();
                Progress.Clear();
                Statistics.Clear();
                ResultVideo = default;
                CompareVideo = default;
                Statistics.Start();

                // Run Extractor
                var resultVideo = await ExtractService.ExecuteAsync(new ExtractVideoRequest
                {
                    VideoStream = _sourceVideo,
                    Options = _options,
                }, ProgressCallback);

                Statistics.Stop();

                // Set Result
                ResultVideo = await HistoryService.AddAsync(resultVideo, new ExtractHistory
                {
                    Options = _options,
                    Model = CurrentPipeline.ExtractModel.Name,
                    ExtractorType = CurrentPipeline.ExtractModel.Type,
                    Source = View.VideoExtract,
                });
                CompareVideo = _sourceVideo;

                Logger.LogInformation("[VideoExtract] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[VideoExtract] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = ExtractService.IsLoaded;
                Logger.LogError(ex, "[VideoExtract] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            return _sourceVideo is not null && ExtractService.IsLoaded && !ExtractService.IsExecuting;
        }


        /// <summary>
        /// Cancel the process.
        /// </summary>
        protected override async Task CancelAsync()
        {
            await ExtractService.CancelAsync();
        }


        /// <summary>
        /// Determines whether this instance can cancel.
        /// </summary>
        protected override bool CanCancel()
        {
            return ExtractService.CanCancel;
        }


        /// <summary>
        /// Called when Extract model changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="pipeline">The pipeline.</param>
        protected async void SelectedExtractorChanged(object sender, PipelineModel pipeline)
        {
            try
            {
                IsPipelineLoaded = false;
                CurrentPipeline = pipeline;

                if (pipeline?.ExtractModel == null)
                {
                    await UnloadPipelineAsync();
                }
                else
                {
                    Progress.Indeterminate($"Loading {pipeline.ExtractModel.Name}...");

                    if (!await DownloadModels(pipeline))
                        return;  // Canceled/Failed to download models


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
                Progress.Update(progress.Value, progress.Maximum, $"Frame {progress.Value}/{progress.Maximum}");
            else
                Progress.Indeterminate("Rendering Video...");

            Logger.LogDebug("[{View}] [OnProgress] Step: {Value}/{Max}, Elapsed: {Elapsed:c}", ViewName, progress.Value, progress.Maximum, progress.Elapsed);
        }
    }
}