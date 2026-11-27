using Amuse.App.Resources;
using Amuse.App.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TensorStack.Common;
using TensorStack.Common.Audio;
using TensorStack.Common.Common;
using TensorStack.Media.Audio;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Controls
{
    /// <summary>
    /// Base for the timeline controls, holds the busy state, media service, progress, cancellation and audio preview they share.
    /// </summary>
    public abstract class TimelineControlBase : BaseControl
    {
        private const int WaveformSampleRate = 8000;
        private readonly MediaPlayer _previewPlayer;
        private CancellationTokenSource _cancellationTokenSource;
        private string _previewFile;
        private string _previewKey;
        private TimeSpan _previewStart;
        private bool _isPreviewOpen;
        private bool _isPreviewOwned;
        private double _previewVolume = 0.5;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimelineControlBase"/> class.
        /// </summary>
        protected TimelineControlBase()
        {
            _previewPlayer = new MediaPlayer();
            _previewPlayer.Volume = _previewVolume;
            _previewPlayer.MediaOpened += OnPreviewMediaOpened;
            _previewPlayer.MediaEnded += OnPreviewMediaEnded;
            _previewPlayer.MediaFailed += OnPreviewMediaFailed;
            Unloaded += OnControlUnloaded;
            Progress = new ProgressInfo();
            CancelCommand = new AsyncRelayCommand(CancelAsync, CanCancel);
        }

        public static readonly DependencyProperty IsControlBusyProperty = DependencyProperty.Register(nameof(IsControlBusy), typeof(bool), typeof(TimelineControlBase));
        public static readonly DependencyProperty MediaServiceProperty = DependencyProperty.Register(nameof(MediaService), typeof(IMediaService), typeof(TimelineControlBase));
        public AsyncRelayCommand CancelCommand { get; }
        public ProgressInfo Progress { get; }
        protected bool IsPreviewOpen => _isPreviewOpen;
        protected TimeSpan PreviewPosition => _previewPlayer.Position;

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

        public double PreviewVolume
        {
            get { return _previewVolume; }
            set { SetProperty(ref _previewVolume, value); _previewPlayer.Volume = value; }
        }


        /// <summary>
        /// Mixes the audio timeline to the preview file the player uses, nothing is done if the timeline has not changed since the last mix.
        /// </summary>
        /// <param name="audioTimeline">The audio timeline.</param>
        /// <returns><c>true</c> if the preview is ready to play; otherwise, <c>false</c>.</returns>
        protected async Task<bool> PreparePreviewAsync(AudioTimeline audioTimeline)
        {
            var previewKey = GetPreviewKey(audioTimeline);
            if (_previewFile != null && _previewKey == previewKey)
                return true;

            try
            {
                IsControlBusy = true;
                using (CancellationTokenSource = new CancellationTokenSource())
                {
                    Progress.Indeterminate();
                    ClosePreview();
                    _previewFile = await AudioManager.CreateAudioTimelineAsync(audioTimeline, _cancellationTokenSource.Token);
                    _isPreviewOwned = true;
                    _previewKey = previewKey;
                    _previewPlayer.Open(new Uri(_previewFile));
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
                return false;
            }
            finally
            {
                IsControlBusy = false;
                Progress.Clear();
                CancellationTokenSource = null;
            }
        }


        /// <summary>
        /// Opens an existing audio file in the player, the file is not deleted when the preview is closed.
        /// Nothing is done if the file is already open.
        /// </summary>
        /// <param name="filename">The audio or video filename.</param>
        protected void OpenPreview(string filename)
        {
            if (_previewFile == filename && !_isPreviewOwned)
                return;

            ClosePreview();
            _previewFile = filename;
            _previewPlayer.Open(new Uri(filename));
        }


        /// <summary>
        /// Plays the preview from a position, the seek is applied once the preview has opened.
        /// </summary>
        /// <param name="position">The position.</param>
        protected void PlayPreview(TimeSpan position)
        {
            _previewStart = position;
            _previewPlayer.Play();
            if (_isPreviewOpen)
                _previewPlayer.Position = position;
        }


        /// <summary>
        /// Moves the preview to a position.
        /// </summary>
        /// <param name="position">The position.</param>
        protected void SeekPreview(TimeSpan position)
        {
            _previewStart = position;
            if (_isPreviewOpen)
                _previewPlayer.Position = position;
        }


        /// <summary>
        /// Pauses the preview.
        /// </summary>
        protected void PausePreview()
        {
            _previewPlayer.Pause();
        }


        /// <summary>
        /// Closes the player and deletes a mixed preview file, the next play mixes it again.
        /// </summary>
        protected void ClosePreview()
        {
            _previewPlayer.Stop();
            _previewPlayer.Close();
            _isPreviewOpen = false;
            if (_previewFile != null && _isPreviewOwned)
                FileHelper.QueueDeleteFile(_previewFile);

            _previewFile = null;
            _previewKey = null;
            _isPreviewOwned = false;
        }


        /// <summary>
        /// Called when the preview has played to its end.
        /// </summary>
        protected virtual void OnPreviewEnded()
        {
        }


        /// <summary>
        /// Called when the preview could not be played and has been closed.
        /// </summary>
        protected virtual void OnPreviewFailed()
        {
        }


        /// <summary>
        /// Called when the control is unloaded, the preview is closed.
        /// </summary>
        protected virtual void OnUnloaded()
        {
            ClosePreview();
        }


        /// <summary>
        /// Creates the waveform peaks of an audio file, normalized to the loudest peak.
        /// </summary>
        /// <param name="filename">The audio or video filename.</param>
        /// <param name="peaksPerSecond">The peaks per second.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected static async Task<float[]> CreatePeaksAsync(string filename, double peaksPerSecond, CancellationToken cancellationToken)
        {
            var audioTensor = await AudioManager.LoadTensorAsync(filename, WaveformSampleRate, 1, cancellationToken);
            return await Task.Run(() =>
            {
                var samples = audioTensor.Span;
                var scale = peaksPerSecond / WaveformSampleRate;
                var peaks = new float[(int)(samples.Length * scale) + 1];
                var maximum = 0f;
                for (int i = 0; i < samples.Length; i++)
                {
                    var index = (int)(i * scale);
                    var sample = Math.Abs(samples[i]);
                    if (sample > peaks[index])
                        peaks[index] = sample;
                    if (sample > maximum)
                        maximum = sample;
                }

                if (maximum > 0f)
                {
                    for (int i = 0; i < peaks.Length; i++)
                    {
                        peaks[i] /= maximum;
                    }
                }
                return peaks;
            }, cancellationToken);
        }


        /// <summary>
        /// Gets a key that changes when the audio timeline would mix differently.
        /// </summary>
        /// <param name="audioTimeline">The audio timeline.</param>
        private static string GetPreviewKey(AudioTimeline audioTimeline)
        {
            return $"{audioTimeline.Duration}|{audioTimeline.Overlap}|{string.Join("|", audioTimeline.Segments)}";
        }


        /// <summary>
        /// Determines whether there is a running operation to cancel.
        /// </summary>
        private bool CanCancel()
        {
            return _cancellationTokenSource != null;
        }


        /// <summary>
        /// Cancels the running operation.
        /// </summary>
        private async Task CancelAsync()
        {
            await _cancellationTokenSource.SafeCancelAsync();
        }


        /// <summary>
        /// Seeks the player to the requested position once the preview has opened.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void OnPreviewMediaOpened(object sender, EventArgs e)
        {
            _isPreviewOpen = true;
            _previewPlayer.Position = _previewStart;
        }


        /// <summary>
        /// Raises OnPreviewEnded when the preview has played to its end.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void OnPreviewMediaEnded(object sender, EventArgs e)
        {
            OnPreviewEnded();
        }


        /// <summary>
        /// Closes the preview when it can't be played, the next play mixes it again.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="ExceptionEventArgs"/> instance containing the event data.</param>
        private async void OnPreviewMediaFailed(object sender, ExceptionEventArgs e)
        {
            ClosePreview();
            OnPreviewFailed();
            await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, e.ErrorException?.Message);
        }


        /// <summary>
        /// Handles the Unloaded event of the control.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void OnControlUnloaded(object sender, RoutedEventArgs e)
        {
            OnUnloaded();
        }
    }
}
