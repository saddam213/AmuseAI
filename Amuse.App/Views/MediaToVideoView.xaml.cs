using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Tensor;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for MediaToVideoView.xaml
    /// </summary>
    public partial class MediaToVideoView : ViewBaseDiffusion
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaToVideoView"/> class.
        /// </summary>
        public MediaToVideoView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IGenerateService generateService, IExtractService extractService, IUpscaleService upscaleService, IHistoryService historyService, ILogger<MediaToVideoView> logger)
            : base(settings, navigationService, downloadService, generateService, extractService, upscaleService, historyService, logger)
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the view.
        /// </summary>
        public override View View => View.MediaToVideo;


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
            await base.CloseAsync();
        }


        /// <summary>
        /// Execute thge pipeline.
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            Logger.LogInformation("[MediaToVideo] [Execute] Executing pipeline...");

            try
            {
                var previousVideo = ResultVideo;
                await ResultControl.ClearAsync();
                Progress.Clear();
                Statistics.Clear();
                ResultVideo = default;
                CompareVideo = default;
                Statistics.Start();

                // References
                var options = await GetReferenceOptionsAsync(Options);

                // Diffusion
                var resultTensor = await ExecuteVideoDiffusionAsync(options);

                // Upscale
                resultTensor = await ExecuteVideoUpscaleAsync(resultTensor);

                // Result
                Statistics.Stop();
                ResultVideo = await SaveHistoryAsync(options, resultTensor);
                CompareVideo = previousVideo;

                Logger.LogInformation("[MediaToVideo] [Execute] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[MediaToVideo] [Execute] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[MediaToVideo] [Execute] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
            Logger.LogInformation($"[MediaToVideo] [ExecuteAutomation] Executing pipeline...");

            try
            {
                Progress.Clear();
                AutomationProgress.Clear();
                Statistics.Clear();
                ResultImage = default;
                CompareImage = default;
                Statistics.Start();
                CancellationTokenSource = new CancellationTokenSource();

                // References, prepared once for all jobs
                var referenceOptions = await GetReferenceOptionsAsync(Options);

                AutomationProgress.Indeterminate(AppDefault.AutomationStarted);
                var cancellationToken = CancellationTokenSource.Token;
                await foreach (var automationJob in AutomationManager.CreateJobsAsync(AutomationOptions, Options, MediaType.Video, MediaType.Text))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Options
                    var options = automationJob.GenerateOptions with
                    {
                        InputImages = referenceOptions.InputImages,
                        InputVideos = referenceOptions.InputVideos,
                        InputAudios = referenceOptions.InputAudios
                    };

                    // Diffusion
                    var resultTensor = await ExecuteVideoDiffusionAsync(options);

                    // Upscale
                    resultTensor = await ExecuteVideoUpscaleAsync(resultTensor);

                    // Result
                    ResultVideo = !AutomationOptions.IsHistoryEnabled
                        ? resultTensor
                        : await SaveHistoryAsync(automationJob.GenerateOptions, resultTensor);

                    await automationJob.SaveAsync(ResultVideo);
                    AutomationProgress.Update(automationJob.Id, automationJob.Count, $"{AppDefault.Automation}: {automationJob.Id}/{automationJob.Count}");
                }

                Statistics.Stop();
                Logger.LogInformation("[MediaToVideo] [ExecuteAutomation] Executing pipeline complete, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException)
            {
                Statistics.Clear();
                Logger.LogInformation("[MediaToVideo] [ExecuteAutomation] Executing pipeline cancelled, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                Statistics.Clear();
                IsPipelineLoaded = GenerateService.IsLoaded;
                Logger.LogError(ex, "[MediaToVideo] [ExecuteAutomation] An exception occurred executing pipeline, Elapsed: {Elapsed:c}", Stopwatch.GetElapsedTime(timestamp));
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
        /// Save history
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="videoInput">The video input.</param>
        private async Task<VideoInputStream> SaveHistoryAsync(GenerateInputOptions options, VideoInputStream videoInput)
        {
            Logger.LogInformation("[MediaToVideo] [SaveHistory] Saving history...");
            var result = await HistoryService.AddAsync(videoInput, new DiffusionHistory
            {
                Options = options,
                Model = CurrentPipeline.DiffusionModel.Name,
                LoraModels = CurrentPipeline.LoraAdapterModel?.Select(x => x.Name).ToArray(),
                UpscaleModel = CurrentPipeline.UpscaleModel?.Name,
                UpscaleOptions = CurrentPipeline.UpscaleModel is not null ? UpscaleOptions : null,
                Source = View.MediaToVideo,
            });
            Logger.LogInformation("[MediaToVideo] [SaveHistory] History saved.");
            return result;
        }


        /// <summary>
        /// Gets a copy of the options with the image, video and audio references attached.
        /// </summary>
        /// <param name="options">The options.</param>
        private async Task<GenerateInputOptions> GetReferenceOptionsAsync(GenerateInputOptions options)
        {
            var videoReferences = ReferenceControl.GetVideoReferences();
            var inputVideos = new List<VideoSequence>(videoReferences.Count);
            foreach (var videoReference in videoReferences)
            {
                inputVideos.Add(await GetReferenceFramesAsync(videoReference, options));
            }

            Progress.Clear();
            return options with
            {
                InputImages = [.. ReferenceControl.GetImageReferences()],
                InputVideos = inputVideos,
                InputAudios = ReferenceControl.GetAudioReferences()
            };
        }


        /// <summary>
        /// Gets the reference video frames, resized to the output resolution and frame rate, up to the output frame count.
        /// Includes the video's own audio track, trimmed to the same length, if it has one.
        /// </summary>
        /// <param name="videoReference">The video reference.</param>
        /// <param name="options">The options.</param>
        private async Task<VideoSequence> GetReferenceFramesAsync(VideoInputStream videoReference, GenerateInputOptions options)
        {
            Progress.Clear();
            var frames = new List<ImageTensor>();
            await foreach (var sourceFrame in videoReference.GetAsync(options.Width, options.Height, options.FrameRate, TensorStack.Common.ResizeMode.Crop).Take(options.Frames))
            {
                Progress.Update(sourceFrame.Index, options.Frames, $"{AppDefault.ProcessingInputFrame}: {sourceFrame.Index}/{options.Frames}");
                frames.Add(sourceFrame.Frame);
            }

            var duration = frames.Count / options.FrameRate;
            var audio = await GetReferenceAudioAsync(videoReference, duration);
            return new VideoSequence([.. frames], options.FrameRate, audio);
        }


        /// <summary>
        /// Gets the reference video audio track, trimmed to the specified duration.
        /// </summary>
        /// <param name="videoReference">The video reference.</param>
        /// <param name="duration">The duration in seconds.</param>
        /// <returns>The audio, or null if the video has no audio track.</returns>
        private async Task<AudioTensor> GetReferenceAudioAsync(VideoInputStream videoReference, float duration)
        {
            var audioInfo = await AudioManager.LoadInfoAsync(videoReference.SourceFile);
            if (audioInfo is null)
                return default;

            var defaultOptions = CurrentPipeline.DiffusionModel.DefaultOptions;
            var sampleRate = defaultOptions.SampleRate > 0 ? defaultOptions.SampleRate : audioInfo.SampleRate;
            var channels = defaultOptions.Channels > 0 ? defaultOptions.Channels : audioInfo.Channels;
            var audio = await AudioManager.LoadTensorAsync(videoReference.SourceFile, sampleRate: sampleRate, channels: channels);
            return TrimAudio(audio, duration);
        }


        /// <summary>
        /// Trims the audio to the specified duration, from the start.
        /// </summary>
        /// <param name="audio">The audio.</param>
        /// <param name="duration">The duration in seconds.</param>
        /// <returns>The trimmed audio, or null if no samples remain.</returns>
        private static AudioTensor TrimAudio(AudioTensor audio, float duration)
        {
            var samples = Math.Min(audio.Samples, (int)Math.Round(duration * audio.SampleRate));
            if (samples <= 0)
                return default;

            if (samples == audio.Samples)
                return audio;

            // Audio data is interleaved, keep the first frames across all channels
            var trimmed = audio.Memory[..(samples * audio.Channels)].ToArray();
            return new Tensor<float>(trimmed, [audio.Channels, samples]).AsAudioTensor(audio.SampleRate);
        }
    }
}
