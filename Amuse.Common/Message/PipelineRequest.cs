using System.Collections.Generic;
using TensorStack.Common;
using TensorStack.Common.Tensor;

namespace Amuse.Common.Message
{
    public sealed class PipelineRequest
    {
        public PipelineRequest() { }
        public PipelineRequest(RequestType type)
        {
            Type = type;
        }

        public PipelineRequest(PipelineLoadOptions options, RequestType type)
        {
            LoadOptions = options;
            Type = type;
        }

        public PipelineRequest(PipelineReloadOptions options)
        {
            ReloadOptions = options;
            Type = RequestType.Reload;
        }

        public PipelineRequest(PipelineCreateOptions options)
        {
            CreateOptions = options;
            Type = RequestType.Create;
        }

        public PipelineRequest(PipelineRunOptions options)
        {
            RunOptions = options;
            Type = RequestType.Run;
        }

        public RequestType Type { get; init; }
        public PipelineCreateOptions CreateOptions { get; init; }
        public PipelineLoadOptions LoadOptions { get; init; }
        public PipelineReloadOptions ReloadOptions { get; init; }
        public PipelineRunOptions RunOptions { get; init; }
        public TensorMetadata TensorMetadata { get; set; }


        public IReadOnlyList<Tensor<float>> PackTensors()
        {
            if (RunOptions?.ImageOptions != null)
                return PackTensors(RunOptions.ImageOptions);
            if (RunOptions?.VideoOptions != null)
                return PackTensors(RunOptions.VideoOptions);
            if (RunOptions?.AudioOptions != null)
                return PackTensors(RunOptions.AudioOptions);
            if (RunOptions?.TextOptions != null)
                return PackTensors(RunOptions.TextOptions);
            if (RunOptions?.UpscaleImageOptions != null)
                return PackTensors(RunOptions.UpscaleImageOptions);
            if (RunOptions?.UpscaleVideoOptions != null)
                return PackTensors(RunOptions.UpscaleVideoOptions);
            if (RunOptions?.ExtractImageOptions != null)
                return PackTensors(RunOptions.ExtractImageOptions);
            return [];
        }


        public void UnpackTensors(IReadOnlyList<Tensor<float>> packedTensors)
        {
            if (RunOptions.ImageOptions != null)
                UnpackTensors(packedTensors, RunOptions.ImageOptions);
            if (RunOptions.VideoOptions != null)
                UnpackTensors(packedTensors, RunOptions.VideoOptions);
            if (RunOptions.AudioOptions != null)
                UnpackTensors(packedTensors, RunOptions.AudioOptions);
            if (RunOptions.TextOptions != null)
                UnpackTensors(packedTensors, RunOptions.TextOptions);
            if (RunOptions.UpscaleImageOptions != null)
                UnpackTensors(packedTensors, RunOptions.UpscaleImageOptions);
            if (RunOptions.UpscaleVideoOptions != null)
                UnpackTensors(packedTensors, RunOptions.UpscaleVideoOptions);
            if (RunOptions.ExtractImageOptions != null)
                UnpackTensors(packedTensors, RunOptions.ExtractImageOptions);
        }


        private IReadOnlyList<Tensor<float>> PackTensors(IGenerateOptions options)
        {
            var metadata = new TensorMetadata();
            var tensorData = new List<Tensor<float>>();
            if (!options.InputImages.IsNullOrEmpty())
            {
                foreach (var tensor in options.InputImages)
                {
                    tensorData.Add(tensor);
                    metadata.AddImage(tensor);
                }
            }
            if (!options.InputControlImages.IsNullOrEmpty())
            {
                foreach (var tensor in options.InputControlImages)
                {
                    tensorData.Add(tensor);
                    metadata.AddControlImage(tensor);
                }
            }
            if (!options.InputAudios.IsNullOrEmpty())
            {
                foreach (var tensor in options.InputAudios)
                {
                    tensorData.Add(tensor);
                    metadata.AddAudio(tensor);
                }
            }
            if (!options.InputVideos.IsNullOrEmpty())
            {
                foreach (var videoSequence in options.InputVideos)
                {
                    if (videoSequence == null)
                        continue;
                    tensorData.AddRange(videoSequence.Frames);
                    if (videoSequence.HasAudio)
                        tensorData.Add(videoSequence.Audio);
                    metadata.AddVideo(videoSequence);
                }
            }
            TensorMetadata = metadata;
            return tensorData;
        }


        private void UnpackTensors(IReadOnlyList<Tensor<float>> packedTensors, IGenerateOptions options)
        {
            var metadata = TensorMetadata;
            if (metadata == null || packedTensors.IsNullOrEmpty())
                return;

            var tensorIndex = 0;
            if (metadata.ImageCount > 0)
            {
                var images = new List<ImageTensor>(metadata.ImageCount);
                for (var i = 0; i < metadata.ImageCount; i++)
                {
                    var imageTensor = packedTensors[tensorIndex + i].AsImageTensor();
                    images.Add(imageTensor);
                }
                tensorIndex += metadata.ImageCount;
                options.InputImages = images;
            }
            if (metadata.ControlImageCount > 0)
            {
                var controlImages = new List<ImageTensor>(metadata.ControlImageCount);
                for (var i = 0; i < metadata.ControlImageCount; i++)
                {
                    var imageTensor = packedTensors[tensorIndex + i].AsImageTensor();
                    controlImages.Add(imageTensor);
                }
                tensorIndex += metadata.ControlImageCount;
                options.InputControlImages = controlImages;
            }
            if (metadata.AudioCount > 0)
            {
                var audioTensors = new List<AudioTensor>();
                for (var i = 0; i < metadata.AudioCount; i++)
                {
                    var audioMetadata = metadata.AudioMetadata[i];
                    var audioTensor = packedTensors[tensorIndex + i].AsAudioTensor(audioMetadata.SampleRate);
                    audioTensors.Add(audioTensor);
                }
                tensorIndex += metadata.AudioCount;
                options.InputAudios = audioTensors;
            }
            if (metadata.VideoCount > 0)
            {
                var videoSequences = new List<VideoSequence>(metadata.VideoCount);
                for (var i = 0; i < metadata.VideoCount; i++)
                {
                    AudioTensor audio = default;
                    var videoMetadata = metadata.VideoMetadata[i];
                    var frames = new ImageTensor[videoMetadata.FrameCount];
                    for (var f = 0; f < videoMetadata.FrameCount; f++)
                        frames[f] = packedTensors[tensorIndex + f].AsImageTensor();

                    tensorIndex += videoMetadata.FrameCount;
                    var audioMetadata = metadata.VideoAudioMetadata[i];
                    if (audioMetadata.SampleRate > 0)
                    {
                        audio = packedTensors[tensorIndex].AsAudioTensor(audioMetadata.SampleRate);
                        tensorIndex++;
                    }
                    var videoSequence = new VideoSequence(frames, videoMetadata.FrameRate, audio);
                    videoSequences.Add(videoSequence);
                }
                options.InputVideos = videoSequences;
            }
        }
    }
}
