using Microsoft.Extensions.Logging;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TensorStack.Common.Common;
using TensorStack.Common.Tensor;
using TensorStack.Common.Video;
using TensorStack.Media;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.StableDiffusionCpp;
using PipelineCommon = TensorStack.StableDiffusionCpp.Common;

namespace Amuse.Host.StableDiffusionCpp
{
    /// <summary>
    /// Generates a long video as a chain of chunks, each chunk starts from the last frame of the one before it.
    /// The chunks are written to the video file as they complete, so only one chunk of frames is held in memory.
    /// A prompt with [ChunkN] tags gives each chunk its own prompt.
    /// The end of every chunk can be colour matched to the first chunk, the matched last frame starts the next chunk so a colour shift is not carried forward.
    /// </summary>
    internal sealed class ChunkedVideoGenerator
    {
        private const int ColorSampleStep = 4;
        private const int ColorTailDivisor = 4;
        private const float ColorGainMinimum = 0.8f;
        private const float ColorGainMaximum = 1.25f;
        private const int WriteBufferSize = 16;

        private readonly StableDiffusionPipeline _pipeline;
        private readonly VideoFrameRule _frameRule;
        private readonly Action<int, int> _chunkStarted;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChunkedVideoGenerator"/> class.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        /// <param name="frameRule">The frame counts the model accepts.</param>
        /// <param name="chunkStarted">Called with the chunk number and the chunk count as each chunk starts.</param>
        /// <param name="logger">The logger.</param>
        public ChunkedVideoGenerator(StableDiffusionPipeline pipeline, VideoFrameRule frameRule, Action<int, int> chunkStarted, ILogger logger)
        {
            _pipeline = pipeline;
            _frameRule = frameRule;
            _chunkStarted = chunkStarted;
            _logger = logger;
        }


        /// <summary>
        /// Gets the number of chunks needed for the frames, every chunk after the first repeats one frame.
        /// </summary>
        /// <param name="totalFrames">The total frames.</param>
        /// <param name="chunkFrames">The chunk frames.</param>
        /// <param name="frameRule">The frame counts the model accepts.</param>
        public static int GetChunkCount(int totalFrames, int chunkFrames, VideoFrameRule frameRule)
        {
            var chunkSize = frameRule.AlignUp(chunkFrames);
            if (chunkSize <= 1)
                return 1;
            if (totalFrames <= chunkSize)
                return 1;

            return 1 + (int)Math.Ceiling((totalFrames - chunkSize) / (double)(chunkSize - 1));
        }


