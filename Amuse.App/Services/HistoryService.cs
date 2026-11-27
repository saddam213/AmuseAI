using Amuse.App.Common;
using Amuse.App.Views;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Image;

namespace Amuse.App.Services
{
    public sealed class HistoryService : IHistoryService
    {
        private const int HistoryVersion = 3;
        private static readonly Dictionary<string, Func<string, IHistoryItem>> _historyLoaders = new Dictionary<string, Func<string, IHistoryItem>>(StringComparer.Ordinal)
        {
            { "Recent", Json.Load<RecentHistory> },
            { "GenerateImage", Json.Load<DiffusionHistory> },
            { "GenerateVideo", Json.Load<DiffusionHistory> },
            { "GenerateAudio", Json.Load<DiffusionHistory> },
            { "GenerateText", Json.Load<DiffusionHistory> },
            { "Conversation", Json.Load<DiffusionHistory> },
            { "ExtractImage", Json.Load<ExtractHistory> },
            { "ExtractVideo", Json.Load<ExtractHistory> },
            { "UpscaleImage", Json.Load<UpscaleHistory> },
            { "UpscaleVideo", Json.Load<UpscaleHistory> },
            { "Interpolate", Json.Load<InterpolateHistory> },
            { "Audio", Json.Load<AudioHistory> },
            { "Text", Json.Load<TextHistory> },
            { "ImageCompose", Json.Load<ComposeHistory> },
            { "VideoCompose", Json.Load<ComposeHistory> }
        };
        private readonly Settings _settings;
        private readonly ILogger<HistoryService> _logger;
        private readonly ObservableCollection<IHistoryItem> _historyCollection;

        /// <summary>
        /// Initializes a new instance of the <see cref="HistoryService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="logger">The logger.</param>
        public HistoryService(Settings settings, ILogger<HistoryService> logger)
        {
            _settings = settings;
            _logger = logger;
            _historyCollection = [];
        }

        /// <summary>
        /// Gets the history collection.
        /// </summary>
        public ObservableCollection<IHistoryItem> HistoryCollection => _historyCollection;


        /// <summary>
        /// Loads the most recent history items from the history directory.
        /// </summary>
        public async Task InitializeAsync()
        {
            var timestamp = Stopwatch.GetTimestamp();
            _historyCollection.Clear();
            if (_settings.HistoryItems <= 0)
                return;

            var historyItems = await Task.Run(LoadHistoryItems);
            foreach (var historyItem in historyItems)
            {
                _historyCollection.Add(historyItem);
            }
            _logger.LogInformation("[HistoryService] [Initialize] Loading history complete, Items: {Count}, Elapsed: {Elapsed:c}", historyItems.Length, Stopwatch.GetElapsedTime(timestamp));
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


        public async Task<ImageInput> AddAsync(ImageInput image, ComposeHistory composeHistory)
        {
            if (_settings.HistoryItems <= 0)
                return image;

            var key = GetRandomName();
            var history = composeHistory with
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "png",
                MediaType = MediaType.Image,
                Timestamp = DateTime.Now,
                LastAccess = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"ImageCompose_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"ImageCompose_{key}.png"),
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"ImageCompose_{key}.png"),
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


        public async Task<VideoInputStream> AddAsync(VideoInputStream videoStream, ComposeHistory composeHistory)
        {
            if (_settings.HistoryItems <= 0)
                return videoStream;

            var key = GetRandomName();
            var history = composeHistory with
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "mp4",
                MediaType = MediaType.Video,
                Timestamp = DateTime.Now,
                LastAccess = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"VideoCompose_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"VideoCompose_{key}.mp4"),
                ThumbPath = Path.Combine(_settings.DirectoryHistory, $"VideoCompose_{key}.png"),
                Width = videoStream.Width,
                Height = videoStream.Height,
                FrameRate = videoStream.FrameRate,
                FrameCount = videoStream.FrameCount,
                Duration = videoStream.Duration,
            };

            return await AddVideoInternalAsync(videoStream, history);
        }


        public async Task<AudioInputStream> AddAsync(AudioInputStream audio, AudioHistory audioHistory)
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
                FilePath = Path.Combine(_settings.DirectoryHistory, $"Audio_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"Audio_{key}.wav"),
                //ThumbPath = Path.Combine(_settings.DirectoryHistory, $"Audio_{key}.png"),
                Duration = audio.Duration,
                SampleRate = audio.SampleRate
            };

            return await AddAudioInternalAsync(audio, history);
        }


        public async Task<AudioInputStream> AddAsync(AudioInputStream audio, DiffusionHistory diffusionHistory)
        {
            if (_settings.HistoryItems <= 0)
                return audio;

            var key = GetRandomName();
            var history = diffusionHistory with
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
                SampleRate = audio.SampleRate,
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
                FilePath = Path.Combine(_settings.DirectoryHistory, $"Text_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"Text_{key}.txt"),
                //ThumbPath = Path.Combine(_settings.DirectoryHistory, $"GenerateText_{key}.png"),
                InputLength = text.Length,
                InputText = text.Text
            };

            return await AddTextInternalAsync(text, history);
        }


        public async Task<TextInput> AddAsync(TextInput text, DiffusionHistory diffusionHistory)
        {
            if (_settings.HistoryItems <= 0)
                return text;

            var key = GetRandomName();
            var history = diffusionHistory with
            {
                Id = key,
                Version = HistoryVersion,
                Extension = "txt",
                MediaType = MediaType.Text,
                Timestamp = DateTime.Now,
                LastAccess = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"GenerateText_{key}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"GenerateText_{key}.txt"),
                Length = text.Length
            };

            return await AddTextInternalAsync(text, history);
        }


