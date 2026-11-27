using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TensorStack.Common;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Dialogs;
using TensorStack.WPF.Image;
using TensorStack.WPF.Resources;
using TensorStack.WPF.Services;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for ContextControl.xaml
    /// </summary>
    public partial class ContextControl : BaseControl
    {
        private int _textMaxCount = 0;
        private int _imageMaxCount = 0;
        private int _audioMaxCount = 0;
        private int _videoMaxCount = 0;
        private Guid _contextVersion;
        private ContextCache _currentContext;
        private ContextItemModel _selectedContextItem;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContextControl"/> class.
        /// </summary>
        public ContextControl()
        {
            ContextCollection = [];
            _contextVersion = Guid.NewGuid();
            AddTextCommand = new AsyncRelayCommand(AddTextAsync, () => _textMaxCount > 0);
            AddImageCommand = new AsyncRelayCommand(AddImageAsync, () => _imageMaxCount > 0);
            AddAudioCommand = new AsyncRelayCommand(AddAudioAsync, () => _audioMaxCount > 0);
            AddVideoCommand = new AsyncRelayCommand(AddVideoAsync, () => _videoMaxCount > 0);
            CaptureImageCommand = new AsyncRelayCommand(CaptureImageAsync, () => _imageMaxCount > 0);
            CaptureAudioCommand = new AsyncRelayCommand(CaptureAudioAsync, () => _audioMaxCount > 0);
            CaptureVideoCommand = new AsyncRelayCommand(CaptureVideoAsync, () => _videoMaxCount > 0);
            ClearCommand = new AsyncRelayCommand(ClearAsync, () => ContextCollection.Count > 0 && ContextCollection.Any(x => !x.IsReadOnly));
            RemoveCommand = new AsyncRelayCommand<ContextItemModel>(RemoveAsync);
            InitializeComponent();
        }

        public static readonly DependencyProperty IsControlBusyProperty = DependencyProperty.Register(nameof(IsControlBusy), typeof(bool), typeof(ContextControl), new PropertyMetadata(false));
        public AsyncRelayCommand AddTextCommand { get; }
        public AsyncRelayCommand AddImageCommand { get; }
        public AsyncRelayCommand AddAudioCommand { get; }
        public AsyncRelayCommand AddVideoCommand { get; }
        public AsyncRelayCommand CaptureImageCommand { get; }
        public AsyncRelayCommand CaptureAudioCommand { get; }
        public AsyncRelayCommand CaptureVideoCommand { get; }
        public AsyncRelayCommand ClearCommand { get; }
        public AsyncRelayCommand<ContextItemModel> RemoveCommand { get; }
        public ObservableCollection<ContextItemModel> ContextCollection { get; }
        public ContextCache CurrentContext => _currentContext;

        public bool IsControlBusy
        {
            get { return (bool)GetValue(IsControlBusyProperty); }
            set { SetValue(IsControlBusyProperty, value); }
        }

        public ContextItemModel SelectedContextItem
        {
            get { return _selectedContextItem; }
            set { SetProperty(ref _selectedContextItem, value); }
        }

        public int TextMaxCount
        {
            get { return _textMaxCount; }
            set { SetProperty(ref _textMaxCount, value); UpdateContextItems(); }
        }

        public int ImageMaxCount
        {
            get { return _imageMaxCount; }
            set { SetProperty(ref _imageMaxCount, value); UpdateContextItems(); }
        }

        public int AudioMaxCount
        {
            get { return _audioMaxCount; }
            set { SetProperty(ref _audioMaxCount, value); UpdateContextItems(); }
        }

        public int VideoMaxCount
        {
            get { return _videoMaxCount; }
            set { SetProperty(ref _videoMaxCount, value); UpdateContextItems(); }
        }

        /// <summary>
        /// Gets or sets the sample rate audio is captured at, zero for the default.
        /// </summary>
        public int CaptureSampleRate { get; set; }

        /// <summary>
        /// Gets or sets the channels audio is captured with, zero for the default.
        /// </summary>
        public int CaptureChannels { get; set; }


        public PromptInputs GetPromptInputs(string prompt)
        {
            var context = CreateContext();
            return context.GetPromptInputs(prompt);
        }


        public PromptInputs GetPromptInputs(string prompt, Collection<ConversationModel> conversation)
        {
            SetContextReadOnly(true);
            var context = CreateContext();
            var promptInputs = context.GetPromptInputs(prompt, _currentContext, conversation);
            SetCurrentContext(context);
            return promptInputs;
        }


        public void ReleaseContext()
        {
            SetCurrentContext(null);
            SetContextReadOnly(false);
        }


        public async Task CreateFromConversation(Collection<ConversationModel> conversation)
        {
            try
            {
                IsControlBusy = true;

                var imageIndex = new Dictionary<int, string>();
                var audioIndex = new Dictionary<int, string>();
                var videoIndex = new Dictionary<int, string>();
                foreach (var message in conversation)
                {
                    if (!message.ImageIndex.IsNullOrEmpty())
                    {
                        foreach (var image in message.ImageIndex)
                        {
                            if (imageIndex.ContainsKey(image.Key))
                                continue;

                            imageIndex.Add(image.Key, image.Value);
                        }
                    }

                    if (!message.AudioIndex.IsNullOrEmpty())
                    {
                        foreach (var audio in message.AudioIndex)
                        {
                            if (audioIndex.ContainsKey(audio.Key))
                                continue;

                            audioIndex.Add(audio.Key, audio.Value);
                        }
                    }

                    if (!message.VideoIndex.IsNullOrEmpty())
                    {
                        foreach (var video in message.VideoIndex)
                        {
                            if (videoIndex.ContainsKey(video.Key))
                                continue;

                            videoIndex.Add(video.Key, video.Value);
                        }
                    }
                }

                ContextCollection.Clear();
                foreach (var image in imageIndex)
                {
                    ContextCollection.Add(new ContextItemModel
                    {
                        Filename = image.Value,
                        MediaType = MediaType.Image,
                        Id = ContextCollection.Count,
                        Image = await ImageInput.CreateAsync(image.Value)
                    });
                }
                foreach (var audio in audioIndex)
                {
                    ContextCollection.Add(new ContextItemModel
                    {
                        Filename = audio.Value,
                        MediaType = MediaType.Audio,
                        Id = ContextCollection.Count,
                        Audio = await AudioInputStream.CreateAsync(audio.Value)
                    });
                }
                foreach (var video in videoIndex)
                {
                    ContextCollection.Add(new ContextItemModel
                    {
                        Filename = video.Value,
                        MediaType = MediaType.Video,
                        Id = ContextCollection.Count,
                        Video = await VideoInputStream.CreateAsync(video.Value)
                    });
                }

                UpdateContextItems();
                _currentContext = CreateContext();
                SetContextReadOnly(true);
            }
            finally
            {
                IsControlBusy = false;
            }
        }


        private void SetCurrentContext(ContextCache context)
        {
            _currentContext = context;
            NotifyPropertyChanged(nameof(CurrentContext));
        }


        private void SetContextReadOnly(bool isReadonly)
        {
            foreach (var item in ContextCollection)
            {
                item.IsReadOnly = isReadonly;
            }
        }


        private ContextCache CreateContext()
        {
            return new ContextCache
            {
                Version = _contextVersion,

                TextMaxCount = _textMaxCount,
                TextContext = ContextCollection
                    .Where(x => x.MediaType == MediaType.Text)
                    .Select(x => x.Text)
                    .ToList(),

                ImageMaxCount = _imageMaxCount,
                ImageContext = ContextCollection
                    .Where(x => x.MediaType == MediaType.Image)
                    .Select(x => x.Image)
                    .ToList(),

                AudioMaxCount = _audioMaxCount,
                AudioContext = ContextCollection
                    .Where(x => x.MediaType == MediaType.Audio)
                    .Select(x => x.Audio)
                    .ToList(),

                VideoMaxCount = _videoMaxCount,
                VideoContext = ContextCollection
                    .Where(x => x.MediaType == MediaType.Video)
                    .Select(x => x.Video)
                    .ToList()
            };
        }


        private async Task AddTextAsync()
        {
            var filename = await DialogService.OpenFileAsync(AppDefault.ImportDocument, filter: $"{AppDefault.Documents}|*.txt;*.md;*.json;*.csv;*.pdf;*.docx;*.rtf|{Default.AllFiles}|*.*");
            if (!string.IsNullOrEmpty(filename))
            {
                await AddTextAsync(filename);
            }
        }


        private async Task AddTextAsync(string filename)
        {
            if (ContextCollection.Any(x => !x.IsIgnored && x.MediaType == MediaType.Text && filename.Equals(x.Filename, StringComparison.OrdinalIgnoreCase)))
                return;

            var parsedDocument = await DocumentManager.ParseAsync(filename);
            if (string.IsNullOrWhiteSpace(parsedDocument))
                return;

            ContextCollection.Add(new ContextItemModel
            {
                Filename = filename,
                MediaType = MediaType.Text,
                Id = ContextCollection.Count,
                Text = new TextInput(parsedDocument, filename)
            });
            UpdateContextItems();
        }


        private async Task AddImageAsync()
        {
            var filename = await DialogService.OpenFileAsync(AppDefault.ImportImage, filter: TensorStack.WPF.Common.ImageFileFilter);
            if (!string.IsNullOrEmpty(filename))
            {
                await AddImageAsync(filename);
            }

        }


        private async Task AddImageAsync(string filename)
        {
            if (ContextCollection.Any(x => !x.IsIgnored && x.MediaType == MediaType.Image && filename.Equals(x.Filename, StringComparison.OrdinalIgnoreCase)))
                return;

            ContextCollection.Add(new ContextItemModel
            {
                Filename = filename,
                MediaType = MediaType.Image,
                Id = ContextCollection.Count,
                Image = await ImageInput.CreateAsync(filename)
            });

            UpdateContextItems();
        }


        private async Task AddAudioAsync()
        {
            var filename = await DialogService.OpenFileAsync(AppDefault.ImportAudio, "Audio", filter: TensorStack.WPF.Common.AudioFileFilter, defualtExt: "wav");
            if (!string.IsNullOrEmpty(filename))
            {
                await AddAudioAsync(filename);
            }
        }


        private async Task AddAudioAsync(string filename)
        {
            if (ContextCollection.Any(x => !x.IsIgnored && x.MediaType == MediaType.Audio && filename.Equals(x.Filename, StringComparison.OrdinalIgnoreCase)))
                return;

            ContextCollection.Add(new ContextItemModel
            {
                Filename = filename,
                MediaType = MediaType.Audio,
                Id = ContextCollection.Count,
                Audio = await AudioInputStream.CreateAsync(filename)
            });

            UpdateContextItems();
        }


        private async Task AddVideoAsync()
        {
            var filename = await DialogService.OpenFileAsync(AppDefault.ImportVideo, filter: TensorStack.WPF.Common.VideoFileFilter);
            if (!string.IsNullOrEmpty(filename))
            {
                await AddVideoAsync(filename);
            }
        }


        private async Task AddVideoAsync(string filename)
        {
            if (ContextCollection.Any(x => !x.IsIgnored && x.MediaType == MediaType.Video && filename.Equals(x.Filename, StringComparison.OrdinalIgnoreCase)))
                return;

            ContextCollection.Add(new ContextItemModel
            {
                Filename = filename,
                MediaType = MediaType.Video,
                Id = ContextCollection.Count,
                Video = await VideoInputStream.CreateAsync(filename)
            });

            UpdateContextItems();
        }


        /// <summary>
        /// Captures an image from a camera and adds it to the context.
        /// </summary>
        private async Task CaptureImageAsync()
        {
            var filename = await DialogService.CaptureFileAsync(MediaType.Image, GetCaptureOptions());
            if (!string.IsNullOrEmpty(filename))
            {
                await AddImageAsync(filename);
            }
        }


        /// <summary>
        /// Records audio from a microphone and adds it to the context.
        /// </summary>
        private async Task CaptureAudioAsync()
        {
            var filename = await DialogService.CaptureFileAsync(MediaType.Audio, GetCaptureOptions());
            if (!string.IsNullOrEmpty(filename))
            {
                await AddAudioAsync(filename);
            }
        }


        /// <summary>
        /// Records a video from a camera and adds it to the context.
        /// </summary>
        private async Task CaptureVideoAsync()
        {
            var filename = await DialogService.CaptureFileAsync(MediaType.Video, GetCaptureOptions());
            if (!string.IsNullOrEmpty(filename))
            {
                await AddVideoAsync(filename);
            }
        }


        /// <summary>
        /// Gets the capture options, audio is recorded in the format the model uses.
        /// The capture is a file in the temporary directory, the conversation refers to it like any other file.
        /// </summary>
        private CaptureOptions GetCaptureOptions()
        {
            var captureOptions = new CaptureOptions();
            if (CaptureSampleRate > 0)
                captureOptions = captureOptions with { SampleRate = CaptureSampleRate };
            if (CaptureChannels > 0)
                captureOptions = captureOptions with { Channels = CaptureChannels };

            return captureOptions;
        }


        private Task RemoveAsync(ContextItemModel model)
        {
            ContextCollection.Remove(model);
            UpdateContextItems();
            return Task.CompletedTask;
        }


        private Task ClearAsync()
        {
            var itemsToRemove = ContextCollection.Where(x => !x.IsReadOnly).ToArray();
            foreach (var item in itemsToRemove)
            {
                ContextCollection.Remove(item);
            }
            _contextVersion = Guid.NewGuid();
            SetCurrentContext(null);
            return Task.CompletedTask;
        }


        private void UpdateContextItems()
        {
            foreach (var item in ContextCollection.Where(x => x.MediaType != MediaType.Text))
            {
                item.IsIgnored = true;
            }
            foreach (var item in ContextCollection.Where(x => x.MediaType == MediaType.Image).TakeLast(_imageMaxCount))
            {
                item.IsIgnored = false;
            }
            foreach (var item in ContextCollection.Where(x => x.MediaType == MediaType.Audio).TakeLast(_audioMaxCount))
            {
                item.IsIgnored = false;
            }
            foreach (var item in ContextCollection.Where(x => x.MediaType == MediaType.Video).TakeLast(_videoMaxCount))
            {
                item.IsIgnored = false;
            }
            _contextVersion = Guid.NewGuid();
        }


        protected override async void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                try
                {
                    IsControlBusy = true;
                    var filename = ((string[])e.Data.GetData(DataFormats.FileDrop))?.FirstOrDefault();
                    if (File.Exists(filename))
                    {
                        var extension = Path.GetExtension(filename);
                        if (TensorStack.WPF.Common.ImageFileExtensions.Contains(extension))
                        {
                            await AddImageAsync(filename);
                        }
                        else if (TensorStack.WPF.Common.VideoFileExtensions.Contains(extension))
                        {
                            await AddVideoAsync(filename);
                        }
                        else if (TensorStack.WPF.Common.AudioFileExtensions.Contains(extension))
                        {
                            await AddAudioAsync(filename);
                        }
                        else
                        {
                            await AddTextAsync(filename);
                        }
                    }
                    CommandManager.InvalidateRequerySuggested();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Exception] [ContextControl] OnDrop: {ex.Message}");
                }
                finally
                {
                    IsControlBusy = false;
                }
            }
        }

    }

    internal static class ContextExtensions
    {
        internal static PromptInputs GetPromptInputs(this ContextCache context, string prompt)
        {
            var imageIndex = context.ImageContext.GetIndexedInputs(context.ImageMaxCount);
            var audioIndex = context.AudioContext.GetIndexedInputs(context.AudioMaxCount);
            var videoIndex = context.VideoContext.GetIndexedInputs(context.VideoMaxCount);
            prompt = $"{context.TextContext.FormatTextContext()}{prompt}";
            return new PromptInputs
            {
                Prompt = prompt,
                ImageIndex = imageIndex,
                AudioIndex = audioIndex,
                VideoIndex = videoIndex,
                TextContext = [.. context.TextContext.TakeLast(context.TextMaxCount)],
                ImageContext = [.. context.ImageContext.TakeLast(context.ImageMaxCount)],
                AudioContext = [.. context.AudioContext.TakeLast(context.AudioMaxCount)],
                VideoContext = [.. context.VideoContext.TakeLast(context.VideoMaxCount)],
            };
        }


        internal static PromptInputs GetPromptInputs(this ContextCache context, string prompt, ContextCache previousContext, Collection<ConversationModel> conversation)
        {
            var imageIndex = default(Dictionary<int, string>);
            var audioIndex = default(Dictionary<int, string>);
            var videoIndex = default(Dictionary<int, string>);
            if (conversation.Count == 0)
            {
                imageIndex = context.ImageContext.GetIndexedInputs(context.ImageMaxCount);
                audioIndex = context.AudioContext.GetIndexedInputs(context.AudioMaxCount);
                videoIndex = context.VideoContext.GetIndexedInputs(context.VideoMaxCount);
                prompt = $"{context.TextContext.FormatTextContext()}\n{prompt}";
            }
            else
            {
                if (context.HasChanged(previousContext))
                {
                    prompt = context.RebuildTextIndexes(previousContext, prompt);
                    imageIndex = context.RebuildImageIndexes(previousContext, conversation);
                    audioIndex = context.RebuildAudioIndexes(previousContext, conversation);
                    videoIndex = context.RebuildVideoIndexes(previousContext, conversation);
                }
            }
            return new PromptInputs
            {
                Prompt = prompt,
                ImageIndex = imageIndex,
                AudioIndex = audioIndex,
                VideoIndex = videoIndex,
                TextContext = [.. context.TextContext.TakeLast(context.TextMaxCount)],
                ImageContext = [.. context.ImageContext.TakeLast(context.ImageMaxCount)],
                AudioContext = [.. context.AudioContext.TakeLast(context.AudioMaxCount)],
                VideoContext = [.. context.VideoContext.TakeLast(context.VideoMaxCount)],
            };
        }


        private static string RebuildTextIndexes(this ContextCache context, ContextCache previousContext, string prompt)
        {
            if (previousContext is null)
                return prompt;

            if (context.TextCount > previousContext.TextCount)
            {
                prompt = $"{context.TextContext.Skip(previousContext.TextCount).FormatTextContext()}\n{prompt}";
            }
            return prompt;
        }


        private static Dictionary<int, string> RebuildImageIndexes(this ContextCache context, ContextCache previousContext, Collection<ConversationModel> conversation)
        {
            if (previousContext is null || conversation.Count == 0)
                return null;

            var newStart = Math.Max(0, context.ImageCount - context.ImageMaxCount);
            var oldStart = Math.Max(0, previousContext.ImageCount - previousContext.ImageMaxCount);
            var shift = newStart - oldStart;
            if (shift != 0)
            {
                foreach (var message in conversation.Where(x => !x.ImageIndex.IsNullOrEmpty()))
                {
                    message.ImageIndex = shift > 0
                        ? message.ImageIndex.DecrementKeys(shift)
                        : message.ImageIndex.IncrementKeys(-shift);
                }
            }

            var imageIndex = new Dictionary<int, string>();
            for (int i = previousContext.ImageCount; i < context.ImageCount; i++)
            {
                var index = i - newStart;
                if (index >= 0 && index < context.ImageMaxCount)
                {
                    imageIndex.Add(index, context.ImageContext[index].SourceFile);
                }
            }
            return imageIndex.Count == 0 ? null : imageIndex;
        }


        private static Dictionary<int, string> RebuildAudioIndexes(this ContextCache context, ContextCache previousContext, Collection<ConversationModel> conversation)
        {
            if (previousContext is null || conversation.Count == 0)
                return null;

            var newStart = Math.Max(0, context.AudioCount - context.AudioMaxCount);
            var oldStart = Math.Max(0, previousContext.AudioCount - previousContext.AudioMaxCount);
            var shift = newStart - oldStart;
            if (shift != 0)
            {
                foreach (var message in conversation.Where(x => !x.AudioIndex.IsNullOrEmpty()))
                {
                    message.AudioIndex = shift > 0
                        ? message.AudioIndex.DecrementKeys(shift)
                        : message.AudioIndex.IncrementKeys(-shift);
                }
            }

            var audioIndex = new Dictionary<int, string>();
            for (int i = previousContext.AudioCount; i < context.AudioCount; i++)
            {
                var index = i - newStart;
                if (index >= 0 && index < context.AudioMaxCount)
                {
                    audioIndex.Add(index, context.AudioContext[index].SourceFile);
                }
            }
            return audioIndex.Count == 0 ? null : audioIndex;
        }


        private static Dictionary<int, string> RebuildVideoIndexes(this ContextCache context, ContextCache previousContext, Collection<ConversationModel> conversation)
        {
            if (previousContext is null || conversation.Count == 0)
                return null;

            var newStart = Math.Max(0, context.VideoCount - context.VideoMaxCount);
            var oldStart = Math.Max(0, previousContext.VideoCount - previousContext.VideoMaxCount);
            var shift = newStart - oldStart;
            if (shift != 0)
            {
                foreach (var message in conversation.Where(x => !x.VideoIndex.IsNullOrEmpty()))
                {
                    message.VideoIndex = shift > 0
                        ? message.VideoIndex.DecrementKeys(shift)
                        : message.VideoIndex.IncrementKeys(-shift);
                }
            }

            var videoIndex = new Dictionary<int, string>();
            for (int i = previousContext.VideoCount; i < context.VideoCount; i++)
            {
                var index = i - newStart;
                if (index >= 0 && index < context.VideoMaxCount)
                {
                    videoIndex.Add(index, context.VideoContext[index].SourceFile);
                }
            }
            return videoIndex.Count == 0 ? null : videoIndex;
        }


        private static string FormatTextContext(this IEnumerable<TextInput> contextCollection)
        {
            if (contextCollection.IsNullOrEmpty())
                return string.Empty;

            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("<context>");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("=== BEGIN REFERENCE DOCUMENTS ===");
            contextBuilder.AppendLine();
            foreach (var (i, textInput) in contextCollection.Index())
            {
                var filename = Path.GetFileName(textInput.SourceFile);
                contextBuilder.AppendLine($"=== DOCUMENT: {filename} ===");
                contextBuilder.AppendLine();
                contextBuilder.AppendLine(textInput.Text.TrimEnd());
                contextBuilder.AppendLine();
                contextBuilder.AppendLine("=== END DOCUMENT ===");
                contextBuilder.AppendLine();
            }
            contextBuilder.AppendLine("=== END REFERENCE DOCUMENTS ===");
            contextBuilder.AppendLine("</context>");
            contextBuilder.AppendLine();
            return contextBuilder.ToString();
        }


        private static Dictionary<int, string> IncrementKeys(this Dictionary<int, string> dictionary, int n)
        {
            return dictionary.ToDictionary(kvp => kvp.Key + n, kvp => kvp.Value);
        }


        private static Dictionary<int, string> DecrementKeys(this Dictionary<int, string> dictionary, int n)
        {
            return dictionary.ToDictionary(kvp => kvp.Key - n, kvp => kvp.Value);
        }
    }


    public sealed class ContextItemModel : BaseModel
    {
        private bool _isReadOnly;
        private bool _isIgnored;
        private string _toolTipMessage;

        public int Id { get; set; }
        public MediaType MediaType { get; set; }
        public string Filename { get; set; }
        public TextInput Text { get; set; }
        public ImageInput Image { get; set; }
        public AudioInputStream Audio { get; set; }
        public VideoInputStream Video { get; set; }

        public bool IsReadOnly
        {
            get { return _isReadOnly; }
            set { SetProperty(ref _isReadOnly, value); }
        }

        public bool IsIgnored
        {
            get { return _isIgnored; }
            set { SetProperty(ref _isIgnored, value); ToolTipMessage = GetToolTipMessage(); }
        }

        public string ToolTipMessage
        {
            get { return _toolTipMessage; }
            set { SetProperty(ref _toolTipMessage, value); }
        }


        private string GetToolTipMessage()
        {
            if (IsIgnored)
            {
                return string.Format(AppDefault.MaxInputsReached, MediaType.GetLocalizedName());
            }
            return Filename;
        }
    }

    public sealed class ContextCache
    {
        public Guid Version { get; set; }
        public List<TextInput> TextContext { get; init; }
        public List<ImageInput> ImageContext { get; init; }
        public List<AudioInputStream> AudioContext { get; init; }
        public List<VideoInputStream> VideoContext { get; init; }

        public int TextCount => TextContext?.Count ?? 0;
        public int ImageCount => ImageContext?.Count ?? 0;
        public int AudioCount => AudioContext?.Count ?? 0;
        public int VideoCount => VideoContext?.Count ?? 0;

        public int TextMaxCount { get; init; }
        public int ImageMaxCount { get; init; }
        public int AudioMaxCount { get; init; }
        public int VideoMaxCount { get; init; }

        public bool HasChanged(ContextCache previous)
        {
            return previous == null
                || Version != previous.Version
                || ImageMaxCount != previous.ImageMaxCount
                || AudioMaxCount != previous.AudioMaxCount
                || VideoMaxCount != previous.VideoMaxCount;
        }
    }
}
