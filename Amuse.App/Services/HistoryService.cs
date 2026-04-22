using Amuse.App.Common;
using Amuse.App.Views;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Audio;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.Image;
using TensorStack.Video;

namespace Amuse.App.Services
{
    public class HistoryService : IHistoryService
    {
        private const int HistoryVersion = 3;
        private readonly Settings _settings;
        private readonly ObservableCollection<IHistoryItem> _historyCollection;

        /// <summary>
        /// Initializes a new instance of the <see cref="HistoryService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public HistoryService(Settings settings)
        {
            _settings = settings;
            _historyCollection = [];
        }

        /// <summary>
        /// Gets the history collection.
        /// </summary>
        public ObservableCollection<IHistoryItem> HistoryCollection => _historyCollection;


        public async Task InitializeAsync()
        {
            var historyFiles = Directory.EnumerateFiles(_settings.DirectoryHistory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(x => new FileInfo(x))
                .OrderByDescending(x => x.CreationTimeUtc)
                .Take(_settings.HistoryItems)
                .ToList();
            foreach (var historyFile in historyFiles)
            {
                var historyItem = default(IHistoryItem);
                if (historyFile.Name.StartsWith("Recent_"))
                    historyItem = await Json.LoadAsync<RecentHistory>(historyFile.FullName);
                else if (historyFile.Name.StartsWith("GenerateImage_"))
                    historyItem = await Json.LoadAsync<DiffusionHistory>(historyFile.FullName);
                else if (historyFile.Name.StartsWith("GenerateVideo_"))
                    historyItem = await Json.LoadAsync<DiffusionHistory>(historyFile.FullName);
                else if (historyFile.Name.StartsWith("ExtractImage_"))
                    historyItem = await Json.LoadAsync<ExtractHistory>(historyFile.FullName);
                else if (historyFile.Name.StartsWith("ExtractVideo_"))
                    historyItem = await Json.LoadAsync<ExtractHistory>(historyFile.FullName);
                else if (historyFile.Name.StartsWith("UpscaleImage_"))
                    historyItem = await Json.LoadAsync<UpscaleHistory>(historyFile.FullName);
                else if (historyFile.Name.StartsWith("UpscaleVideo_"))
                    historyItem = await Json.LoadAsync<UpscaleHistory>(historyFile.FullName);
                else if (historyFile.Name.StartsWith("Interpolate_"))
                    historyItem = await Json.LoadAsync<InterpolateHistory>(historyFile.FullName);
                else if (historyFile.Name.StartsWith("GenerateAudio_"))
                    historyItem = await Json.LoadAsync<AudioHistory>(historyFile.FullName);
                else if (historyFile.Name.StartsWith("GenerateText_"))
                    historyItem = await Json.LoadAsync<TextHistory>(historyFile.FullName);
                else if (historyFile.Name.StartsWith("LayerImage_"))
                    historyItem = await Json.LoadAsync<ImageLayerHistory>(historyFile.FullName);
                if (historyItem == null || historyItem.Version != HistoryVersion)
                    continue;

                historyItem.FilePath = historyFile.FullName;
                historyItem.MediaPath = Path.Combine(historyFile.DirectoryName, historyFile.Name.Replace(".json", $".{historyItem.Extension}"));
                historyItem.ThumbPath = Path.Combine(historyFile.DirectoryName, historyFile.Name.Replace(".json", ".png"));
                if (!File.Exists(historyItem.MediaPath))
                    continue; // Delete?

                _historyCollection.Add(historyItem);
                if (_historyCollection.Count == _settings.HistoryItems)
                    break;
            }
        }


        public Task DeleteAsync(IHistoryItem historyItem)
        {
            _historyCollection.Remove(historyItem);
            FileHelper.QueueDeleteFiles(historyItem.FilePath, historyItem.MediaPath, historyItem.ThumbPath);
            return Task.CompletedTask;
        }


        public async Task AddAsync(ImageInput image)
        {
            if (_settings.HistoryItems <= 0)
                return;

            var key = GetRandomName();
            var history = new RecentHistory
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "png",
                MediaType = MediaType.Image,
                Timestamp = DateTime.Now,
                Source = View.History,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"Recent_{key}.json"),
                MediaPath = image.SourceFile,
                Width = image.Width,
                Height = image.Height,
            };

            await Json.SaveAsync(history.FilePath, history);
            AddHistoryItem(history);
        }