        public async Task AddAsync(DiffusionHistory diffusionHistory, string conversationId, string conversationMarkdown)
        {
            if (_settings.HistoryItems <= 0)
                return;

            var timestamp = DateTime.Now;
            var lastAccess = DateTime.Now;
            var existing = _historyCollection.FirstOrDefault(x => x.Id == conversationId);
            if (existing != null)
            {
                timestamp = existing.Timestamp;
                _historyCollection.Remove(existing);
            }

            var history = diffusionHistory with
            {
                Id = conversationId,
                Version = HistoryVersion,
                Extension = "txt",
                MediaType = MediaType.Text,
                Timestamp = timestamp,
                LastAccess = lastAccess,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"Conversation_{conversationId}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"Conversation_{conversationId}.txt"),
                Length = diffusionHistory.Options.Conversation.Count
            };

            await Json.SaveAsync(history.FilePath, history);
            await File.WriteAllTextAsync(history.MediaPath, conversationMarkdown);
            AddHistoryItem(history);
        }


        public Task BranchAsync(string conversationId, string newConversationId)
        {
            var existing = _historyCollection.FirstOrDefault(x => x.Id == conversationId) as DiffusionHistory;
            if (existing == null)
                return Task.CompletedTask;

            var branchHistory = existing with
            {
                Id = newConversationId,
                Timestamp = DateTime.Now,
                LastAccess = DateTime.Now,
                FilePath = Path.Combine(_settings.DirectoryHistory, $"Conversation_{newConversationId}.json"),
                MediaPath = Path.Combine(_settings.DirectoryHistory, $"Conversation_{newConversationId}.txt"),
            };

            File.Copy(existing.FilePath, branchHistory.FilePath);
            File.Copy(existing.MediaPath, branchHistory.MediaPath);
            AddHistoryItem(branchHistory);
            return Task.CompletedTask;
        }



        public string GetRandomName()
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
            await Json.SaveAsync<T>(history.FilePath, history);
            AddHistoryItem(history);
            return newStream;
        }


        private async Task<AudioInputStream> AddAudioInternalAsync<T>(AudioInputStream audioStream, T history) where T : IHistoryItem
        {
            var newStream = await audioStream.MoveAsync(history.MediaPath);
            await Json.SaveAsync<T>(history.FilePath, history);
            AddHistoryItem(history);
            return newStream;
        }


        private async Task<TextInput> AddTextInternalAsync<T>(TextInput text, T history) where T : IHistoryItem
        {
            await text.SaveAsync(history.MediaPath);
            await Json.SaveAsync<T>(history.FilePath, history);
            AddHistoryItem(history);
            return text;
        }


        /// <summary>
        /// Loads the newest history files in parallel, preserving their order.
        /// </summary>
        private IHistoryItem[] LoadHistoryItems()
        {
            var historyFiles = new DirectoryInfo(_settings.DirectoryHistory)
                .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(x => x.CreationTimeUtc)
                .Take(_settings.HistoryItems)
                .ToArray();

            var historyItems = new IHistoryItem[historyFiles.Length];
            Parallel.For(0, historyFiles.Length, index => historyItems[index] = LoadHistoryItem(historyFiles[index]));
            return historyItems
                .Where(x => x != null)
                .ToArray();
        }


        /// <summary>
        /// Loads a history file, deleting it if its media no longer exists.
        /// </summary>
        /// <param name="historyFile">The history file.</param>
        private static IHistoryItem LoadHistoryItem(FileInfo historyFile)
        {
            var separator = historyFile.Name.IndexOf('_');
            if (separator <= 0 || !_historyLoaders.TryGetValue(historyFile.Name.Substring(0, separator), out var historyLoader))
                return null;

            var historyItem = historyLoader(historyFile.FullName);
            if (historyItem == null || historyItem.Version != HistoryVersion)
                return null;

            historyItem.FilePath = historyFile.FullName;
            historyItem.MediaPath = Path.ChangeExtension(historyFile.FullName, historyItem.Extension);
            historyItem.ThumbPath = Path.ChangeExtension(historyFile.FullName, ".png");
            if (historyItem is RecentHistory recentHistory)
            {
                historyItem.MediaPath = recentHistory.OriginalPath;
                if (historyItem.MediaType == MediaType.Image)
                    historyItem.ThumbPath = recentHistory.OriginalPath;
            }

            if (!File.Exists(historyItem.MediaPath))
            {
                FileHelper.DeleteFiles(historyItem.FilePath, historyItem.ThumbPath);
                return null;
            }
            return historyItem;
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
        Task<ImageInput> AddAsync(ImageInput image, ComposeHistory history);


        Task<VideoInputStream> AddAsync(VideoInputStream videoStream, DiffusionHistory history);
        Task<VideoInputStream> AddAsync(VideoInputStream videoStream, ExtractHistory history);
        Task<VideoInputStream> AddAsync(VideoInputStream videoStream, UpscaleHistory history);
        Task<VideoInputStream> AddAsync(VideoInputStream videoStream, InterpolateHistory history);
        Task<VideoInputStream> AddAsync(VideoInputStream videoStream, ComposeHistory history);


        Task<AudioInputStream> AddAsync(AudioInputStream audio, AudioHistory history);
        Task<AudioInputStream> AddAsync(AudioInputStream audio, DiffusionHistory history);


        Task<TextInput> AddAsync(TextInput text, TextHistory history);
        Task<TextInput> AddAsync(TextInput text, DiffusionHistory history);
        Task AddAsync(DiffusionHistory diffusionHistory, string conversationId, string conversationMarkdown);
        string GetRandomName();
        Task BranchAsync(string conversationId, string newConversationId);
    }

}
