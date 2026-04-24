using System.IO;
using System.Threading.Tasks;
using TensorStack.Image;
using TensorStack.Video;

namespace Amuse.App.Common
{
    public record AutomationJob
    {
        public int Id { get; init; }
        public int Count { get; init; }
        public DiffusionInputOptions DiffusionOptions { get; init; }
        public UpscaleInputOptions UpscaleOptions { get; init; }
        public ExtractInputOptions ExtractOptions { get; init; }
        public InterpolateInputOptions InterpolateOptions { get; init; }

        public string OutputFile { get; init; }
        public ImageInput[] InputImages { get; init; }
        public VideoInputStream[] VideoStreams { get; init; }

        public async Task SaveAsync(ImageInput imageInput)
        {
            if (string.IsNullOrWhiteSpace(OutputFile))
                return;

            await imageInput.SaveAsync(OutputFile);
        }

        public Task SaveAsync(VideoInputStream videoInput)
        {
            if (string.IsNullOrWhiteSpace(OutputFile))
                return Task.CompletedTask;

            File.Copy(videoInput.SourceFile, OutputFile, true);
            return Task.CompletedTask;
        }
    }
}