        /// <summary>
        /// Generates the video to the output file.
        /// </summary>
        /// <param name="options">The options of the whole video, the init image starts the first chunk and the end image ends the last.</param>
        /// <param name="chunkOptions">The chunk options.</param>
        /// <param name="outputFile">The output video file.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task GenerateAsync(PipelineCommon.GenerateVideoOptions options, ChunkOptions chunkOptions, string outputFile, CancellationToken cancellationToken = default)
        {
            var chunkFrames = chunkOptions.ChunkFrames;
            var timestamp = Stopwatch.GetTimestamp();
            var totalFrames = options.VideoFrames;
            var chunkSize = _frameRule.AlignUp(chunkFrames);
            var chunkCount = GetChunkCount(totalFrames, chunkFrames, _frameRule);
            var audioFile = default(string);
            var frameChannel = Channel.CreateBounded<VideoFrame>(WriteBufferSize);

            MediaManager.Initialize(Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"), Path.Combine(AppContext.BaseDirectory, "ffprobe.exe"), Path.GetDirectoryName(outputFile));
            var writerTask = frameChannel.Reader.ReadAllAsync(cancellationToken).SaveAsync(outputFile, frameRateOverride: options.Fps, cancellationToken: cancellationToken);
            try
            {
                var audio = new List<float>();
                var audioFormat = default(AudioFormat);
                var colorReference = default(ColorStatistics);
                var previousFrame = default(ImageTensor);
                var framesWritten = 0;
                for (var chunk = 0; chunk < chunkCount; chunk++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _chunkStarted?.Invoke(chunk + 1, chunkCount);

                    var chunkTimestamp = Stopwatch.GetTimestamp();
                    var isFirst = chunk == 0;
                    var isLast = chunk == chunkCount - 1;
                    var repeatedFrames = isFirst ? 0 : 1;
                    var framesRemaining = totalFrames - framesWritten;
                    var chunkVideoOptions = options with
                    {
                        Seed = options.Seed + chunk,
                        Prompt = ChunkPrompt.GetPrompt(options.Prompt, chunk + 1),
                        NegativePrompt = GetNegativePrompt(options.NegativePrompt, chunk + 1),
                        VideoFrames = Math.Min(chunkSize, _frameRule.AlignUp(framesRemaining + repeatedFrames)),
                        InitImage = isFirst ? options.InitImage : previousFrame,
                        Strength = isFirst || chunkOptions.Strength <= 0 ? options.Strength : chunkOptions.Strength,
                        EndImage = isLast ? options.EndImage : default
                    };

                    var result = await _pipeline.GenerateVideoAsync(chunkVideoOptions, cancellationToken);
                    if (isFirst)
                        colorReference = GetColorStatistics(result.Frames);
                    else if (chunkOptions.IsColorMatchEnabled)
                        MatchColor(result.Frames, colorReference);

                    var frameCount = Math.Min(result.Frames.Length - repeatedFrames, framesRemaining);
                    for (var i = 0; i < frameCount; i++)
                    {
                        await frameChannel.Writer.WriteAsync(new VideoFrame(framesWritten + i, result.Frames[repeatedFrames + i], options.Fps), cancellationToken);
                    }

                    if (result.Audio != null)
                    {
                        audioFormat ??= new AudioFormat(result.Audio.SampleRate, result.Audio.Channels);
                        AppendAudio(audio, result.Audio, audioFormat, options.Fps, repeatedFrames, frameCount);
                    }

                    framesWritten += frameCount;
                    previousFrame = result.Frames[repeatedFrames + frameCount - 1];
                    _logger.LogInformation("[AmuseHost] [ChunkedVideo] Chunk {Chunk}/{ChunkCount} complete, Frames: {Frames}/{TotalFrames}, Elapsed: {Elapsed:c}", chunk + 1, chunkCount, framesWritten, totalFrames, Stopwatch.GetElapsedTime(chunkTimestamp));
                }

                frameChannel.Writer.Complete();
                await writerTask;

                if (audio.Count > 0)
                {
                    audioFile = FileHelper.RandomFileName(Path.GetDirectoryName(outputFile), "wav");
                    AudioManager.SaveAudio(audioFile, new AudioTensor(audio.ToArray(), audioFormat.Channels, audioFormat.SampleRate));
                    await AudioManager.AddAudioAsync(outputFile, audioFile, cancellationToken);
                }

                _logger.LogInformation("[AmuseHost] [ChunkedVideo] Video complete, Chunks: {ChunkCount}, Frames: {Frames}, Elapsed: {Elapsed:c}", chunkCount, framesWritten, Stopwatch.GetElapsedTime(timestamp));
            }
            catch (Exception ex)
            {
                frameChannel.Writer.TryComplete(ex);
                try
                {
                    await writerTask;
                }
                catch (Exception)
                {
                    // The generation failure is the one reported
                }

                FileHelper.QueueDeleteFile(outputFile);
                throw;
            }
            finally
            {
                FileHelper.QueueDeleteFile(audioFile);
            }
        }


        /// <summary>
        /// Gets the negative prompt of a chunk, null when the chunk has none.
        /// </summary>
        /// <param name="negativePrompt">The full negative prompt.</param>
        /// <param name="chunk">The chunk number, starting at 1.</param>
        internal static string GetNegativePrompt(string negativePrompt, int chunk)
        {
            var chunkPrompt = ChunkPrompt.GetPrompt(negativePrompt, chunk);
            return string.IsNullOrWhiteSpace(chunkPrompt) ? default : chunkPrompt;
        }


        /// <summary>
        /// Appends the audio of a chunk, the audio under the repeated frame is crossfaded into the end of the audio already joined.
        /// </summary>
        /// <param name="audio">The joined interleaved audio.</param>
        /// <param name="chunkAudio">The chunk audio.</param>
        /// <param name="format">The format of the joined audio.</param>
        /// <param name="frameRate">The frame rate.</param>
        /// <param name="repeatedFrames">The frames at the start of the chunk that repeat the previous chunk.</param>
        /// <param name="frameCount">The frames kept from the chunk.</param>
        private static void AppendAudio(List<float> audio, AudioTensor chunkAudio, AudioFormat format, int frameRate, int repeatedFrames, int frameCount)
        {
            if (chunkAudio.SampleRate != format.SampleRate || chunkAudio.Channels != format.Channels)
                return;

            var channels = format.Channels;
            var samples = chunkAudio.Span;
            var samplesPerFrame = format.SampleRate / (double)frameRate;
            var skipSamples = Math.Min((int)Math.Round(repeatedFrames * samplesPerFrame), chunkAudio.Samples);
            var keepSamples = Math.Min((int)Math.Round(frameCount * samplesPerFrame), chunkAudio.Samples - skipSamples);
            var fadeSamples = Math.Min(skipSamples, audio.Count / channels);
            var fadeStart = audio.Count - fadeSamples * channels;
            for (var i = 0; i < fadeSamples; i++)
            {
                var weight = (i + 1) / (float)(fadeSamples + 1);
                for (var c = 0; c < channels; c++)
                {
                    var index = fadeStart + i * channels + c;
                    var incoming = samples[(skipSamples - fadeSamples + i) * channels + c];
                    audio[index] = audio[index] * (1f - weight) + incoming * weight;
                }
            }

            var keepStart = skipSamples * channels;
            var keepLength = keepSamples * channels;
            for (var i = 0; i < keepLength; i++)
            {
                audio.Add(samples[keepStart + i]);
            }
        }


        /// <summary>
        /// Matches the colour of the frames to the reference with a per channel gain and offset.
        /// The colour shift builds up over a chunk, so the correction is measured at the end of the chunk and ramps in from the first frame, which keeps the join to the previous chunk continuous.
        /// </summary>
        /// <param name="frames">The frames, updated in place.</param>
        /// <param name="reference">The reference statistics.</param>
        private static void MatchColor(ImageTensor[] frames, ColorStatistics reference)
        {
            if (frames.Length < 2)
                return;

            var tailStart = frames.Length - Math.Max(1, frames.Length / ColorTailDivisor);
            var statistics = GetColorStatistics(frames.AsSpan(tailStart));
            var gains = new float[3];
            var offsets = new float[3];
            for (var channel = 0; channel < 3; channel++)
            {
                var deviation = statistics.Deviation[channel];
                gains[channel] = deviation > 0 ? Math.Clamp((float)(reference.Deviation[channel] / deviation), ColorGainMinimum, ColorGainMaximum) : 1f;
                offsets[channel] = (float)(reference.Mean[channel] - statistics.Mean[channel] * gains[channel]);
            }

            Parallel.For(1, frames.Length, index =>
            {
                var weight = index / (float)(frames.Length - 1);
                for (var channel = 0; channel < 3; channel++)
                {
                    var gain = 1f + (gains[channel] - 1f) * weight;
                    var offset = offsets[channel] * weight;
                    var pixels = frames[index].GetChannel(channel + 1);
                    for (var i = 0; i < pixels.Length; i++)
                    {
                        pixels[i] = Math.Clamp(pixels[i] * gain + offset, -1f, 1f);
                    }
                }
            });
        }


        /// <summary>
        /// Gets the per channel mean and standard deviation of the frames, from a sample of the frames and pixels.
        /// </summary>
        /// <param name="frames">The frames.</param>
        private static ColorStatistics GetColorStatistics(ReadOnlySpan<ImageTensor> frames)
        {
            var mean = new double[3];
            var deviation = new double[3];
            for (var channel = 0; channel < 3; channel++)
            {
                var sum = 0d;
                var sumSquares = 0d;
                var count = 0L;
                for (var f = 0; f < frames.Length; f += ColorSampleStep)
                {
                    var pixels = frames[f].GetChannel(channel + 1);
                    for (var i = 0; i < pixels.Length; i += ColorSampleStep)
                    {
                        var value = pixels[i];
                        sum += value;
                        sumSquares += value * value;
                        count++;
                    }
                }

                mean[channel] = sum / count;
                deviation[channel] = Math.Sqrt(Math.Max(0d, sumSquares / count - mean[channel] * mean[channel]));
            }
            return new ColorStatistics(mean, deviation);
        }


        /// <summary>
        /// The per channel colour statistics of a chunk.
        /// </summary>
        /// <param name="Mean">The mean of each channel.</param>
        /// <param name="Deviation">The standard deviation of each channel.</param>
        private sealed record ColorStatistics(double[] Mean, double[] Deviation);


        /// <summary>
        /// The format of the joined audio.
        /// </summary>
        /// <param name="SampleRate">The sample rate.</param>
        /// <param name="Channels">The channels.</param>
        private sealed record AudioFormat(int SampleRate, int Channels);
    }
}
