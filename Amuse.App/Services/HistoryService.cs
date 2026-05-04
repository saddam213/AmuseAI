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
using TensorStack.WPF.Controls;

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
            _historyCollection.Clear();
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
                else if (historyFile.Name.StartsWith("Recent_"))
                    historyItem = await Json.LoadAsync<RecentHistory>(historyFile.FullName);
                if (historyItem == null || historyItem.Version != HistoryVersion)
                    continue;

                historyItem.FilePath = historyFile.FullName;
                historyItem.MediaPath = Path.Combine(historyFile.DirectoryName, historyFile.Name.Replace(".json", $".{historyItem.Extension}"));
                historyItem.ThumbPath = Path.Combine(historyFile.DirectoryName, historyFile.Name.Replace(".json", ".png"));
                if (historyItem is RecentHistory recentHistory)
                {
                    historyItem.MediaPath = recentHistory.OriginalPath;
                    if (historyItem.MediaType == MediaType.Image)
                        historyItem.ThumbPath = recentHistory.OriginalPath;
                }

                if (!File.Exists(historyItem.MediaPath))
                {
                    FileHelper.DeleteFiles(historyItem.FilePath, historyItem.ThumbPath);
                    continue;
                }

                _historyCollection.Add(historyItem);
                if (_historyCollection.Count == _settings.HistoryItems)
                    break;
            }
        }


        public Task DeleteAsync(IHistoryItem historyItem)
        {
            _historyCollection.Remove(historyItem);

            if (historyItem is RecentHistory)
            {
                if (historyItem.MediaType == MediaType.Image)
                {
                    FileHelper.QueueDeleteFiles(historyItem.FilePath);
                }
                else
                {
                    FileHelper.QueueDeleteFiles(historyItem.FilePath, historyItem.ThumbPath);
                }
            }
            else
            {
                FileHelper.QueueDeleteFiles(historyItem.FilePath, historyItem.MediaPath, historyItem.ThumbPath);
            }
            return Task.CompletedTask;
        }


        public async Task AddAsync(MediaImportEventArgs mediaImport)
        {
            if (_settings.HistoryItems <= 0)
                return;

            if (!_settings.IsHistoryRecentItemsEnabled)
                return;

            var existing = _historyCollection.FirstOrDefault(x => x.MediaPath == mediaImport.MediaFile);
            if (existing != null)
            {
                if (_settings.IsHistoryAutoSortEnabled)
                {
                    existing.LastAccess = DateTime.Now;
                    await Json.SaveAsync(existing.FilePath, existing);
                    _historyCollection.Move(_historyCollection.IndexOf(existing), 0);
                }
            }
            else
            {
                var key = GetRandomName();
                var mediaType = mediaImport.MediaType;
                var extension = mediaType.GetExtension();
                var history = new RecentHistory
                {
                    Id = key,
                    Version = HistoryVersion,
                    Extension = extension,
                    MediaType = mediaType,
                    Timestamp = DateTime.Now,
                    LastAccess = DateTime.Now,
                    Source = View.Recent,
                    FilePath = Path.Combine(_settings.DirectoryHistory, $"Recent_{key}.json"),
                    MediaPath = mediaImport.MediaFile,
                    ThumbPath = Path.Combine(_settings.DirectoryHistory, $"Recent_{key}.png"),
                    OriginalPath = mediaImport.MediaFile,
                    Width = mediaImport.Width,
                    Height = mediaImport.Height,
                    FrameRate = mediaImport.FrameRate,
                    FrameCount = mediaImport.FrameCount,
                    Duration = mediaImport.Duration,
                    SampleRate = mediaImport.SampleRate
                };

                if (mediaImport.Thumbnail is not null)
                    await mediaImport.Thumbnail.SaveAsync(history.ThumbPath);
                else if (mediaType == MediaType.Image)
                    history.ThumbPath = mediaImport.MediaFile;

                await Json.SaveAsync(history.FilePath, history);
                AddHistoryItem(history);
            }
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
                LastAccess = DateTime.Now,
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
                LastAccess = DateTime.Now,
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
                LastAccess = DateTime.Now,
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
                LastAccess = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"LayerImage_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"LayerImage_{key}.png"),
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"LayerImage_{key}.png"),
                Width = image.Width,
                Height = image.Height,
            };

            return await AddImageInternalAsync(image, history);
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
                LastAccess = DateTime.Now,
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
                LastAccess = DateTime.Now,
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
                LastAccess = DateTime.Now,
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
                LastAccess = DateTime.Now,
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
                LastAccess = DateTime.Now,
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
                LastAccess = DateTime.Now,
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

        Task AddAsync(MediaImportEventArgs mediaImport);

        Task<ImageInput> AddAsync(ImageInput image, DiffusionHistory history);
        Task<ImageInput> AddAsync(ImageInput image, ExtractHistory history);
        Task<ImageInput> AddAsync(ImageInput image, UpscaleHistory history);
        Task<ImageInput> AddAsync(ImageInput image, ImageLayerHistory history);


        Task<VideoInputStream> AddAsync(VideoInputStream videoStream, DiffusionHistory history);
        Task<VideoInputStream> AddAsync(VideoInputStream videoStream, ExtractHistory history);
        Task<VideoInputStream> AddAsync(VideoInputStream videoStream, UpscaleHistory history);
        Task<VideoInputStream> AddAsync(VideoInputStream videoStream, InterpolateHistory history);

        Task<AudioInput> AddAsync(AudioInput audio, AudioHistory history);
        Task<TextInput> AddAsync(TextInput text, TextHistory history);
    }

}
