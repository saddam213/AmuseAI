using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.Common.Video;
using TensorStack.Media.Video;

namespace Amuse.App.Services
{
    /// <summary>
    /// App media helpers, video and audio processing is in TensorStack.Media (VideoInputStream, AudioManager, VideoManager).
    /// </summary>
    public sealed class MediaService : IMediaService
    {
        private readonly Settings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public MediaService(Settings settings)
        {
            _settings = settings;
        }


        /// <summary>
        /// Gets a new temporary filename for the media type.
        /// </summary>
        /// <param name="mediaType">The media type.</param>
        public string GetTempFile(MediaType mediaType)
        {
            var extension = mediaType.GetExtension();
            return FileHelper.RandomFileName(_settings.DirectoryTemp, extension);
        }


        /// <summary>
        /// Get video information
        /// </summary>
        /// <param name="filename">The filename.</param>
        public async Task<VideoInfo> GetVideoInfoAsync(string filename)
        {
            return await VideoManager.LoadVideoInfoAsync(filename);
        }
    }


    public interface IMediaService : IVideoService
    {
        string GetTempFile(MediaType mediaType);
    }
}