        public async Task<ImageInput> AddAsync(ImageInput image, DiffusionHistory diffusionHistory)
        {
            if (_settings.HistoryItems <= 0)
                return image;

            var key = GetRandomName();
            var history = diffusionHistory with
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "png",
                MediaType = MediaType.Image,
                Timestamp = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"GenerateImage_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"GenerateImage_{key}.png"),
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"GenerateImage_{key}.png"),
                Width = image.Width,
                Height = image.Height,
            };
            return await AddImageInternalAsync(image, history);
        }


        public async Task<ImageInput> AddAsync(ImageInput image, ExtractHistory extractHistory)
        {
            if (_settings.HistoryItems <= 0)
                return image;

            var key = GetRandomName();
            var history = extractHistory with
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "png",
                MediaType = MediaType.Image,
                Timestamp = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"ExtractImage_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"ExtractImage_{key}.png"),
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"ExtractImage_{key}.png"),
                Width = image.Width,
                Height = image.Height,
            };

            return await AddImageInternalAsync(image, history);
        }


        public async Task<ImageInput> AddAsync(ImageInput image, UpscaleHistory upscaleHistory)
        {
            if (_settings.HistoryItems <= 0)
                return image;

            var key = GetRandomName();
            var history = upscaleHistory with
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "png",
                MediaType = MediaType.Image,
                Timestamp = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"UpscaleImage_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"UpscaleImage_{key}.png"),
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"UpscaleImage_{key}.png"),
                Width = image.Width,
                Height = image.Height,
            };

            return await AddImageInternalAsync(image, history);
        }


        public async Task<ImageInput> AddAsync(ImageInput image, ImageLayerHistory layerHistory)
        {
            if (_settings.HistoryItems <= 0)
                return image;

            var key = GetRandomName();
            var history = layerHistory with
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "png",
                MediaType = MediaType.Image,
                Timestamp = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"LayerImage_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"LayerImage_{key}.png"),
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"LayerImage_{key}.png"),
                Width = image.Width,
                Height = image.Height,
            };

            return await AddImageInternalAsync(image, history);
        }



        public async Task<VideoInputStream> AddAsync(VideoInputStream videoStream)
        {
            if (_settings.HistoryItems <= 0)
                return videoStream;

            var key = GetRandomName();
            var history = new RecentHistory
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "mp4",
                MediaType = MediaType.Video,
                Timestamp = DateTime.Now,
                Source = View.History,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"Recent_{key}.json"),
                MediaPath = videoStream.SourceFile,
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"Recent_{key}.png"),
                Width = videoStream.Width,
                Height = videoStream.Height,
                FrameRate = videoStream.FrameRate,
                FrameCount = videoStream.FrameCount,
                Duration = videoStream.Duration
            };

            return await AddVideoInternalAsync(videoStream, history);
        }


        public async Task<AudioInput> AddAsync(AudioInput audioInput)
        {
            if (_settings.HistoryItems <= 0)
                return audioInput;

            var key = GetRandomName();
            var history = new RecentHistory
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "wav",
                MediaType = MediaType.Audio,
                Timestamp = DateTime.Now,
                Source = View.History,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"Recent_{key}.json"),
                MediaPath = audioInput.SourceFile,
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"Recent_{key}.png"),
                Duration = audioInput.Duration,
                SampleRate = audioInput.SampleRate,
            };

            return await AddAudioInternalAsync(audioInput, history);
        }


        public async Task<TextInput> AddAsync(TextInput textInput)
        {
            if (_settings.HistoryItems <= 0)
                return textInput;

            var key = GetRandomName();
            var history = new RecentHistory
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "txt",
                MediaType = MediaType.Text,
                Timestamp = DateTime.Now,
                Source = View.History,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"Recent_{key}.json"),
                MediaPath = textInput.SourceFile,
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"Recent_{key}.txt"),
            };

            return await AddTextInternalAsync(textInput, history);
        }


        public async Task<VideoInputStream> AddAsync(VideoInputStream videoStream, DiffusionHistory diffusionHistory)
        {
            if (_settings.HistoryItems <= 0)
                return videoStream;

            var key = GetRandomName();
            var history = diffusionHistory with
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "mp4",
                MediaType = MediaType.Video,
                Timestamp = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"GenerateVideo_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"GenerateVideo_{key}.mp4"),
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"GenerateVideo_{key}.png"),
                Width = videoStream.Width,
                Height = videoStream.Height,
                FrameRate = videoStream.FrameRate,
                FrameCount = videoStream.FrameCount,
                Duration = videoStream.Duration
            };

            return await AddVideoInternalAsync(videoStream, history);
        }


        public async Task<VideoInputStream> AddAsync(VideoInputStream videoStream, ExtractHistory extractHistory)
        {
            if (_settings.HistoryItems <= 0)
                return videoStream;

            var key = GetRandomName();
            var history = extractHistory with
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "mp4",
                MediaType = MediaType.Video,
                Timestamp = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"ExtractVideo_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"ExtractVideo_{key}.mp4"),
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"ExtractVideo_{key}.png"),
                Width = videoStream.Width,
                Height = videoStream.Height,
                FrameRate = videoStream.FrameRate,
                FrameCount = videoStream.FrameCount,
                Duration = videoStream.Duration
            };

            return await AddVideoInternalAsync(videoStream, history);
        }


        public async Task<VideoInputStream> AddAsync(VideoInputStream videoStream, UpscaleHistory upscaleHistory)
        {
            if (_settings.HistoryItems <= 0)
                return videoStream;

            var key = GetRandomName();
            var history = upscaleHistory with
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "mp4",
                MediaType = MediaType.Video,
                Timestamp = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"UpscaleImage_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"UpscaleImage_{key}.mp4"),
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"UpscaleImage_{key}.png"),
                Width = videoStream.Width,
                Height = videoStream.Height,
                FrameRate = videoStream.FrameRate,
                FrameCount = videoStream.FrameCount,
                Duration = videoStream.Duration
            };

            return await AddVideoInternalAsync(videoStream, history);
        }


        public async Task<VideoInputStream> AddAsync(VideoInputStream videoStream, InterpolateHistory interpolateHistory)
        {
            if (_settings.HistoryItems <= 0)
                return videoStream;

            var key = GetRandomName();
            var history = interpolateHistory with
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "mp4",
                MediaType = MediaType.Video,
                Timestamp = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"Interpolate_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"Interpolate_{key}.mp4"),
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"Interpolate_{key}.png"),
                Width = videoStream.Width,
                Height = videoStream.Height,
                FrameRate = videoStream.FrameRate,
                FrameCount = videoStream.FrameCount,
                Duration = videoStream.Duration
            };

            return await AddVideoInternalAsync(videoStream, history);
        }


        public async Task<AudioInput> AddAsync(AudioInput audio, AudioHistory audioHistory)
        {
            if (_settings.HistoryItems <= 0)
                return audio;

            var key = GetRandomName();
            var history = audioHistory with
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "wav",
                MediaType = MediaType.Audio,
                Timestamp = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"GenerateAudio_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"GenerateAudio_{key}.wav"),
                //ThumbPath = Path.Combine(_settings.DirectoryHistory, $"GenerateAudio_{key}.png"),
                Duration = audio.Duration,
                SampleRate = audio.SampleRate
            };

            return await AddAudioInternalAsync(audio, history);
        }


        public async Task<TextInput> AddAsync(TextInput text, TextHistory textHistory)
        {
            if (_settings.HistoryItems <= 0)
                return text;

            var key = GetRandomName();
            var history = textHistory with
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "txt",
                MediaType = MediaType.Text,
                Timestamp = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"GenerateText_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"GenerateText_{key}.txt"),
                //ThumbPath = Path.Combine(_settings.DirectoryHistory, $"GenerateText_{key}.png"),
                InputLength = text.Length,
                InputText = text.Text
            };

            return await AddTextInternalAsync(text, history);
        }


        private string GetRandomName()
        {
            return Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
        }


        private async Task<ImageInput> AddImageInternalAsync<T>(ImageInput image, T history) where T : IHistoryItem
        {
            await image.SaveAsync(history.MediaPath);
            await Json.SaveAsync<T>(history.FilePath, history);
            AddHistoryItem(history);
            return image;
        }


        private async Task<VideoInputStream> AddVideoInternalAsync<T>(VideoInputStream videoStream, T history) where T : IHistoryItem
        {
            var newStream = await videoStream.MoveAsync(history.MediaPath);
            await videoStream.Thumbnail.SaveAsync(history.ThumbPath);
            await Json.SaveAsync(history.FilePath, history);
            AddHistoryItem(history);
            return newStream;
        }


        private async Task<AudioInput> AddAudioInternalAsync<T>(AudioInput audio, T history) where T : IHistoryItem
        {
            await audio.SaveAsync(history.MediaPath);
            await Json.SaveAsync<T>(history.FilePath, history);
            AddHistoryItem(history);
            return audio;
        }


        private async Task<TextInput> AddTextInternalAsync<T>(TextInput text, T history) where T : IHistoryItem
        {
            await text.SaveAsync(history.MediaPath);
            await Json.SaveAsync<T>(history.FilePath, history);
            AddHistoryItem(history);
            return text;
        }


        private void AddHistoryItem(IHistoryItem historyItem)
        {
            while (_historyCollection.Count > Math.Max(0, _settings.HistoryItems))
            {
                _historyCollection.RemoveAt(_historyCollection.Count - 1);
            }
            _historyCollection.Insert(0, historyItem);
        }

    }


    public interface IHistoryService
    {
        ObservableCollection<IHistoryItem> HistoryCollection { get; }

        Task InitializeAsync();
        Task DeleteAsync(IHistoryItem historyItem);

        Task AddAsync(ImageInput image);
        Task<ImageInput> AddAsync(ImageInput image, DiffusionHistory diffusionHistory);
        Task<ImageInput> AddAsync(ImageInput image, ExtractHistory extractHistory);
        Task<ImageInput> AddAsync(ImageInput image, UpscaleHistory upscaleHistory);
        Task<ImageInput> AddAsync(ImageInput image, ImageLayerHistory layerHistory);

        Task<VideoInputStream> AddAsync(VideoInputStream videoStream);
        Task<VideoInputStream> AddAsync(VideoInputStream videoStream, DiffusionHistory diffusionHistory);
        Task<VideoInputStream> AddAsync(VideoInputStream videoStream, ExtractHistory extractHistory);
        Task<VideoInputStream> AddAsync(VideoInputStream videoStream, UpscaleHistory upscaleHistory);
        Task<VideoInputStream> AddAsync(VideoInputStream videoStream, InterpolateHistory interpolateHistory);

        Task<AudioInput> AddAsync(AudioInput image, AudioHistory upscaleHistory);
        Task<TextInput> AddAsync(TextInput text, TextHistory upscaleHistory);
    }

}
