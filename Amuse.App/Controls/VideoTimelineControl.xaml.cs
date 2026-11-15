using Amuse.App.Resources;
using Amuse.App.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TensorStack.Common;
using TensorStack.Common.Audio;
using TensorStack.Common.Tensor;
using TensorStack.Common.Video;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Image;
using TensorStack.WPF.Services;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Interaction logic for VideoTimelineControl.xaml
    /// </summary>
    public partial class VideoTimelineControl : BaseControl
    {
        private readonly DispatcherTimer _progressTimer;
        private CancellationTokenSource _cancellationTokenSource;
        private TimelineFrameModel _previewFrame;
        private TimelineFrameModel _previewOverlay;
        private int _timelineLength;
        private TimeSpan _timelineLengthTime;
        private int? _timelineWidth;
        private int? _timelineHeight;
        private float? _timelineFrameRate;
        private double _timelineInterval;
        private int _timelinePosition;
        private int _timelinePositions;
        private TimeSpan _timelinePositionTime;
        private int? _timelineThumbWidth;
        private int _timelineThumbHeight = 100;
        private double _timelineFrameWidth = 80;
        private int _previewFrameSize = 80;
        private int? _selectionRangeStart;
        private TimelineSegment _selectedSegement;
        private TimelineFrameModel _selectedFrame;
        private bool _isAudioEnabled = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoTimelineControl"/> class.
        /// </summary>
        public VideoTimelineControl()
        {
            _progressTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, UpdateProgress, Dispatcher);
            _progressTimer.Stop();
            TimelineSegements = [];
            TimelineMarkers = [];
            Progress = new ProgressInfo();
            CancelCommand = new AsyncRelayCommand(CancelAsync, CanCancel);
            SaveVideoCommand = new AsyncRelayCommand(SaveVideoAsync, CanSaveVideo);
            LoadVideoCommand = new AsyncRelayCommand(LoadVideoAsync, CanLoadVideo);
            LoadImageCommand = new AsyncRelayCommand(LoadImageAsync, CanLoadImage);
            LoadOverlayCommand = new AsyncRelayCommand(LoadOverlayAsync, CanLoadImage);
            LoadAudioCommand = new AsyncRelayCommand(LoadAudioAsync, CanLoadImage);
            TimelinePlayCommand = new AsyncRelayCommand(TimelinePlayAsync, CanTimelineEditVideo);
            TimelineStopCommand = new AsyncRelayCommand(TimelineStopAsync, CanTimelineEditVideo);
            TimelinePauseCommand = new AsyncRelayCommand(TimelinePauseAsync, CanTimelineEditVideo);
            TimelineNextCommand = new AsyncRelayCommand(TimelineNextAsync, CanTimelineEditVideo);
            TimelinePrevCommand = new AsyncRelayCommand(TimelinePrevAsync, CanTimelineEditVideo);
            TimelineStartCommand = new AsyncRelayCommand(TimelineStartAsync, CanTimelineEditVideo);
            TimelineEndCommand = new AsyncRelayCommand(TimelineEndAsync, CanTimelineEditVideo);
            TimelineClearCommand = new AsyncRelayCommand(TimelineClearAsync, CanTimelineEditVideo);
            TimelineTrimCommand = new AsyncRelayCommand(TimelineTrimAsync, CanTimelineEditVideo);
            TimelineZoomInCommand = new AsyncRelayCommand(TimelineZoomInAsync, CanTimelineEditVideo);
            TimelineZoomOutCommand = new AsyncRelayCommand(TimelineZoomOutAsync, CanTimelineEditVideo);
            TimelineExtendCommand = new AsyncRelayCommand<int>(TimelineExtendAsync, CanTimelineExtendVideo);
            SegmentSplitCommand = new AsyncRelayCommand(SegmentSplitAsync, CanSegmentSplit);
            SegmentMoveUpCommand = new AsyncRelayCommand(SegmentMoveUpAsync, CanSegmentMove);
            SegmentMoveDownCommand = new AsyncRelayCommand(SegmentMoveDownAsync, CanSegmentMove);
            SegmentCopyCommand = new AsyncRelayCommand(SegmentCopyAsync, CanSegmentCopy);
            SegmentRemoveCommand = new AsyncRelayCommand(SegmentRemoveAsync, CanSegmentRemove);
            SegmentRemoveFrameCommand = new AsyncRelayCommand(SegmentRemoveFrameAsync, CanSegmentRemoveSelected);
            SegmentSaveFrameCommand = new AsyncRelayCommand(SegmentSaveFrameAsync, CanSegmentRemoveSelected);
            InitializeComponent();
        }

        public static readonly DependencyProperty IsControlBusyProperty = DependencyProperty.Register(nameof(IsControlBusy), typeof(bool), typeof(VideoTimelineControl));
        public static readonly DependencyProperty MediaServiceProperty = DependencyProperty.Register(nameof(MediaService), typeof(IMediaService), typeof(VideoTimelineControl));
        public event EventHandler<VideoInputStream> OnVideoCreated;
        public event EventHandler<ImageInput> OnImageCreated;
        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand LoadVideoCommand { get; }
        public AsyncRelayCommand LoadImageCommand { get; }
        public AsyncRelayCommand LoadOverlayCommand { get; }
        public AsyncRelayCommand LoadAudioCommand { get; }
        public AsyncRelayCommand SaveVideoCommand { get; }
        public AsyncRelayCommand TimelinePlayCommand { get; }
        public AsyncRelayCommand TimelineStopCommand { get; }
        public AsyncRelayCommand TimelinePauseCommand { get; }
        public AsyncRelayCommand TimelineNextCommand { get; }
        public AsyncRelayCommand TimelinePrevCommand { get; }
        public AsyncRelayCommand TimelineStartCommand { get; }
        public AsyncRelayCommand TimelineEndCommand { get; }
        public AsyncRelayCommand TimelineClearCommand { get; }
        public AsyncRelayCommand TimelineTrimCommand { get; }
        public AsyncRelayCommand TimelineZoomInCommand { get; }
        public AsyncRelayCommand SegmentSplitCommand { get; }
        public AsyncRelayCommand TimelineZoomOutCommand { get; }
        public AsyncRelayCommand<int> TimelineExtendCommand { get; }
        public AsyncRelayCommand SegmentRemoveFrameCommand { get; }
        public AsyncRelayCommand SegmentSaveFrameCommand { get; }
        public AsyncRelayCommand SegmentMoveUpCommand { get; }
        public AsyncRelayCommand SegmentMoveDownCommand { get; }
        public AsyncRelayCommand SegmentCopyCommand { get; }
        public AsyncRelayCommand SegmentRemoveCommand { get; }
        public ObservableCollection<TimelineSegment> TimelineSegements { get; }
        public ObservableCollection<TimeSpan> TimelineMarkers { get; }
        public ProgressInfo Progress { get; }

        public bool IsControlBusy
        {
            get { return (bool)GetValue(IsControlBusyProperty); }
            set { SetValue(IsControlBusyProperty, value); }
        }

        public IMediaService MediaService
        {
            get { return (IMediaService)GetValue(MediaServiceProperty); }
            set { SetValue(MediaServiceProperty, value); }
        }

        public CancellationTokenSource CancellationTokenSource
        {
            get { return _cancellationTokenSource; }
            set { _cancellationTokenSource = value; CommandManager.InvalidateRequerySuggested(); }
        }

        public double TimelineFrameWidth
        {
            get { return _timelineFrameWidth; }
            set { SetProperty(ref _timelineFrameWidth, value); ScrollTimeline(); }
        }

        public int PreviewFrameSize
        {
            get { return _previewFrameSize; }
            set
            {
                var updateTimeline = _previewFrameSize == (int)_timelineFrameWidth;
                SetProperty(ref _previewFrameSize, value);
                if (updateTimeline)
                    TimelineFrameWidth = value;
            }
        }

        public TimelineFrameModel PreviewFrame
        {
            get { return _previewFrame; }
            set { SetProperty(ref _previewFrame, value); }
        }

        public TimelineFrameModel PreviewOverlay
        {
            get { return _previewOverlay; }
            set { SetProperty(ref _previewOverlay, value); }
        }

        public int TimelineLength
        {
            get { return _timelineLength; }
            set
            {
                SetProperty(ref _timelineLength, value);
                UpdateLengthTime();
            }
        }

        public TimeSpan TimelineLengthTime
        {
            get { return _timelineLengthTime; }
            set { SetProperty(ref _timelineLengthTime, value); }
        }

        public int? TimelineWidth
        {
            get { return _timelineWidth; }
            set { SetProperty(ref _timelineWidth, value); }
        }

        public int? TimelineHeight
        {
            get { return _timelineHeight; }
            set { SetProperty(ref _timelineHeight, value); }
        }

        public float? TimelineFrameRate
        {
            get { return _timelineFrameRate; }
            set { SetProperty(ref _timelineFrameRate, value); UpdateTimelineTnterval(); }
        }

        public double TimelineInterval
        {
            get { return _timelineInterval; }
            set { SetProperty(ref _timelineInterval, value); }
        }

        public int TimelinePosition
        {
            get { return _timelinePosition; }
            set { SetProperty(ref _timelinePosition, value); UpdatePositionTime(); }
        }

        public int TimelinePositions
        {
            get { return _timelinePositions; }
            set { SetProperty(ref _timelinePositions, value); }
        }

        public TimeSpan TimelinePositionTime
        {
            get { return _timelinePositionTime; }
            set { SetProperty(ref _timelinePositionTime, value); }
        }

        public int? TimelineThumbWidth
        {
            get { return _timelineThumbWidth; }
            set { SetProperty(ref _timelineThumbWidth, value); }
        }

        public int TimelineThumbHeight
        {
            get { return _timelineThumbHeight; }
            set { SetProperty(ref _timelineThumbHeight, value); TimelineThumbWidth = null; }
        }

        public TimelineSegment SelectedSegement
        {
            get { return _selectedSegement; }
            set { SetProperty(ref _selectedSegement, value); }
        }

        public TimelineFrameModel SelectedFrame
        {
            get { return _selectedFrame; }
            set { SetProperty(ref _selectedFrame, value); }
        }

        public bool IsAudioEnabled
        {
            get { return _isAudioEnabled; }
            set { SetProperty(ref _isAudioEnabled, value); }
        }

        private bool CanCancel() => _cancellationTokenSource != null;
        private bool CanLoadVideo() => !IsControlBusy;
        private bool CanLoadImage() => !IsControlBusy && TimelineFrameRate.HasValue;
        private bool CanSaveVideo() => !IsControlBusy && TimelineFrameRate.HasValue;
        private bool CanTimelineEditVideo() => !IsControlBusy && TimelineFrameRate.HasValue;
        private bool CanTimelineExtendVideo(int frames) => frames > 0 && CanTimelineEditVideo();
        private bool CanSegmentSplit() => _selectedFrame != null && _selectedFrame.IsSelected && !_selectedFrame.IsPadding;
        private bool CanSegmentRemoveSelected() => _selectedFrame != null && !_selectedFrame.IsPadding;
        private bool CanSegmentMove() => TimelineSegements.Count > 1 && _selectedSegement != null;
        private bool CanSegmentCopy() => TimelineSegements.Count > 0 && _selectedSegement != null;
        private bool CanSegmentRemove() => TimelineSegements.Count > 1 && _selectedSegement != null;


        private async Task CancelAsync()
        {
            await _cancellationTokenSource.SafeCancelAsync();
        }


        private async Task SaveVideoAsync()
        {
            try
            {
                IsControlBusy = true;
                using (CancellationTokenSource = new CancellationTokenSource())
                {
                    var tempFilename = MediaService.GetTempFile(MediaType.Video);
                    await VideoManager.WriteVideoStreamAsync(tempFilename, GetOutputVideoFrames(_cancellationTokenSource.Token), cancellationToken: _cancellationTokenSource.Token);

                    var resultStream = await VideoInputStream.CreateAsync(tempFilename);
                    if (_isAudioEnabled)
                    {
                        var audioTimeline = await CreateAudioTimeline();
                        resultStream = await MediaService.SaveWithAudioAsync(resultStream, audioTimeline);
                    }

                    OnVideoCreated?.Invoke(this, resultStream);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                IsControlBusy = false;
                Progress.Clear();
                CancellationTokenSource = null;
            }
        }


        private async Task TimelineClearAsync()
        {
            if (!await DialogService.ShowMessageAsync("Clear Timeline", "Are you sure you want to clear the timeline?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Question, TensorStack.WPF.Dialogs.MessageBoxStyleType.Info))
                return;

            await TimelineStopAsync();
            PreviewFrame = null;
            PreviewOverlay = null;
            TimelineSegements.Clear();
            TimelineLength = 0;
            TimelineInterval = 1000;
            TimelineWidth = null;
            TimelineHeight = null;
            TimelineFrameRate = null;
            TimelinePositions = 0;
            TimelineLengthTime = TimeSpan.Zero;
            TimelinePositionTime = TimeSpan.Zero;
            TimelineThumbWidth = null;
            SelectedSegement = null;
            SelectedFrame = null;
        }


        private Task TimelinePlayAsync()
        {
            _progressTimer.Start();
            return Task.CompletedTask;
        }


        private Task TimelinePauseAsync()
        {
            if (_progressTimer.IsEnabled)
            {
                _progressTimer.Stop();
            }
            else
            {
                _progressTimer.Start();
            }
            return Task.CompletedTask;
        }


        private Task TimelineStopAsync()
        {
            _progressTimer.Stop();
            TimelinePosition = 0;
            return Task.CompletedTask;
        }


        private Task TimelineNextAsync()
        {
            TimelinePosition = Math.Min(_timelinePositions, TimelinePosition + 1);
            return Task.CompletedTask;
        }


        private Task TimelinePrevAsync()
        {
            TimelinePosition = Math.Max(0, TimelinePosition - 1);
            return Task.CompletedTask;
        }


        private Task TimelineStartAsync()
        {
            TimelinePosition = 0;
            return Task.CompletedTask;
        }


        private Task TimelineEndAsync()
        {
            TimelinePosition = _timelinePositions;
            return Task.CompletedTask;
        }


        private Task TimelineTrimAsync()
        {
            var count = _timelineLength;
            for (int i = 0; i < count; i++)
            {
                var isEndPadding = TimelineSegements.All(x => x.Frames.Last().IsPadding);
                var isStartPadding = TimelineSegements.All(x => x.Frames.First().IsPadding);
                if (!isEndPadding && !isStartPadding)
                    break;

                var increment = isEndPadding && isStartPadding ? 2 : 1;
                foreach (var timeline in TimelineSegements)
                {
                    if (isStartPadding)
                        timeline.Frames.RemoveAt(0);
                    if (isEndPadding)
                        timeline.Frames.RemoveAt(timeline.Frames.Count - 1);
                }

                TimelinePositions -= increment;
                TimelineLength -= increment;
            }

            return Task.CompletedTask;
        }


        private Task TimelineExtendAsync(int frames)
        {
            TimelinePositions += frames;
            TimelineLength += frames;
            SetTimlineEndPadding();
            return Task.CompletedTask;
        }


        private Task TimelineZoomOutAsync()
        {
            TimelineFrameWidth = Math.Max(1.0, (TimelineViewer.ViewportWidth - 2.0) / _timelineLength);
            return Task.CompletedTask;
        }


        private Task TimelineZoomInAsync()
        {
            TimelineFrameWidth = _previewFrameSize;
            return Task.CompletedTask;
        }


        private Task SegmentSplitAsync()
        {
            if (_selectedSegement == null)
                return Task.CompletedTask;

            var selection = _selectedSegement.Frames.FirstOrDefault(x => x.IsSelected && !x.IsPadding);
            if (selection == null)
                return Task.CompletedTask;

            var splitTimeline = new TimelineSegment(_selectedSegement);
            TimelineSegements.Add(splitTimeline);
            var startIndex = _selectedSegement.Frames.IndexOf(selection);
            splitTimeline.SetStartPadding(startIndex);
            for (int i = startIndex; i < _timelineLength; i++)
            {
                if (i > _selectedSegement.Length)
                    break;

                var frame = _selectedSegement.Frames[i];
                if (frame.IsPadding)
                    continue;

                frame.IsSelected = true;

                if (splitTimeline.SegmentType == TimelineSegmentType.Audio)
                {
                    splitTimeline.AddFrame(frame.FrameIndex);
                    continue;
                }

                splitTimeline.AddFrame(frame.Frame);
            }

            _selectedSegement.RemoveSelected();
            return Task.CompletedTask;
        }


        private Task SegmentMoveUpAsync()
        {
            if (_selectedSegement == null)
                return Task.CompletedTask;

            var index = TimelineSegements.IndexOf(_selectedSegement);
            if (index < 1)
                return Task.CompletedTask;

            TimelineSegements.Move(index, Math.Max(index - 1, 0));
            return Task.CompletedTask;
        }


        private Task SegmentMoveDownAsync()
        {
            if (_selectedSegement == null)
                return Task.CompletedTask;

            var index = TimelineSegements.IndexOf(_selectedSegement);
            if (index >= TimelineSegements.Count)
                return Task.CompletedTask;

            TimelineSegements.Move(index, Math.Min(index + 1, TimelineSegements.Count - 1));
            return Task.CompletedTask;
        }


        private Task SegmentCopyAsync()
        {
            if (_selectedSegement == null)
                return Task.CompletedTask;

            var segment = new TimelineSegment(_selectedSegement);
            TimelineSegements.Add(segment);
            segment.SetStartPadding(_timelineLength);
            segment.FrameLength = _selectedSegement.FrameLength;
            foreach (var frame in _selectedSegement.Frames)
            {
                if (frame.IsPadding)
                    continue;

                segment.Frames.Add(new TimelineFrameModel(frame));
            }

            TimelineLength += segment.FrameLength;
            SetTimlineEndPadding();
            TimelinePosition = _timelineLength - segment.FrameLength;
            ClearHighlight(true);
            return Task.CompletedTask;
        }


        private Task SegmentRemoveAsync()
        {
            if (_selectedSegement == null)
                return Task.CompletedTask;

            TimelineSegements.Remove(_selectedSegement);
            SelectedSegement = null;
            SelectedFrame = null;
            return Task.CompletedTask;
        }


        private Task SegmentRemoveFrameAsync()
        {
            if (_selectedSegement == null)
                return Task.CompletedTask;

            _selectedSegement.RemoveSelected();
            return Task.CompletedTask;
        }


        private async Task SegmentSaveFrameAsync()
        {
            if (_selectedSegement == null)
                return;
            if (_selectedFrame?.Frame == null)
                return;

            var originalFrame = await _selectedSegement.GetOutputFrameAsync(_selectedFrame.FrameIndex, _timelineWidth.Value, _timelineHeight.Value, _timelineFrameRate.Value);
            if (originalFrame == null)
                return;

            var imageInput = await originalFrame.Frame.ToImageInputAsync();
            OnImageCreated?.Invoke(this, imageInput);
        }


        private async Task LoadVideoAsync()
        {
            var sourceFilename = await DialogService.OpenFileAsync("Open Video", filter: "Videos|*.mp4;*.gif;|All Files|*.*;");
            if (string.IsNullOrEmpty(sourceFilename))
                return;

            var videoInput = await VideoInputStream.CreateAsync(sourceFilename);
            await CreateVideoTimelineAsync(videoInput);
        }


        private async Task LoadImageAsync()
        {
            var sourceFilename = await DialogService.OpenFileAsync("Open Image", filter: "Image Files|*.bmp;*.jpg;*.jpeg;*.png;*.gif;*.tif;*.tiff|All Files|*.*");
            if (string.IsNullOrEmpty(sourceFilename))
                return;

            var imageInput = await ImageInput.CreateAsync(sourceFilename);
            await CreateImageTimelineAsync(imageInput);
        }


        private async Task LoadOverlayAsync()
        {
            var sourceFilename = await DialogService.OpenFileAsync("Open Image", filter: "Image Files|*.bmp;*.jpg;*.jpeg;*.png;*.gif;*.tif;*.tiff|All Files|*.*");
            if (string.IsNullOrEmpty(sourceFilename))
                return;

            var imageInput = await ImageInput.CreateAsync(sourceFilename);
            await CreateOverlayTimelineAsync(imageInput);
        }


        private async Task LoadAudioAsync()
        {
            var sourceFilename = await DialogService.OpenFileAsync("Load Audio", "Audio", filter: "Audio/Video files (*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.mp4;*.mov;*.mkv;*.webm)|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.mp4;*.mov;*.mkv;*.webm", defualtExt: "wav");
            if (string.IsNullOrEmpty(sourceFilename))
                return;

            var imageInput = await AudioInputStream.CreateAsync(sourceFilename);
            await CreateAudioTimelineAsync(imageInput);
        }


        private async IAsyncEnumerable<VideoFrame> GetOutputVideoFrames([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var progress = 0;
            var progressMax = TimelineSegements.Sum(x => x.FrameLength);
            void progressAction(int val) => Progress.Update(progress++, progressMax);
            var segementFrames = new TimelineFrameModel[TimelineSegements.Count][];
            foreach (var (s, segment) in TimelineSegements.Index())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var results = new TimelineFrameModel[segment.Frames.Count];
                var videoFrames = await segment.GetOutputFramesAsync(_timelineWidth.Value, _timelineHeight.Value, _timelineFrameRate.Value, progressAction, cancellationToken);
                foreach (var (f, videoFrame) in segment.Frames.Index())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (videoFrame.IsPadding || videoFrame.Frame == null)
                        continue;

                    results[f] = new TimelineFrameModel(segment.SegmentType, videoFrames[videoFrame.FrameIndex]);
                    if (segment.SegmentType == TimelineSegmentType.Overlay)
                        results[f].IsSelected = true;
                }
                segementFrames[s] = results;
            }

            var currentFrame = default(VideoFrame);
            for (int i = 0; i < _timelineLength; i++)
            {
                foreach (var segementFrame in segementFrames)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var frame = segementFrame[i];
                    if (frame == null)
                        continue;

                    if (frame.IsSelected)
                    {
                        if (currentFrame != null)
                        {
                            currentFrame.Frame.OverlayImage(frame.Frame.Frame);
                            continue;
                        }
                    }
                    currentFrame = frame.Frame;
                }

                if (currentFrame != null)
                    yield return currentFrame;

                Progress.Update(i + 1, _timelineLength);
            }
        }


        private async Task CreateVideoTimelineAsync(VideoInputStream videoStream)
        {
            IsControlBusy = true;
            var audioInfo = await AudioManager.LoadInfoAsync(videoStream.SourceFile);
            var timelineSegment = new TimelineSegment(videoStream, audioInfo != null);
            try
            {
                using (CancellationTokenSource = new CancellationTokenSource())
                {
                    TimelineWidth ??= videoStream.Width;
                    TimelineHeight ??= videoStream.Height;
                    TimelineFrameRate ??= videoStream.FrameRate;

                    TimelineSegements.Add(timelineSegment);
                    timelineSegment.SetStartPadding(_timelineLength);
                    await Dispatcher.Yield();

                    var frameIndex = 0;
                    await foreach (var videoFrame in videoStream.GetAsync(widthOverride: _timelineThumbWidth, heightOverride: _timelineThumbHeight, frameRateOverride: _timelineFrameRate, resizeMode: TensorStack.Common.ResizeMode.Crop, _cancellationTokenSource.Token))
                    {
                        TimelineThumbWidth ??= videoFrame.Width;

                        timelineSegment.AddFrame(videoFrame);
                        Progress.Update(frameIndex++, videoStream.FrameCount);
                        if (frameIndex % 2 == 0)
                            await Dispatcher.Yield();
                    }
                }

                TimelineLength += timelineSegment.FrameLength;
                SetTimlineEndPadding();
                TimelinePosition = _timelineLength - timelineSegment.FrameLength;
                if (TimelineSegements.Count == 1)
                    SetCurrentFrame(0);
            }
            catch (OperationCanceledException)
            {
                TimelineSegements.Remove(timelineSegment);
                if (TimelineSegements.Count == 0)
                    await TimelineClearAsync();
            }
            finally
            {
                IsControlBusy = false;
                Progress.Clear();
                CancellationTokenSource = null;
            }
        }


        private Task CreateImageTimelineAsync(ImageInput imageFrame)
        {
            try
            {
                IsControlBusy = true;
                var previewImage = imageFrame.ResizeImage(_timelineThumbWidth.Value, _timelineThumbHeight, TensorStack.Common.ResizeMode.Crop);
                var previewFrame = new VideoFrame(0, previewImage, _timelineFrameRate.Value);
                var timelineSegment = new TimelineSegment(TimelineSegmentType.Image, imageFrame);
                timelineSegment.AddFrame(previewFrame);
                TimelineSegements.Add(timelineSegment);
                timelineSegment.SetStartPadding(_timelineLength);

                TimelineLength++;
                SetTimlineEndPadding();
                TimelinePosition = _timelineLength - 1;
                return Task.CompletedTask;
            }
            finally
            {
                IsControlBusy = false;
                Progress.Clear();
            }
        }


        private async Task CreateOverlayTimelineAsync(ImageInput imageFrame)
        {
            IsControlBusy = true;
            var timelineSegment = new TimelineSegment(TimelineSegmentType.Overlay, imageFrame);

            try
            {
                using (CancellationTokenSource = new CancellationTokenSource())
                {
                    var frameIndex = 0;
                    var previewImage = imageFrame.ResizeImage(_timelineThumbWidth.Value, _timelineThumbHeight, TensorStack.Common.ResizeMode.Crop);
                    var previewFrame = new VideoFrame(0, previewImage, _timelineFrameRate.Value);
                    TimelineSegements.Add(timelineSegment);
                    for (int i = 0; i < _timelineLength; i++)
                    {
                        CancellationTokenSource.Token.ThrowIfCancellationRequested();

                        timelineSegment.AddFrame(previewFrame);
                        Progress.Update(frameIndex++, _timelineLength);
                        if (frameIndex % 2 == 0)
                            await Dispatcher.Yield();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                TimelineSegements.Remove(timelineSegment);
            }
            finally
            {
                IsControlBusy = false;
                Progress.Clear();
            }
        }


        private async Task CreateAudioTimelineAsync(AudioInputStream audioInput)
        {
            IsControlBusy = true;
            var timelineSegment = new TimelineSegment(audioInput);
            try
            {
                using (CancellationTokenSource = new CancellationTokenSource())
                {
                    TimelineSegements.Add(timelineSegment);

                    var frameIndex = 0;
                    var durationSeconds = (double)audioInput.Samples / audioInput.SampleRate;
                    var frameCount = (int)Math.Round(durationSeconds * _timelineFrameRate.Value);
                    for (int i = 0; i < frameCount; i++)
                    {
                        timelineSegment.AddFrame(i);
                        Progress.Update(frameIndex++, frameCount);
                        if (frameIndex % 10 == 0)
                            await Dispatcher.Yield();
                    }
                }

                TimelineLength = Math.Max(_timelineLength, timelineSegment.FrameLength);
                SetTimlineEndPadding();
                TimelinePosition = 0;
            }
            catch (OperationCanceledException)
            {
                TimelineSegements.Remove(timelineSegment);
            }
            finally
            {
                IsControlBusy = false;
                Progress.Clear();
                CancellationTokenSource = null;
            }
        }


        private void SetTimlineEndPadding()
        {
            foreach (var timeline in TimelineSegements)
            {
                timeline.SetEndPadding(_timelineLength);
            }
        }


        private async Task SetSegmentPositionAsync(TimelineSegment timelineSegment, int index)
        {
            var length = index + timelineSegment.FrameLength;
            if (length > _timelineLength)
                await TimelineExtendAsync(length - _timelineLength);

            timelineSegment.RemovePadding();
            timelineSegment.SetStartPadding(index);
            timelineSegment.SetEndPadding(_timelineLength);
        }


        private void SetCurrentFrame(int index)
        {
            ClearHighlight(false);
            foreach (var timeline in TimelineSegements)
            {
                var safeIndex = Math.Min(index, timeline.Frames.Count - 1);
                var timelineFrame = timeline.Frames[safeIndex];
                timelineFrame.IsHighlight = true;

                if (timelineFrame.IsPadding)
                    continue;

                if (timeline.SegmentType == TimelineSegmentType.Overlay)
                {
                    PreviewOverlay = timelineFrame;
                    continue;
                }

                PreviewOverlay = default;
                PreviewFrame = timelineFrame;
            }
        }


        private Task<AudioTimeline> CreateAudioTimeline()
        {
            var audioTimeline = new AudioTimeline(_timelineLengthTime) { Overlap = TimeSpan.FromMilliseconds(_timelineInterval) };
            var segments = TimelineSegements.Where(x => x.IsAudioPresent && x.IsAudioEnabled).ToArray();
            foreach (var segement in segments)
            {
                var lastFrame = segement.Frames.LastOrDefault(x => !x.IsPadding);
                var startFrame = segement.Frames.FirstOrDefault(x => !x.IsPadding);
                var startPosition = segement.Frames.IndexOf(startFrame);

                var audioStart = TimeSpan.FromMilliseconds(_timelineInterval * startFrame.FrameIndex);
                var audioEnd = TimeSpan.FromMilliseconds(_timelineInterval * (lastFrame.FrameIndex - startFrame.FrameIndex));
                var audioPosition = TimeSpan.FromMilliseconds(_timelineInterval * startPosition);

                var audioSegment = new AudioSegment(segement.SourceFile, audioStart, audioEnd, audioPosition);
                audioSegment.IsFirst = startPosition == 0 || audioTimeline.Segments.Count == 0;
                audioSegment.IsLast = audioTimeline.Segments.Count == segments.Length - 1;
                audioTimeline.Segments.Add(audioSegment);
            }
            return Task.FromResult(audioTimeline);
        }


        private void ScrollTimeline()
        {
            double targetFrameLeft = _timelineFrameWidth * _timelinePosition;
            double targetFrameCenter = targetFrameLeft + (_timelineFrameWidth / 2.0);
            double viewportCenter = (TimelineViewer.ViewportWidth / 2.0) - 2;
            double targetOffset = targetFrameCenter - viewportCenter;
            if (double.IsNormal(targetOffset))
                TimelineViewer.ScrollToHorizontalOffset(Math.Max(0, targetOffset));
        }


        private void UpdateProgress(object sender, EventArgs e)
        {
            if (_timelinePosition == _timelineLength)
            {
                TimelinePosition = 0;
                return;
            }
            TimelinePosition++;
        }


        private void ClearHighlight(bool clearSelection)
        {
            foreach (var timeline in TimelineSegements)
            {
                foreach (var item in timeline.Frames)
                {
                    item.IsHighlight = false;
                    if (clearSelection)
                        item.IsSelected = false;
                }
            }
        }


        private void UpdateLengthTime()
        {
            TimelinePositions = Math.Max(0, _timelineLength - 1);
            TimelineLengthTime = TimeSpan.FromMilliseconds(_timelineInterval * _timelineLength);
            TimelineMarkers.Clear();
            foreach (var time in GenerateTimelineLabels(_timelineLengthTime, 20))
            {
                TimelineMarkers.Add(time);
            }
        }


        private void UpdatePositionTime()
        {
            TimelinePositionTime = TimeSpan.FromMilliseconds(_timelineInterval * _timelinePosition);
        }


        private void UpdateTimelineTnterval()
        {
            if (_timelineFrameRate == null)
            {
                _progressTimer.Stop();
                return;
            }

            TimelineInterval = 1000.0 / (double)_timelineFrameRate;
            _progressTimer.Interval = TimeSpan.FromMilliseconds(_timelineInterval);
        }


        private List<TimeSpan> GenerateTimelineLabels(TimeSpan totalDuration, int numberOfLabels)
        {
            var labels = new List<TimeSpan>();
            long totalTicks = totalDuration.Ticks;
            for (int i = 1; i < numberOfLabels; i++)
            {
                long ticks = (totalTicks * i) / (numberOfLabels - 1);
                labels.Add(TimeSpan.FromTicks(ticks));
            }
            return labels;
        }


        protected void OnTimelineSegementMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                SelectedSegement = listBox.DataContext as TimelineSegment;
                if (_selectedSegement == null)
                    return;

                SelectedFrame = listBox.SelectedItem as TimelineFrameModel;
                if (_selectedFrame == null)
                    return;

                var currentState = _selectedFrame.IsSelected;
                ClearHighlight(true);

                if (Keyboard.IsKeyDown(Key.LeftShift))
                {
                    if (_selectionRangeStart.HasValue)
                    {
                        var selectionEnd = _selectedSegement.Frames.IndexOf(_selectedFrame);
                        var rangeEnd = Math.Max(_selectionRangeStart.Value, selectionEnd);
                        var rangeStart = Math.Min(_selectionRangeStart.Value, selectionEnd);
                        for (var i = rangeStart; i <= rangeEnd; i++)
                        {
                            _selectedSegement.Frames[i].IsSelected = true;
                        }
                        _selectedFrame.IsSelected = true;
                        return;
                    }
                }
                _selectedFrame.IsSelected = !currentState;
                _selectionRangeStart = _selectedFrame.IsSelected ? _selectedSegement.Frames.IndexOf(_selectedFrame) : null;
                PreviewFrame = _selectedFrame;
            }
        }


        protected async void OnTimelineViewerKeyUp(object sender, KeyEventArgs e)
        {
            _selectionRangeStart = null;
            if (e.Key == Key.Delete)
                await SegmentRemoveFrameAsync();
        }


        protected void OnTimelineViewerMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var increment = 4.0;
            if (int.IsPositive(e.Delta))
            {
                TimelineFrameWidth = Math.Min(_previewFrameSize, _timelineFrameWidth + increment);
            }
            else
            {
                var minWidth = Math.Max(1.0, (TimelineViewer.ViewportWidth - 2.0) / _timelineLength);
                TimelineFrameWidth = Math.Max(minWidth, _timelineFrameWidth - increment);
            }
            ScrollTimeline();
        }


        protected void OnTimelineSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetCurrentFrame(_timelinePosition);
            ScrollTimeline();
        }


        private async void OnTimelineSegementMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                var timelineSegment = listBox.DataContext as TimelineSegment;
                if (timelineSegment == null)
                    return;

                var timelineFrame = listBox.SelectedItem as TimelineFrameModel;
                if (timelineFrame == null)
                    return;

                var index = timelineSegment.Frames.IndexOf(timelineFrame);
                await SetSegmentPositionAsync(timelineSegment, index);
            }
        }


        protected void OnTimelineSegementRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }


        protected async void OnTimelineDrop(object sender, DragEventArgs e)
        {
            var fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (!fileNames.IsNullOrEmpty())
            {
                var videoInput = await VideoInputStream.CreateAsync(fileNames[0]);
                await CreateVideoTimelineAsync(videoInput);
                CommandManager.InvalidateRequerySuggested();
            }
        }


        private async void OnImageDrop(object sender, DragEventArgs e)
        {
            var fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (!fileNames.IsNullOrEmpty())
            {
                var imageInput = await ImageInput.CreateAsync(fileNames[0]);
                await CreateImageTimelineAsync(imageInput);
                CommandManager.InvalidateRequerySuggested();
            }
        }


        private async void OnOverlayDrop(object sender, DragEventArgs e)
        {
            var fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (!fileNames.IsNullOrEmpty())
            {
                var imageInput = await ImageInput.CreateAsync(fileNames[0]);
                await CreateOverlayTimelineAsync(imageInput);
                CommandManager.InvalidateRequerySuggested();
            }
        }


        private async void OnAudioDrop(object sender, DragEventArgs e)
        {
            var fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (!fileNames.IsNullOrEmpty())
            {
                var audioInput = await AudioInputStream.CreateAsync(fileNames[0]);
                await CreateAudioTimelineAsync(audioInput);
                CommandManager.InvalidateRequerySuggested();
            }
        }


    }


    public class TimelineSegment
    {
        private readonly ImageTensor _imageSource;
        private readonly VideoInputStream _videoStream;
        private readonly AudioInputStream _audioStream;
        private readonly TimelineSegmentType _segmentType;

        public TimelineSegment(VideoInputStream videoStream, bool isAudioPresent)
        {
            _videoStream = videoStream;
            _segmentType = TimelineSegmentType.Video;
            IsAudioEnabled = isAudioPresent;
            IsAudioPresent = isAudioPresent;
        }

        public TimelineSegment(AudioInputStream audioStream)
        {
            _audioStream = audioStream;
            _segmentType = TimelineSegmentType.Audio;
            IsAudioEnabled = true;
            IsAudioPresent = true;
        }

        public TimelineSegment(TimelineSegmentType segmentType, ImageTensor imageTensor)
        {
            _segmentType = segmentType;
            _imageSource = imageTensor;
        }

        public TimelineSegment(TimelineSegment timelineSegment)
        {
            _imageSource = timelineSegment._imageSource;
            _videoStream = timelineSegment._videoStream;
            _audioStream = timelineSegment._audioStream;
            _segmentType = timelineSegment._segmentType;
            IsAudioEnabled = timelineSegment.IsAudioEnabled;
            IsAudioPresent = timelineSegment.IsAudioPresent;
        }

        public TimelineSegmentType SegmentType => _segmentType;
        public int Length => Frames.Count;
        public string SourceFile => _videoStream?.SourceFile ?? _audioStream?.SourceFile;
        public int FrameLength { get; set; }
        public bool IsAudioEnabled { get; set; }
        public bool IsAudioPresent { get; }
        public ObservableCollection<TimelineFrameModel> Frames { get; } = [];


        public void AddFrame(int frameIndex)
        {
            Frames.Add(new TimelineFrameModel(_segmentType, frameIndex));
            FrameLength++;
        }


        public void AddFrame(VideoFrame videoFrame)
        {
            Frames.Add(new TimelineFrameModel(_segmentType, videoFrame));
            FrameLength++;
        }


        public void RemoveSelected()
        {
            if (_segmentType == TimelineSegmentType.Image)
                return;

            var canRemove = false;
            foreach (var (i, selectedFrame) in Frames.Index())
            {
                if (selectedFrame.IsPadding || !selectedFrame.IsSelected)
                    continue;

                var next = Frames.ElementAtOrDefault(i + 1);
                var previous = Frames.ElementAtOrDefault(i - 1);
                if (previous == null || next == null || previous?.IsPadding == true || next?.IsPadding == true)
                {
                    canRemove = true;
                }
            }

            foreach (var selectedFrame in Frames.Where(x => x.IsSelected))
            {
                if (canRemove)
                {
                    selectedFrame.IsPadding = true;
                    selectedFrame.Frame = null;
                    selectedFrame.IsSelected = false;
                    FrameLength--;
                }
            }
        }


        public void RemovePadding()
        {
            Frames.RemoveAll(x => x.IsPadding);
        }


        public void SetStartPadding(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Frames.Insert(0, new TimelineFrameModel(_segmentType, true));
            }
        }


        public void SetEndPadding(int count)
        {
            for (int i = Frames.Count; i < count; i++)
            {
                Frames.Add(new TimelineFrameModel(_segmentType, true));
            }
        }


        public async Task<List<VideoFrame>> GetOutputFramesAsync(int width, int height, float frameRate, Action<int> progress, CancellationToken cancellationToken)
        {
            var index = 0;
            var output = new List<VideoFrame>();
            if (_segmentType == TimelineSegmentType.Image)
            {
                progress.Invoke(0);
                var image = _imageSource.ResizeImage(width, height, TensorStack.Common.ResizeMode.Crop);
                output = new List<VideoFrame> { new VideoFrame(0, image, frameRate) };
            }
            else if (_segmentType == TimelineSegmentType.Overlay)
            {
                var image = _imageSource.ResizeImage(width, height, TensorStack.Common.ResizeMode.Crop);
                foreach (var frame in Frames)
                {
                    output.Add(new VideoFrame(index++, image, frameRate));
                    progress.Invoke(index);
                }
            }
            else if (_segmentType == TimelineSegmentType.Video)
            {
                await foreach (var item in _videoStream.GetAsync(width, height, frameRate, TensorStack.Common.ResizeMode.Crop, cancellationToken: cancellationToken))
                {
                    output.Add(item);
                    progress.Invoke(index++);
                }
            }
            return output;
        }


        public async Task<VideoFrame> GetOutputFrameAsync(int frameIndex, int width, int height, float frameRate)
        {
            if (_segmentType == TimelineSegmentType.Image)
            {
                var image = _imageSource.ResizeImage(width, height, TensorStack.Common.ResizeMode.Crop);
                return new VideoFrame(frameIndex, image, frameRate);
            }
            else if (_segmentType == TimelineSegmentType.Overlay)
            {
                if (Frames.Any(x => x.Frame?.Index == frameIndex))
                {
                    var image = _imageSource.ResizeImage(width, height, TensorStack.Common.ResizeMode.Crop);
                    return new VideoFrame(frameIndex, image, frameRate);
                }
                return default;
            }
            else if (_segmentType == TimelineSegmentType.Video)
            {
                return await _videoStream.GetFrameAsync(frameIndex, width, height, frameRate, TensorStack.Common.ResizeMode.Crop);
            }
            return default;
        }
    }

    public enum TimelineSegmentType
    {
        [Display(Name = "TimelineSegmentType0", ResourceType = typeof(AppEnums))]
        Video = 0,

        [Display(Name = "TimelineSegmentType1", ResourceType = typeof(AppEnums))]
        Image = 1,

        [Display(Name = "TimelineSegmentType2", ResourceType = typeof(AppEnums))]
        Audio = 2,

        [Display(Name = "TimelineSegmentType3", ResourceType = typeof(AppEnums))]
        Overlay = 3
    }

    public class TimelineFrameModel : BaseModel
    {
        private readonly int _frameIndex;
        private readonly TimelineSegmentType _type;
        private VideoFrame _frame;
        private bool _isPadding;
        private bool _isSelected;
        private bool _isHighlight;


        public TimelineFrameModel(TimelineSegmentType type, bool isPadding)
        {
            _type = type;
            IsPadding = isPadding;
        }

        public TimelineFrameModel(TimelineSegmentType type, VideoFrame frame)
        {
            _type = type;
            Frame = frame;
            _frameIndex = frame.Index;
        }

        public TimelineFrameModel(TimelineSegmentType type, int frameIndex)
        {
            _type = type;
            _frameIndex = frameIndex;
        }

        public TimelineFrameModel(TimelineFrameModel frame)
        {
            _type = frame._type;
            _frameIndex = frame._frameIndex;
            Frame = frame.Frame;
            IsPadding = frame.IsPadding;
        }


        public int FrameIndex => _frameIndex;
        public TimelineSegmentType Type => _type;

        public VideoFrame Frame
        {
            get { return _frame; }
            set { SetProperty(ref _frame, value); }
        }

        public bool IsPadding
        {
            get { return _isPadding; }
            set { SetProperty(ref _isPadding, value); }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value); }
        }

        public bool IsHighlight
        {
            get { return _isHighlight; }
            set { SetProperty(ref _isHighlight, value); }
        }
    }
}