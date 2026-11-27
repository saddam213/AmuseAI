using Amuse.App.Resources;
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
using TensorStack.WPF.Dialogs;
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
            CaptureImageCommand = new AsyncRelayCommand(CaptureImageAsync, () => CanAdd(MediaType.Image));
            CaptureVideoCommand = new AsyncRelayCommand(CaptureVideoAsync, () => CanAdd(MediaType.Video));
            CaptureAudioCommand = new AsyncRelayCommand(CaptureAudioAsync, () => CanAdd(MediaType.Audio));
            ClearCommand = new AsyncRelayCommand(ClearAsync, () => ReferenceCollection.Count > 0);
            RemoveCommand = new AsyncRelayCommand<ReferenceItemModel>(RemoveAsync);
            CropCommand = new AsyncRelayCommand<ReferenceItemModel>(CropAsync);
            InitializeComponent();
        }

        public static readonly DependencyProperty IsControlBusyProperty = DependencyProperty.Register(nameof(IsControlBusy), typeof(bool), typeof(MediaReferenceControl), new PropertyMetadata(false));
        public static readonly DependencyProperty ImageMaxCountProperty = DependencyProperty.Register(nameof(ImageMaxCount), typeof(int), typeof(MediaReferenceControl), new PropertyMetadata<MediaReferenceControl>((c) => c.OnMaxCountChanged()));
        public static readonly DependencyProperty VideoMaxCountProperty = DependencyProperty.Register(nameof(VideoMaxCount), typeof(int), typeof(MediaReferenceControl), new PropertyMetadata<MediaReferenceControl>((c) => c.OnMaxCountChanged()));
        public static readonly DependencyProperty AudioMaxCountProperty = DependencyProperty.Register(nameof(AudioMaxCount), typeof(int), typeof(MediaReferenceControl), new PropertyMetadata<MediaReferenceControl>((c) => c.OnMaxCountChanged()));
        public static readonly DependencyProperty CaptureWidthProperty = DependencyProperty.Register(nameof(CaptureWidth), typeof(int), typeof(MediaReferenceControl));
        public static readonly DependencyProperty CaptureHeightProperty = DependencyProperty.Register(nameof(CaptureHeight), typeof(int), typeof(MediaReferenceControl));
        public static readonly DependencyProperty CaptureFrameRateProperty = DependencyProperty.Register(nameof(CaptureFrameRate), typeof(float), typeof(MediaReferenceControl));
        public static readonly DependencyProperty CaptureFrameCountProperty = DependencyProperty.Register(nameof(CaptureFrameCount), typeof(int), typeof(MediaReferenceControl));
        public static readonly DependencyProperty CaptureSampleRateProperty = DependencyProperty.Register(nameof(CaptureSampleRate), typeof(int), typeof(MediaReferenceControl));
        public static readonly DependencyProperty CaptureChannelsProperty = DependencyProperty.Register(nameof(CaptureChannels), typeof(int), typeof(MediaReferenceControl));
        public event EventHandler<MediaImportEventArgs> OnMediaImport;

        public AsyncRelayCommand AddImageCommand { get; }
        public AsyncRelayCommand AddVideoCommand { get; }
        public AsyncRelayCommand AddAudioCommand { get; }
        public AsyncRelayCommand CaptureImageCommand { get; }
        public AsyncRelayCommand CaptureVideoCommand { get; }
        public AsyncRelayCommand CaptureAudioCommand { get; }
        public AsyncRelayCommand ClearCommand { get; }
        public AsyncRelayCommand<ReferenceItemModel> RemoveCommand { get; }
        public AsyncRelayCommand<ReferenceItemModel> CropCommand { get; }
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

        /// <summary>
        /// Gets or sets the width a camera image or video is captured at, zero for the size of the camera.
        /// </summary>
        public int CaptureWidth
        {
            get { return (int)GetValue(CaptureWidthProperty); }
            set { SetValue(CaptureWidthProperty, value); }
        }

        /// <summary>
        /// Gets or sets the height a camera image or video is captured at, zero for the size of the camera.
        /// </summary>
        public int CaptureHeight
        {
            get { return (int)GetValue(CaptureHeightProperty); }
            set { SetValue(CaptureHeightProperty, value); }
        }

        /// <summary>
        /// Gets or sets the frame rate a camera video is captured at, zero for the frame rate of the camera.
        /// </summary>
        public float CaptureFrameRate
        {
            get { return (float)GetValue(CaptureFrameRateProperty); }
            set { SetValue(CaptureFrameRateProperty, value); }
        }

        /// <summary>
        /// Gets or sets the exact number of frames of a captured camera video, zero for any length.
        /// </summary>
        public int CaptureFrameCount
        {
            get { return (int)GetValue(CaptureFrameCountProperty); }
            set { SetValue(CaptureFrameCountProperty, value); }
        }

        /// <summary>
        /// Gets or sets the sample rate audio is captured at, zero for the default.
        /// </summary>
        public int CaptureSampleRate
        {
            get { return (int)GetValue(CaptureSampleRateProperty); }
            set { SetValue(CaptureSampleRateProperty, value); }
        }

        /// <summary>
        /// Gets or sets the channels audio is captured with, zero for the default.
        /// </summary>
        public int CaptureChannels
        {
            get { return (int)GetValue(CaptureChannelsProperty); }
            set { SetValue(CaptureChannelsProperty, value); }
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
            var filename = await DialogService.OpenFileAsync(AppDefault.ImportImage, filter: TensorStack.WPF.Common.ImageFileFilter);
            if (!string.IsNullOrEmpty(filename))
            {
                await AddImageAsync(filename);
            }
        }


        /// <summary>
        /// Adds an image reference.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <param name="isImport">if set to <c>true</c> the file is reported as a media import, a capture is not.</param>
        private async Task AddImageAsync(string filename, bool isImport = true)
        {
            if (!CanAdd(MediaType.Image, filename))
                return;

            var image = await ImageInput.CreateAsync(filename);
            AddReference(new ReferenceItemModel
            {
                Filename = filename,
                MediaType = MediaType.Image,
                Image = image
            });
            if (isImport)
                OnMediaImport?.Invoke(this, new MediaImportEventArgs(filename, image));
        }


        /// <summary>
        /// Captures an image reference from a camera, at the capture size.
        /// </summary>
        private async Task CaptureImageAsync()
        {
            var filename = await DialogService.CaptureFileAsync(MediaType.Image, GetCaptureOptions());
            if (!string.IsNullOrEmpty(filename))
            {
                await AddImageAsync(filename, false);
            }
        }


        /// <summary>
        /// Adds a video reference.
        /// </summary>
        private async Task AddVideoAsync()
        {
            var filename = await DialogService.OpenFileAsync(AppDefault.ImportVideo, filter: TensorStack.WPF.Common.VideoFileFilter);
            if (!string.IsNullOrEmpty(filename))
            {
                await AddVideoAsync(filename);
            }
        }


        /// <summary>
        /// Adds a video reference.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <param name="isImport">if set to <c>true</c> the file is reported as a media import, a capture is not.</param>
        private async Task AddVideoAsync(string filename, bool isImport = true)
        {
            if (!CanAdd(MediaType.Video, filename))
                return;

            var video = await VideoInputStream.CreateAsync(filename);
            AddReference(new ReferenceItemModel
            {
                Filename = filename,
                MediaType = MediaType.Video,
                Video = video
            });
            if (isImport)
                OnMediaImport?.Invoke(this, new MediaImportEventArgs(filename, video));
        }


        /// <summary>
        /// Captures a video reference from a camera, at the capture size, frame rate and frame count.
        /// </summary>
        private async Task CaptureVideoAsync()
        {
            var filename = await DialogService.CaptureFileAsync(MediaType.Video, GetCaptureOptions());
            if (!string.IsNullOrEmpty(filename))
            {
                await AddVideoAsync(filename, false);
            }
        }


        /// <summary>
        /// Adds an audio reference.
        /// </summary>
        private async Task AddAudioAsync()
        {
            var filename = await DialogService.OpenFileAsync(AppDefault.ImportAudio, "Audio", filter: TensorStack.WPF.Common.AudioFileFilter, defualtExt: "wav");
            if (!string.IsNullOrEmpty(filename))
            {
                await AddAudioAsync(filename);
            }
        }


        /// <summary>
        /// Adds an audio reference.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <param name="isImport">if set to <c>true</c> the file is reported as a media import, a capture is not.</param>
        private async Task AddAudioAsync(string filename, bool isImport = true)
        {
            if (!CanAdd(MediaType.Audio, filename))
                return;

            var audio = await AudioInputStream.CreateAsync(filename);
            AddReference(new ReferenceItemModel
            {
                Filename = filename,
                MediaType = MediaType.Audio,
                Audio = audio
            });
            if (isImport)
                OnMediaImport?.Invoke(this, new MediaImportEventArgs(filename, audio));
        }


        /// <summary>
        /// Captures an audio reference from a microphone, at the capture sample rate and channels.
        /// </summary>
        private async Task CaptureAudioAsync()
        {
            var filename = await DialogService.CaptureFileAsync(MediaType.Audio, GetCaptureOptions());
            if (!string.IsNullOrEmpty(filename))
            {
                await AddAudioAsync(filename, false);
            }
        }


        /// <summary>
        /// Gets the capture options, a capture is made in the format the model uses so it needs no conversion.
        /// The recording is in the temporary directory so it is not reported as a media import.
        /// </summary>
        private CaptureOptions GetCaptureOptions()
        {
            var captureOptions = new CaptureOptions
            {
                CropWidth = CaptureWidth,
                CropHeight = CaptureHeight,
                FrameRate = CaptureFrameRate,
                FrameCount = CaptureFrameCount
            };
            if (CaptureSampleRate > 0)
                captureOptions = captureOptions with { SampleRate = CaptureSampleRate };
            if (CaptureChannels > 0)
                captureOptions = captureOptions with { Channels = CaptureChannels };

            return captureOptions;
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
        /// Crops a video or audio reference to a clip in the format the model uses, the section of it is chosen in the crop dialog.
        /// The clip is in the temporary directory and takes the place of the reference.
        /// </summary>
        /// <param name="model">The model.</param>
        private async Task CropAsync(ReferenceItemModel model)
        {
            var reference = default(ReferenceItemModel);
            if (model?.Video != null)
                reference = await CropVideoAsync(model);
            else if (model?.Audio != null)
                reference = await CropAudioAsync(model);

            var index = ReferenceCollection.IndexOf(model);
            if (reference == null || index < 0)
                return;

            // The reference keeps its place
            ReferenceCollection[index] = reference;
            UpdateReferenceItems();
        }


        /// <summary>
        /// Crops a video reference to a clip of the capture size, frame rate and frame count, the section and area of the video are chosen in the crop dialog.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>The reference of the clip, null if the dialog was cancelled.</returns>
        private async Task<ReferenceItemModel> CropVideoAsync(ReferenceItemModel model)
        {
            var cropOptions = new CropVideoOptions
            {
                Width = CaptureWidth,
                Height = CaptureHeight,
                FrameRate = CaptureFrameRate,
                FrameCount = CaptureFrameCount
            };
            var video = await DialogService.CropVideoAsync(model.Video, cropOptions);
            if (video == null)
                return null;

            return new ReferenceItemModel
            {
                Id = model.Id,
                Filename = video.SourceFile,
                MediaType = MediaType.Video,
                Video = video
            };
        }


        /// <summary>
        /// Crops an audio reference to a clip at the capture sample rate and channels, the section of the audio is chosen in the crop dialog.
        /// The clip is no longer than the video the model makes.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>The reference of the clip, null if the dialog was cancelled.</returns>
        private async Task<ReferenceItemModel> CropAudioAsync(ReferenceItemModel model)
        {
            var cropOptions = new CropAudioOptions
            {
                SampleRate = CaptureSampleRate,
                Channels = CaptureChannels,
                MaxDuration = CaptureFrameRate > 0 && CaptureFrameCount > 0 ? TimeSpan.FromSeconds(CaptureFrameCount / CaptureFrameRate) : TimeSpan.Zero
            };
            var audio = await DialogService.CropAudioAsync(model.Audio, cropOptions);
            if (audio == null)
                return null;

            return new ReferenceItemModel
            {
                Id = model.Id,
                Filename = audio.SourceFile,
                MediaType = MediaType.Audio,
                Audio = audio
            };
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
                return string.Format(AppDefault.MaxInputsReached, MediaType.GetLocalizedName());
            }
            return Filename;
        }
    }
}
