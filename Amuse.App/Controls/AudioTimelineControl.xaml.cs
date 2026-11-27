using Amuse.App.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TensorStack.Common;
using TensorStack.Common.Audio;
using TensorStack.Common.Common;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.WPF;
using TensorStack.WPF.Image;
using TensorStack.WPF.Resources;
using TensorStack.WPF.Services;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for AudioTimelineControl.xaml, an audio track is built from clips placed on a timeline, optionally against a reference video.
    /// </summary>
    public partial class AudioTimelineControl : TimelineControlBase
    {
        private const int WaveformRate = 25;
        private const int ReferenceFrameHeight = 120;
        private const int ReferenceFrameLimit = 1200;
        private const int ReferenceThumbLimit = 300;
        private const int MarkerLimit = 2000;
        private const double ReferenceStripHeight = 48;
        private const double MarkerSpacing = 70;
        private const double TimelinePadding = 200;
        private const double ZoomMaximum = 400;
        private const double ZoomStep = 1.25;
        private const double SnapDistance = 6;
        private const double DragThreshold = 3;
        private static readonly TimeSpan MinimumClipDuration = TimeSpan.FromMilliseconds(50);
        private static readonly double[] MarkerIntervals = [0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600];
        private readonly DispatcherTimer _playbackTimer;
        private readonly List<BitmapSource> _referenceFrames = [];
        private VideoInputStream _referenceVideo;
        private double _referenceFrameRate;
        private double _referenceWidth;
        private BitmapSource _previewFrame;
        private AudioTimelineClip _selectedClip;
        private AudioTimelineClip _dragClip;
        private double _dragStart;
        private double _dragOffset;
        private bool _isDragging;
        private bool _isSeeking;
        private double _pixelsPerSecond = 50;
        private double _timelineWidth;
        private double _playheadLeft;
        private TimeSpan _position;
        private TimeSpan _duration;
        private bool _isPlaying;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioTimelineControl"/> class.
        /// </summary>
        public AudioTimelineControl()
        {
            _playbackTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Normal, OnPlaybackTick, Dispatcher);
            _playbackTimer.Stop();
            Clips = [];
            Markers = [];
            ReferenceThumbs = [];
            LoadAudioCommand = new AsyncRelayCommand(LoadAudioAsync, CanLoad);
            LoadReferenceCommand = new AsyncRelayCommand(LoadReferenceAsync, CanLoad);
            RemoveReferenceCommand = new AsyncRelayCommand(RemoveReferenceAsync, CanRemoveReference);
            SaveAudioCommand = new AsyncRelayCommand(SaveAudioAsync, CanSaveAudio);
            SaveVideoCommand = new AsyncRelayCommand(SaveVideoAsync, CanSaveVideo);
            PlayCommand = new AsyncRelayCommand(PlayAsync, CanPlay);
            PauseCommand = new AsyncRelayCommand(PauseAsync, CanPlay);
            StopCommand = new AsyncRelayCommand(StopAsync, CanPlay);
            StartCommand = new AsyncRelayCommand(StartAsync, CanSeek);
            EndCommand = new AsyncRelayCommand(EndAsync, CanSeek);
            ClearCommand = new AsyncRelayCommand(ClearAsync, CanSeek);
            ZoomInCommand = new AsyncRelayCommand(ZoomInAsync, CanSeek);
            ZoomOutCommand = new AsyncRelayCommand(ZoomOutAsync, CanSeek);
            ClipMoveCommand = new AsyncRelayCommand(ClipMoveAsync, CanClipEdit);
            ClipTrimStartCommand = new AsyncRelayCommand(ClipTrimStartAsync, CanClipCut);
            ClipTrimEndCommand = new AsyncRelayCommand(ClipTrimEndAsync, CanClipCut);
            ClipSplitCommand = new AsyncRelayCommand(ClipSplitAsync, CanClipCut);
            ClipRestoreCommand = new AsyncRelayCommand(ClipRestoreAsync, CanClipRestore);
            ClipCopyCommand = new AsyncRelayCommand(ClipCopyAsync, CanClipEdit);
            ClipRemoveCommand = new AsyncRelayCommand(ClipRemoveAsync, CanClipEdit);
            InitializeComponent();
        }

        public event EventHandler<AudioInputStream> OnAudioCreated;
        public event EventHandler<VideoInputStream> OnVideoCreated;
        public AsyncRelayCommand LoadAudioCommand { get; }
        public AsyncRelayCommand LoadReferenceCommand { get; }
        public AsyncRelayCommand RemoveReferenceCommand { get; }
        public AsyncRelayCommand SaveAudioCommand { get; }
        public AsyncRelayCommand SaveVideoCommand { get; }
        public AsyncRelayCommand PlayCommand { get; }
        public AsyncRelayCommand PauseCommand { get; }
        public AsyncRelayCommand StopCommand { get; }
        public AsyncRelayCommand StartCommand { get; }
        public AsyncRelayCommand EndCommand { get; }
        public AsyncRelayCommand ClearCommand { get; }
        public AsyncRelayCommand ZoomInCommand { get; }
        public AsyncRelayCommand ZoomOutCommand { get; }
        public AsyncRelayCommand ClipMoveCommand { get; }
        public AsyncRelayCommand ClipTrimStartCommand { get; }
        public AsyncRelayCommand ClipTrimEndCommand { get; }
        public AsyncRelayCommand ClipSplitCommand { get; }
        public AsyncRelayCommand ClipRestoreCommand { get; }
        public AsyncRelayCommand ClipCopyCommand { get; }
        public AsyncRelayCommand ClipRemoveCommand { get; }
        public ObservableCollection<AudioTimelineClip> Clips { get; }
        public ObservableCollection<AudioTimelineMarker> Markers { get; }
        public ObservableCollection<BitmapSource> ReferenceThumbs { get; }
        public bool HasReference => _referenceVideo != null;

        public VideoInputStream ReferenceVideo
        {
            get { return _referenceVideo; }
            set { SetProperty(ref _referenceVideo, value); NotifyPropertyChanged(nameof(HasReference)); }
        }

        public double ReferenceWidth
        {
            get { return _referenceWidth; }
            set { SetProperty(ref _referenceWidth, value); }
        }

        public BitmapSource PreviewFrame
        {
            get { return _previewFrame; }
            set { SetProperty(ref _previewFrame, value); }
        }

        public AudioTimelineClip SelectedClip
        {
            get { return _selectedClip; }
            set { SetProperty(ref _selectedClip, value); }
        }

        public double PixelsPerSecond
        {
            get { return _pixelsPerSecond; }
            set { SetProperty(ref _pixelsPerSecond, value); }
        }

        public double TimelineWidth
        {
            get { return _timelineWidth; }
            set { SetProperty(ref _timelineWidth, value); }
        }

        public double PlayheadLeft
        {
            get { return _playheadLeft; }
            set { SetProperty(ref _playheadLeft, value); }
        }

        public TimeSpan Position
        {
            get { return _position; }
            set { SetProperty(ref _position, value); UpdatePlayhead(); }
        }

        public TimeSpan Duration
        {
            get { return _duration; }
            set { SetProperty(ref _duration, value); }
        }

        public bool IsPlaying
        {
            get { return _isPlaying; }
            set { SetProperty(ref _isPlaying, value); }
        }

        private bool CanLoad() => !IsControlBusy;
        private bool CanSeek() => !IsControlBusy && (Clips.Count > 0 || HasReference);
        private bool CanPlay() => !IsControlBusy && Clips.Count > 0;
        private bool CanSaveAudio() => !IsControlBusy && Clips.Count > 0;
        private bool CanSaveVideo() => !IsControlBusy && HasReference && Clips.Count > 0;
        private bool CanRemoveReference() => !IsControlBusy && HasReference;
        private bool CanClipEdit() => !IsControlBusy && _selectedClip != null;
        private bool CanClipRestore() => CanClipEdit() && (_selectedClip.Start > TimeSpan.Zero || _selectedClip.Duration < _selectedClip.SourceDuration);
        private bool CanClipCut() => CanClipEdit() && _position >= _selectedClip.Position + MinimumClipDuration && _position <= _selectedClip.End - MinimumClipDuration;


        /// <summary>
        /// Opens an audio file and adds it to the timeline as a clip.
        /// </summary>
        private async Task LoadAudioAsync()
        {
            var sourceFilename = await DialogService.OpenFileAsync(Default.LoadAudio, "Audio", filter: TensorStack.WPF.Common.AudioFileFilter, defualtExt: "wav");
            if (string.IsNullOrEmpty(sourceFilename))
                return;

            await AddClipAsync(sourceFilename);
        }


        /// <summary>
        /// Opens a video file and sets it as the reference video.
        /// </summary>
        private async Task LoadReferenceAsync()
        {
            var sourceFilename = await DialogService.OpenFileAsync(Default.OpenVideo, filter: TensorStack.WPF.Common.VideoFileFilter);
            if (string.IsNullOrEmpty(sourceFilename))
                return;

            await SetReferenceAsync(sourceFilename);
        }


        /// <summary>
        /// Removes the reference video, the clips are kept.
        /// </summary>
        private Task RemoveReferenceAsync()
        {
            ClearReference();
            UpdateTimeline();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Mixes the timeline to an audio file and raises OnAudioCreated.
        /// </summary>
        private async Task SaveAudioAsync()
        {
            try
            {
                IsControlBusy = true;
                PausePlayback();
                using (CancellationTokenSource = new CancellationTokenSource())
                {
                    var mixFile = default(string);
                    try
                    {
                        Progress.Indeterminate();
                        mixFile = await AudioManager.CreateAudioTimelineAsync(CreateAudioTimeline(_duration), CancellationTokenSource.Token);
                        var tempFilename = MediaService.GetTempFile(MediaType.Audio);
                        var mixStream = await AudioInputStream.CreateAsync(mixFile);
                        await mixStream.SaveAsync(tempFilename, cancellationToken: CancellationTokenSource.Token);
                        OnAudioCreated?.Invoke(this, await AudioInputStream.CreateAsync(tempFilename));
                    }
                    finally
                    {
                        FileHelper.DeleteFile(mixFile);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
            }
            finally
            {
                IsControlBusy = false;
                Progress.Clear();
                CancellationTokenSource = null;
            }
        }


        /// <summary>
        /// Mixes the timeline and saves the reference video with it as its audio track, raises OnVideoCreated.
        /// An mp4 reference is copied without encoding the video again, the audio is padded or trimmed to the video length.
        /// </summary>
        private async Task SaveVideoAsync()
        {
            try
            {
                IsControlBusy = true;
                PausePlayback();
                using (CancellationTokenSource = new CancellationTokenSource())
                {
                    var mixFile = default(string);
                    try
                    {
                        Progress.Indeterminate();
                        mixFile = await AudioManager.CreateAudioTimelineAsync(CreateAudioTimeline(_referenceVideo.Duration), CancellationTokenSource.Token);
                        var tempFilename = MediaService.GetTempFile(MediaType.Video);
                        if (Path.GetExtension(_referenceVideo.SourceFile).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
                        {
                            var videoStream = await _referenceVideo.CopyAsync(tempFilename);
                            OnVideoCreated?.Invoke(this, await videoStream.AddAudioAsync(mixFile, CancellationTokenSource.Token));
                            return;
                        }

                        await _referenceVideo.GetAsync(cancellationToken: CancellationTokenSource.Token).SaveAsync(tempFilename, audioSourceFile: mixFile, cancellationToken: CancellationTokenSource.Token);
                        OnVideoCreated?.Invoke(this, await VideoInputStream.CreateAsync(tempFilename));
                    }
                    finally
                    {
                        FileHelper.DeleteFile(mixFile);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
            }
            finally
            {
                IsControlBusy = false;
                Progress.Clear();
                CancellationTokenSource = null;
            }
        }


        /// <summary>
        /// Plays the timeline from the playhead, the preview mix is created again if the timeline has changed.
        /// </summary>
        private async Task PlayAsync()
        {
            if (!await PreparePreviewAsync(CreateAudioTimeline(_duration)))
                return;

            if (_position >= _duration)
                Position = TimeSpan.Zero;

            PlayPreview(_position);
            _playbackTimer.Start();
            IsPlaying = true;
        }


        /// <summary>
        /// Pauses playback, the playhead stays where it is.
        /// </summary>
        private Task PauseAsync()
        {
            PausePlayback();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Stops playback and returns the playhead to the start.
        /// </summary>
        private Task StopAsync()
        {
            PausePlayback();
            Position = TimeSpan.Zero;
            ScrollToPlayhead();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Moves the playhead to the start of the timeline.
        /// </summary>
        private Task StartAsync()
        {
            SetPosition(TimeSpan.Zero);
            ScrollToPlayhead();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Moves the playhead to the end of the timeline.
        /// </summary>
        private Task EndAsync()
        {
            SetPosition(_duration);
            ScrollToPlayhead();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Clears the clips and the reference video.
        /// </summary>
        private async Task ClearAsync()
        {
            if (!await DialogService.ShowMessageAsync(AppDefault.ClearTimeline, AppDefault.ClearTimelineMessage, TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Question, TensorStack.WPF.Dialogs.MessageBoxStyleType.Info))
                return;

            PausePlayback();
            ClosePreview();
            foreach (var clip in Clips)
            {
                clip.PropertyChanged -= OnClipPropertyChanged;
            }

            Clips.Clear();
            SelectedClip = null;
            ClearReference();
            Position = TimeSpan.Zero;
            UpdateTimeline();
        }


        /// <summary>
        /// Zooms in around the centre of the view.
        /// </summary>
        private Task ZoomInAsync()
        {
            SetZoom(_pixelsPerSecond * 2, TimelineViewer.ViewportWidth / 2);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Zooms out to fit the whole timeline in the view.
        /// </summary>
        private Task ZoomOutAsync()
        {
            SetZoom(GetZoomFit(), 0);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Moves the selected clip to start at the playhead.
        /// </summary>
        private Task ClipMoveAsync()
        {
            _selectedClip.Position = _position;
            UpdateClips();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Trims the selected clip to start at the playhead.
        /// </summary>
        private Task ClipTrimStartAsync()
        {
            var offset = _position - _selectedClip.Position;
            _selectedClip.SetRange(_selectedClip.Start + offset, _selectedClip.Duration - offset);
            _selectedClip.Position = _position;
            UpdateClips();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Trims the selected clip to end at the playhead.
        /// </summary>
        private Task ClipTrimEndAsync()
        {
            _selectedClip.SetRange(_selectedClip.Start, _position - _selectedClip.Position);
            UpdateClips();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Splits the selected clip in two at the playhead.
        /// </summary>
        private Task ClipSplitAsync()
        {
            var offset = _position - _selectedClip.Position;
            var splitClip = new AudioTimelineClip(_selectedClip);
            splitClip.SetRange(_selectedClip.Start + offset, _selectedClip.Duration - offset);
            splitClip.Position = _position;
            _selectedClip.SetRange(_selectedClip.Start, offset);
            InsertClip(Clips.IndexOf(_selectedClip) + 1, splitClip);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Restores the selected clip to the full length of its source, the audio stays where it is on the timeline.
        /// </summary>
        private Task ClipRestoreAsync()
        {
            var position = _selectedClip.Position - _selectedClip.Start;
            _selectedClip.Position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
            _selectedClip.SetRange(TimeSpan.Zero, _selectedClip.SourceDuration);
            UpdateClips();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Copies the selected clip, the copy is placed after it.
        /// </summary>
        private Task ClipCopyAsync()
        {
            var copyClip = new AudioTimelineClip(_selectedClip);
            copyClip.Position = _selectedClip.End;
            InsertClip(Clips.IndexOf(_selectedClip) + 1, copyClip);
            SelectClip(copyClip);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Removes the selected clip.
        /// </summary>
        private Task ClipRemoveAsync()
        {
            _selectedClip.PropertyChanged -= OnClipPropertyChanged;
            Clips.Remove(_selectedClip);
            SelectedClip = null;
            UpdateClips();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Adds an audio file to the timeline as a clip starting at the playhead.
        /// </summary>
        /// <param name="filename">The audio or video filename.</param>
        private async Task AddClipAsync(string filename)
        {
            try
            {
                IsControlBusy = true;
                PausePlayback();
                using (CancellationTokenSource = new CancellationTokenSource())
                {
                    Progress.Indeterminate();
                    var audioStream = await AudioInputStream.CreateAsync(filename);
                    var peaks = await CreatePeaksAsync(filename, WaveformRate, CancellationTokenSource.Token);
                    var clip = new AudioTimelineClip(audioStream, peaks, WaveformRate);
                    clip.Position = _position;
                    InsertClip(Clips.Count, clip);
                    SelectClip(clip);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync(AppErrors.ImportErrorTitle, ex.Message);
            }
            finally
            {
                IsControlBusy = false;
                Progress.Clear();
                CancellationTokenSource = null;
            }
        }


        /// <summary>
        /// Sets the reference video, its frames are read at preview size so the frame under the playhead can be shown.
        /// The preview frame rate is reduced for long videos to limit the frames held in memory.
        /// </summary>
        /// <param name="filename">The video filename.</param>
        private async Task SetReferenceAsync(string filename)
        {
            try
            {
                IsControlBusy = true;
                PausePlayback();
                using (CancellationTokenSource = new CancellationTokenSource())
                {
                    var frames = new List<BitmapSource>();
                    var videoStream = await VideoInputStream.CreateAsync(filename);
                    var seconds = Math.Max(1.0, videoStream.Duration.TotalSeconds);
                    var frameRate = (float)Math.Min(videoStream.FrameRate, ReferenceFrameLimit / seconds);
                    var frameCount = (int)(seconds * frameRate);
                    await foreach (var videoFrame in videoStream.GetAsync(heightOverride: ReferenceFrameHeight, frameRateOverride: frameRate, resizeMode: TensorStack.Common.ResizeMode.Crop, cancellationToken: CancellationTokenSource.Token))
                    {
                        var bitmap = videoFrame.Frame.ToImage();
                        if (bitmap.CanFreeze)
                            bitmap.Freeze();

                        frames.Add(bitmap);
                        Progress.Update(frames.Count, frameCount);
                        if (frames.Count % 2 == 0)
                            await Dispatcher.Yield();
                    }

                    ClearReference();
                    _referenceFrames.AddRange(frames);
                    _referenceFrameRate = frames.Count / seconds;
                    ReferenceVideo = videoStream;
                    UpdateTimeline();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync(AppErrors.ImportErrorTitle, ex.Message);
            }
            finally
            {
                IsControlBusy = false;
                Progress.Clear();
                CancellationTokenSource = null;
            }
        }


        /// <summary>
        /// Clears the reference video and its frames.
        /// </summary>
        private void ClearReference()
        {
            _referenceFrames.Clear();
            _referenceFrameRate = 0;
            ReferenceThumbs.Clear();
            ReferenceVideo = null;
            PreviewFrame = null;
        }


        /// <summary>
        /// Creates the AudioTimeline of the clips that are not muted.
        /// </summary>
        /// <param name="duration">The duration of the mix.</param>
        private AudioTimeline CreateAudioTimeline(TimeSpan duration)
        {
            var audioTimeline = new AudioTimeline(duration);
            foreach (var clip in Clips)
            {
                if (clip.IsMuted || clip.Volume <= 0 || clip.Position >= duration)
                    continue;

                audioTimeline.Segments.Add(new AudioSegment(clip.SourceFile, clip.Start, clip.Duration, clip.Position) { Volume = (float)clip.Volume });
            }
            return audioTimeline;
        }


        /// <summary>
        /// Pauses the player.
        /// </summary>
        private void PausePlayback()
        {
            _playbackTimer.Stop();
            if (_isPlaying)
                PausePreview();

            IsPlaying = false;
        }


        /// <summary>
        /// Moves the playhead, the player follows if it is playing.
        /// </summary>
        /// <param name="position">The timeline position.</param>
        private void SetPosition(TimeSpan position)
        {
            if (position < TimeSpan.Zero)
                position = TimeSpan.Zero;
            if (position > _duration)
                position = _duration;

            Position = position;
            if (_isPlaying)
                SeekPreview(position);
        }


        /// <summary>
        /// Inserts a clip into the timeline.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="clip">The clip.</param>
        private void InsertClip(int index, AudioTimelineClip clip)
        {
            clip.PropertyChanged += OnClipPropertyChanged;
            Clips.Insert(index, clip);
            UpdateClips();
        }


        /// <summary>
        /// Selects a clip.
        /// </summary>
        /// <param name="clip">The clip.</param>
        private void SelectClip(AudioTimelineClip clip)
        {
            foreach (var item in Clips)
            {
                item.IsSelected = item == clip;
            }
            SelectedClip = clip;
        }


        /// <summary>
        /// Updates the timeline after the clips have changed.
        /// </summary>
        private void UpdateClips()
        {
            UpdateTimeline();
            PausePlayback();
            CommandManager.InvalidateRequerySuggested();
        }


        /// <summary>
        /// Updates the timeline length and the layout of the clips, markers, reference frames and playhead.
        /// </summary>
        private void UpdateTimeline()
        {
            var duration = TimeSpan.Zero;
            foreach (var clip in Clips)
            {
                clip.UpdateLayout(_pixelsPerSecond);
                if (clip.End > duration)
                    duration = clip.End;
            }

            if (_referenceVideo != null && _referenceVideo.Duration > duration)
                duration = _referenceVideo.Duration;

            Duration = duration;
            TimelineWidth = Math.Max(TimelineViewer.ViewportWidth, duration.TotalSeconds * _pixelsPerSecond + TimelinePadding);
            ReferenceWidth = _referenceVideo == null ? 0 : _referenceVideo.Duration.TotalSeconds * _pixelsPerSecond;
            if (_position > duration)
                Position = duration;

            UpdatePlayhead();
            UpdateMarkers();
            UpdateReferenceThumbs();
        }


        /// <summary>
        /// Updates the playhead and the reference frame shown for it.
        /// </summary>
        private void UpdatePlayhead()
        {
            PlayheadLeft = _position.TotalSeconds * _pixelsPerSecond;
            if (_referenceVideo == null || _referenceFrames.Count == 0 || _position > _referenceVideo.Duration)
            {
                PreviewFrame = null;
                return;
            }

            var index = (int)(_position.TotalSeconds * _referenceFrameRate);
            PreviewFrame = _referenceFrames[Math.Clamp(index, 0, _referenceFrames.Count - 1)];
        }


        /// <summary>
        /// Updates the time markers, spaced to stay readable at the current zoom.
        /// </summary>
        private void UpdateMarkers()
        {
            Markers.Clear();
            var interval = MarkerIntervals.FirstOrDefault(x => x * _pixelsPerSecond >= MarkerSpacing);
            if (interval <= 0)
                interval = MarkerIntervals[^1];

            var spacing = interval * _pixelsPerSecond;
            var count = Math.Min(MarkerLimit, (int)(_timelineWidth / spacing));
            for (int i = 0; i <= count; i++)
            {
                Markers.Add(new AudioTimelineMarker(TimeSpan.FromSeconds(i * interval), i * spacing));
            }
        }


        /// <summary>
        /// Updates the reference frames shown along the timeline, as many as fit at the current zoom.
        /// </summary>
        private void UpdateReferenceThumbs()
        {
            ReferenceThumbs.Clear();
            if (_referenceFrames.Count == 0)
                return;

            var frame = _referenceFrames[0];
            var thumbWidth = ReferenceStripHeight * frame.PixelWidth / frame.PixelHeight;
            var count = (int)Math.Clamp(Math.Ceiling(_referenceWidth / thumbWidth), 1, ReferenceThumbLimit);
            for (int i = 0; i < count; i++)
            {
                var index = (int)((i + 0.5) / count * _referenceFrames.Count);
                ReferenceThumbs.Add(_referenceFrames[Math.Min(index, _referenceFrames.Count - 1)]);
            }
        }


        /// <summary>
        /// Sets the zoom, the time under the anchor stays where it is in the view.
        /// </summary>
        /// <param name="pixelsPerSecond">The pixels per second.</param>
        /// <param name="anchor">The anchor position in the view.</param>
        private void SetZoom(double pixelsPerSecond, double anchor)
        {
            var anchorSeconds = (TimelineViewer.HorizontalOffset + anchor) / _pixelsPerSecond;
            PixelsPerSecond = Math.Clamp(pixelsPerSecond, 1, ZoomMaximum);
            UpdateTimeline();
            TimelineViewer.UpdateLayout();
            TimelineViewer.ScrollToHorizontalOffset(Math.Max(0, anchorSeconds * _pixelsPerSecond - anchor));
        }


        /// <summary>
        /// Gets the zoom that fits the whole timeline in the view.
        /// </summary>
        private double GetZoomFit()
        {
            var seconds = Math.Max(1.0, _duration.TotalSeconds);
            return Math.Clamp((TimelineViewer.ViewportWidth - TimelinePadding) / seconds, 1, ZoomMaximum);
        }


        /// <summary>
        /// Scrolls the timeline to the playhead if it is out of view.
        /// </summary>
        private void ScrollToPlayhead()
        {
            var offset = TimelineViewer.HorizontalOffset;
            var viewport = TimelineViewer.ViewportWidth;
            if (_playheadLeft < offset || _playheadLeft > offset + viewport)
                TimelineViewer.ScrollToHorizontalOffset(Math.Max(0, _playheadLeft - viewport * 0.1));
        }


        /// <summary>
        /// Moves the playhead to a position on the timeline content.
        /// </summary>
        /// <param name="e">The <see cref="MouseEventArgs"/> instance containing the event data.</param>
        private void SeekTo(MouseEventArgs e)
        {
            SetPosition(TimeSpan.FromSeconds(Math.Max(0, e.GetPosition(TimelineContent).X) / _pixelsPerSecond));
        }


        /// <summary>
        /// Follows the player while it is playing.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void OnPlaybackTick(object sender, EventArgs e)
        {
            if (!_isPlaying || !IsPreviewOpen || _isSeeking)
                return;

            var position = PreviewPosition;
            Position = position > _duration ? _duration : position;
            ScrollToPlayhead();
        }


        /// <summary>
        /// Pauses playback when a clip's volume or mute changes, the next play mixes the preview again.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void OnClipPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AudioTimelineClip.Volume) || e.PropertyName == nameof(AudioTimelineClip.IsMuted))
                PausePlayback();
        }


        /// <summary>
        /// Returns the playhead to the start when the preview has played to its end.
        /// </summary>
        protected override void OnPreviewEnded()
        {
            PausePlayback();
            Position = TimeSpan.Zero;
            ScrollToPlayhead();
        }


        /// <summary>
        /// Stops following the player when the preview could not be played.
        /// </summary>
        protected override void OnPreviewFailed()
        {
            PausePlayback();
        }


        /// <summary>
        /// Pauses playback and closes the preview when the control is unloaded.
        /// </summary>
        protected override void OnUnloaded()
        {
            PausePlayback();
            base.OnUnloaded();
        }


        /// <summary>
        /// Lays the timeline out again when the view is resized.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SizeChangedEventArgs"/> instance containing the event data.</param>
        protected void OnTimelineSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateTimeline();
        }


        /// <summary>
        /// Zooms the timeline around the mouse.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseWheelEventArgs"/> instance containing the event data.</param>
        protected void OnTimelineMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var anchor = e.GetPosition(TimelineViewer).X;
            if (int.IsPositive(e.Delta))
            {
                SetZoom(_pixelsPerSecond * ZoomStep, anchor);
            }
            else
            {
                var minimum = Math.Min(GetZoomFit(), _pixelsPerSecond);
                SetZoom(Math.Max(minimum, _pixelsPerSecond / ZoomStep), anchor);
            }
            e.Handled = true;
        }


        /// <summary>
        /// Removes the selected clip with the delete key.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="KeyEventArgs"/> instance containing the event data.</param>
        protected async void OnTimelineKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && CanClipEdit())
                await ClipRemoveAsync();
        }


        /// <summary>
        /// Starts moving the playhead with the mouse.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        protected void OnTimelineMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!CanSeek())
                return;

            _isSeeking = true;
            TimelineContent.CaptureMouse();
            TimelineViewer.Focus();
            SeekTo(e);
        }


        /// <summary>
        /// Moves the playhead with the mouse.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseEventArgs"/> instance containing the event data.</param>
        protected void OnTimelineMouseMove(object sender, MouseEventArgs e)
        {
            if (_isSeeking && e.LeftButton == MouseButtonState.Pressed)
                SeekTo(e);
        }


        /// <summary>
        /// Stops moving the playhead with the mouse.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        protected void OnTimelineMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSeeking)
                TimelineContent.ReleaseMouseCapture();
        }


        /// <summary>
        /// Ends moving the playhead when the mouse capture is released or lost.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseEventArgs"/> instance containing the event data.</param>
        protected void OnTimelineLostMouseCapture(object sender, MouseEventArgs e)
        {
            _isSeeking = false;
            CommandManager.InvalidateRequerySuggested();
        }


        /// <summary>
        /// Selects a clip and starts moving it with the mouse.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        protected void OnClipMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is AudioTimelineClip clip)
            {
                SelectClip(clip);
                _dragClip = clip;
                _isDragging = false;
                _dragStart = e.GetPosition(TimelineContent).X;
                _dragOffset = _dragStart - clip.Left;
                element.CaptureMouse();
                TimelineViewer.Focus();
                e.Handled = true;
            }
        }


        /// <summary>
        /// Moves a clip along the timeline, its start snaps to the playhead when close.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseEventArgs"/> instance containing the event data.</param>
        protected void OnClipMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragClip == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var position = e.GetPosition(TimelineContent).X;
            if (!_isDragging && Math.Abs(position - _dragStart) < DragThreshold)
                return;

            _isDragging = true;
            var left = Math.Max(0, position - _dragOffset);
            if (Math.Abs(left - _playheadLeft) < SnapDistance)
                left = _playheadLeft;

            _dragClip.Position = TimeSpan.FromSeconds(left / _pixelsPerSecond);
            _dragClip.UpdateLayout(_pixelsPerSecond);
            e.Handled = true;
        }


        /// <summary>
        /// Stops moving a clip with the mouse.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        protected void OnClipMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragClip == null)
                return;

            if (sender is FrameworkElement element)
                element.ReleaseMouseCapture();

            e.Handled = true;
        }


        /// <summary>
        /// Ends moving a clip when the mouse capture is released or lost, the timeline is updated if the clip moved.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MouseEventArgs"/> instance containing the event data.</param>
        protected void OnClipLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_dragClip == null)
                return;

            var isMoved = _isDragging;
            _dragClip = null;
            _isDragging = false;
            if (isMoved)
                UpdateClips();
        }


        /// <summary>
        /// Adds a dropped audio file to the timeline.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DragEventArgs"/> instance containing the event data.</param>
        protected async void OnAudioDrop(object sender, DragEventArgs e)
        {
            var fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (!fileNames.IsNullOrEmpty())
            {
                await AddClipAsync(fileNames[0]);
                CommandManager.InvalidateRequerySuggested();
            }
        }


        /// <summary>
        /// Sets a dropped video file as the reference video.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DragEventArgs"/> instance containing the event data.</param>
        protected async void OnReferenceDrop(object sender, DragEventArgs e)
        {
            var fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (!fileNames.IsNullOrEmpty())
            {
                await SetReferenceAsync(fileNames[0]);
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }


    /// <summary>
    /// A clip on the audio timeline, a range of an audio source placed at a timeline position.
    /// </summary>
    public sealed class AudioTimelineClip : BaseModel
    {
        private readonly AudioInputStream _audioStream;
        private readonly float[] _peaks;
        private readonly int _peakRate;
        private TimeSpan _start;
        private TimeSpan _duration;
        private TimeSpan _position;
        private double _volume = 1;
        private bool _isMuted;
        private bool _isSelected;
        private double _left;
        private double _width;
        private Geometry _waveform;
        private int _waveformLength = 1;
        private double _waveformScale = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioTimelineClip"/> class.
        /// </summary>
        /// <param name="audioStream">The audio source.</param>
        /// <param name="peaks">The waveform peaks of the source.</param>
        /// <param name="peakRate">The peaks per second.</param>
        public AudioTimelineClip(AudioInputStream audioStream, float[] peaks, int peakRate)
        {
            _audioStream = audioStream;
            _peaks = peaks;
            _peakRate = peakRate;
            SetRange(TimeSpan.Zero, SourceDuration);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioTimelineClip"/> class as a copy of a clip.
        /// </summary>
        /// <param name="clip">The clip to copy.</param>
        public AudioTimelineClip(AudioTimelineClip clip)
        {
            _audioStream = clip._audioStream;
            _peaks = clip._peaks;
            _peakRate = clip._peakRate;
            _position = clip._position;
            _volume = clip._volume;
            _isMuted = clip._isMuted;
            SetRange(clip._start, clip._duration);
        }

        public string Name => Path.GetFileName(_audioStream.SourceFile);
        public string SourceFile => _audioStream.SourceFile;
        public TimeSpan SourceDuration => _audioStream.Duration > TimeSpan.Zero ? _audioStream.Duration : TimeSpan.FromSeconds((double)_peaks.Length / _peakRate);
        public TimeSpan End => _position + _duration;

        public TimeSpan Start
        {
            get { return _start; }
            private set { SetProperty(ref _start, value); }
        }

        public TimeSpan Duration
        {
            get { return _duration; }
            private set { SetProperty(ref _duration, value); NotifyPropertyChanged(nameof(End)); }
        }

        public TimeSpan Position
        {
            get { return _position; }
            set { SetProperty(ref _position, value); NotifyPropertyChanged(nameof(End)); }
        }

        public double Volume
        {
            get { return _volume; }
            set { SetProperty(ref _volume, value); }
        }

        public bool IsMuted
        {
            get { return _isMuted; }
            set { SetProperty(ref _isMuted, value); }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value); }
        }

        public double Left
        {
            get { return _left; }
            private set { SetProperty(ref _left, value); }
        }

        public double Width
        {
            get { return _width; }
            private set { SetProperty(ref _width, value); }
        }

        public Geometry Waveform
        {
            get { return _waveform; }
            private set { SetProperty(ref _waveform, value); }
        }

        public double WaveformScale
        {
            get { return _waveformScale; }
            private set { SetProperty(ref _waveformScale, value); }
        }


        /// <summary>
        /// Sets the range of the source the clip plays.
        /// </summary>
        /// <param name="start">The start in the source.</param>
        /// <param name="duration">The duration.</param>
        public void SetRange(TimeSpan start, TimeSpan duration)
        {
            Start = start;
            Duration = duration;
            UpdateWaveform();
        }


        /// <summary>
        /// Updates the position and size of the clip on the timeline.
        /// </summary>
        /// <param name="pixelsPerSecond">The pixels per second.</param>
        public void UpdateLayout(double pixelsPerSecond)
        {
            Left = _position.TotalSeconds * pixelsPerSecond;
            Width = Math.Max(2, _duration.TotalSeconds * pixelsPerSecond);
            WaveformScale = _width / _waveformLength;
        }


        /// <summary>
        /// Creates the waveform of the clip's range, one unit wide per peak and one unit high.
        /// </summary>
        private void UpdateWaveform()
        {
            var first = Math.Clamp((int)(_start.TotalSeconds * _peakRate), 0, _peaks.Length);
            var count = Math.Clamp((int)Math.Ceiling(_duration.TotalSeconds * _peakRate), 0, _peaks.Length - first);
            _waveformLength = Math.Max(1, count - 1);
            WaveformScale = _width / _waveformLength;
            if (count < 2)
            {
                Waveform = Geometry.Empty;
                return;
            }

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(0, 0.5), true, true);
                for (int i = 0; i < count; i++)
                {
                    context.LineTo(new Point(i, 0.5 - Math.Max(0.01, _peaks[first + i] * 0.5)), false, false);
                }
                for (int i = count - 1; i >= 0; i--)
                {
                    context.LineTo(new Point(i, 0.5 + Math.Max(0.01, _peaks[first + i] * 0.5)), false, false);
                }
            }
            geometry.Freeze();
            Waveform = geometry;
        }
    }


    /// <summary>
    /// A time marker on the audio timeline.
    /// </summary>
    /// <param name="Time">The time.</param>
    /// <param name="Left">The position on the timeline.</param>
    public sealed record AudioTimelineMarker(TimeSpan Time, double Left);
}
