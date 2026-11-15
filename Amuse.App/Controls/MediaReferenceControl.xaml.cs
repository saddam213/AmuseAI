using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TensorStack.Common;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Image;
using TensorStack.WPF.Services;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for MediaReferenceControl.xaml
    /// </summary>
    public partial class MediaReferenceControl : BaseControl
    {
        private ReferenceItemModel _selectedReferenceItem;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaReferenceControl"/> class.
        /// </summary>
        public MediaReferenceControl()
        {
            ReferenceCollection = [];
            AddImageCommand = new AsyncRelayCommand(AddImageAsync, () => CanAdd(MediaType.Image));
            AddVideoCommand = new AsyncRelayCommand(AddVideoAsync, () => CanAdd(MediaType.Video));
            AddAudioCommand = new AsyncRelayCommand(AddAudioAsync, () => CanAdd(MediaType.Audio));
            ClearCommand = new AsyncRelayCommand(ClearAsync, () => ReferenceCollection.Count > 0);
            RemoveCommand = new AsyncRelayCommand<ReferenceItemModel>(RemoveAsync);
            InitializeComponent();
        }

        public static readonly DependencyProperty IsControlBusyProperty = DependencyProperty.Register(nameof(IsControlBusy), typeof(bool), typeof(MediaReferenceControl), new PropertyMetadata(false));
        public static readonly DependencyProperty ImageMaxCountProperty = DependencyProperty.Register(nameof(ImageMaxCount), typeof(int), typeof(MediaReferenceControl), new PropertyMetadata<MediaReferenceControl>((c) => c.OnMaxCountChanged()));
        public static readonly DependencyProperty VideoMaxCountProperty = DependencyProperty.Register(nameof(VideoMaxCount), typeof(int), typeof(MediaReferenceControl), new PropertyMetadata<MediaReferenceControl>((c) => c.OnMaxCountChanged()));
        public static readonly DependencyProperty AudioMaxCountProperty = DependencyProperty.Register(nameof(AudioMaxCount), typeof(int), typeof(MediaReferenceControl), new PropertyMetadata<MediaReferenceControl>((c) => c.OnMaxCountChanged()));

        public AsyncRelayCommand AddImageCommand { get; }
        public AsyncRelayCommand AddVideoCommand { get; }
        public AsyncRelayCommand AddAudioCommand { get; }
        public AsyncRelayCommand ClearCommand { get; }
        public AsyncRelayCommand<ReferenceItemModel> RemoveCommand { get; }
        public ObservableCollection<ReferenceItemModel> ReferenceCollection { get; }

        public int ImageCount => GetCount(MediaType.Image);
        public int VideoCount => GetCount(MediaType.Video);
        public int AudioCount => GetCount(MediaType.Audio);

        public bool IsControlBusy
        {
            get { return (bool)GetValue(IsControlBusyProperty); }
            set { SetValue(IsControlBusyProperty, value); }
        }

        public int ImageMaxCount
        {
            get { return (int)GetValue(ImageMaxCountProperty); }
            set { SetValue(ImageMaxCountProperty, value); }
        }

        public int VideoMaxCount
        {
            get { return (int)GetValue(VideoMaxCountProperty); }
            set { SetValue(VideoMaxCountProperty, value); }
        }

        public int AudioMaxCount
        {
            get { return (int)GetValue(AudioMaxCountProperty); }
            set { SetValue(AudioMaxCountProperty, value); }
        }

        public ReferenceItemModel SelectedReferenceItem
        {
            get { return _selectedReferenceItem; }
            set { SetProperty(ref _selectedReferenceItem, value); }
        }


        /// <summary>
        /// Gets the image references, in order, within the current model limit.
        /// </summary>
        public List<ImageInput> GetImageReferences()
        {
            return GetReferences(MediaType.Image)
                .Select(x => x.Image)
                .ToList();
        }


        /// <summary>
        /// Gets the video references, in order, within the current model limit.
        /// </summary>
        public List<VideoInputStream> GetVideoReferences()
        {
            return GetReferences(MediaType.Video)
                .Select(x => x.Video)
                .ToList();
        }


        /// <summary>
        /// Gets the audio references, in order, within the current model limit.
        /// </summary>
        public List<AudioInputStream> GetAudioReferences()
        {
            return GetReferences(MediaType.Audio)
                .Select(x => x.Audio)
                .ToList();
        }


        /// <summary>
        /// Called when a model maximum count changes.
        /// </summary>
        private Task OnMaxCountChanged()
        {
            UpdateReferenceItems();
            CommandManager.InvalidateRequerySuggested();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Adds an image reference.
        /// </summary>
        private async Task AddImageAsync()
        {
            var filename = await DialogService.OpenFileAsync("Import Image", filter: "Image Files|*.bmp;*.jpg;*.jpeg;*.png;*.gif;*.tif;*.tiff|All Files|*.*");
            if (!string.IsNullOrEmpty(filename))
            {
                await AddImageAsync(filename);
            }
        }


        /// <summary>
        /// Adds an image reference.
        /// </summary>
        /// <param name="filename">The filename.</param>
        private async Task AddImageAsync(string filename)
        {
            if (!CanAdd(MediaType.Image, filename))
                return;

            AddReference(new ReferenceItemModel
            {
                Filename = filename,
                MediaType = MediaType.Image,
                Image = await ImageInput.CreateAsync(filename)
            });
        }


        /// <summary>
        /// Adds a video reference.
        /// </summary>
        private async Task AddVideoAsync()
        {
            var filename = await DialogService.OpenFileAsync("Import Video", filter: "Videos|*.mp4;*.gif;|All Files|*.*;");
            if (!string.IsNullOrEmpty(filename))
            {
                await AddVideoAsync(filename);
            }
        }


        /// <summary>
        /// Adds a video reference.
        /// </summary>
        /// <param name="filename">The filename.</param>
        private async Task AddVideoAsync(string filename)
        {
            if (!CanAdd(MediaType.Video, filename))
                return;

            AddReference(new ReferenceItemModel
            {
                Filename = filename,
                MediaType = MediaType.Video,
                Video = await VideoInputStream.CreateAsync(filename)
            });
        }


        /// <summary>
        /// Adds an audio reference.
        /// </summary>
        private async Task AddAudioAsync()
        {
            var filename = await DialogService.OpenFileAsync("Import Audio", "Audio", filter: "Audio/Video files (*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.mp4;*.mov;*.mkv;*.webm)|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.mp4;*.mov;*.mkv;*.webm", defualtExt: "wav");
            if (!string.IsNullOrEmpty(filename))
            {
                await AddAudioAsync(filename);
            }
        }


        /// <summary>
        /// Adds an audio reference.
        /// </summary>
        /// <param name="filename">The filename.</param>
        private async Task AddAudioAsync(string filename)
        {
            if (!CanAdd(MediaType.Audio, filename))
                return;

            AddReference(new ReferenceItemModel
            {
                Filename = filename,
                MediaType = MediaType.Audio,
                Audio = await AudioInputStream.CreateAsync(filename)
            });
        }


        /// <summary>
        /// Removes a reference.
        /// </summary>
        /// <param name="model">The model.</param>
        private Task RemoveAsync(ReferenceItemModel model)
        {
            ReferenceCollection.Remove(model);
            UpdateReferenceItems();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Clears all references.
        /// </summary>
        private Task ClearAsync()
        {
            ReferenceCollection.Clear();
            UpdateReferenceItems();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Adds a reference to the collection.
        /// </summary>
        /// <param name="model">The model.</param>
        private void AddReference(ReferenceItemModel model)
        {
            model.Id = ReferenceCollection.Count == 0 ? 0 : ReferenceCollection.Max(x => x.Id) + 1;
            ReferenceCollection.Add(model);
            UpdateReferenceItems();
        }


        /// <summary>
        /// Determines whether another reference of the specified type can be added.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        private bool CanAdd(MediaType mediaType)
        {
            return GetCount(mediaType) < GetMaxCount(mediaType);
        }


        /// <summary>
        /// Determines whether the specified file can be added as a reference.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        /// <param name="filename">The filename.</param>
        private bool CanAdd(MediaType mediaType, string filename)
        {
            if (!CanAdd(mediaType))
                return false;

            return !ReferenceCollection.Any(x => x.MediaType == mediaType && filename.Equals(x.Filename, StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// Gets the reference count for the specified type.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        private int GetCount(MediaType mediaType)
        {
            return ReferenceCollection.Count(x => x.MediaType == mediaType);
        }


        /// <summary>
        /// Gets the model maximum count for the specified type.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        private int GetMaxCount(MediaType mediaType)
        {
            return mediaType switch
            {
                MediaType.Image => ImageMaxCount,
                MediaType.Video => VideoMaxCount,
                MediaType.Audio => AudioMaxCount,
                _ => 0
            };
        }


        /// <summary>
        /// Gets the usable references of the specified type, in the order they were added.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        private IEnumerable<ReferenceItemModel> GetReferences(MediaType mediaType)
        {
            return ReferenceCollection
                .Where(x => x.MediaType == mediaType && !x.IsIgnored)
                .OrderBy(x => x.Index);
        }


        /// <summary>
        /// Updates the reference indexes and ignored state.
        /// References beyond the model maximum (e.g. after a model change) are kept but ignored.
        /// </summary>
        private void UpdateReferenceItems()
        {
            foreach (var group in ReferenceCollection.GroupBy(x => x.MediaType))
            {
                var maxCount = GetMaxCount(group.Key);
                foreach (var (i, item) in group.OrderBy(x => x.Id).Index())
                {
                    item.Index = i + 1;
                    item.IsIgnored = i >= maxCount;
                }
            }
            NotifyPropertyChanged(nameof(ImageCount));
            NotifyPropertyChanged(nameof(VideoCount));
            NotifyPropertyChanged(nameof(AudioCount));
        }


        /// <summary>
        /// Raises the <see cref="E:Drop" /> event.
        /// </summary>
        /// <param name="e">The <see cref="DragEventArgs"/> instance containing the event data.</param>
        protected override async void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                try
                {
                    IsControlBusy = true;
                    var filenames = (string[])e.Data.GetData(DataFormats.FileDrop) ?? [];
                    foreach (var filename in filenames.Where(File.Exists))
                    {
                        var extension = Path.GetExtension(filename);
                        if (TensorStack.WPF.Common.ImageFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                        {
                            await AddImageAsync(filename);
                        }
                        else if (TensorStack.WPF.Common.VideoFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                        {
                            await AddVideoAsync(filename);
                        }
                        else if (TensorStack.WPF.Common.AudioFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                        {
                            await AddAudioAsync(filename);
                        }
                    }
                    CommandManager.InvalidateRequerySuggested();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Exception] [MediaReferenceControl] OnDrop: {ex.Message}");
                }
                finally
                {
                    IsControlBusy = false;
                }
            }
        }
    }


    public sealed class ReferenceItemModel : BaseModel
    {
        private int _index;
        private bool _isIgnored;
        private string _toolTipMessage;

        public int Id { get; set; }
        public MediaType MediaType { get; set; }
        public string Filename { get; set; }
        public ImageInput Image { get; set; }
        public AudioInputStream Audio { get; set; }
        public VideoInputStream Video { get; set; }

        public int Index
        {
            get { return _index; }
            set { SetProperty(ref _index, value); }
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
                return $"Models Maximum {MediaType} Inputs Reached";
            }
            return Filename;
        }
    }
}
