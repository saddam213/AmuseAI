// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using TensorStack.Common;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Image;
using TensorStack.WPF.Services;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for MediaPreviewDialog.xaml
    /// </summary>
    public partial class MediaPreviewDialog : DialogControl
    {
        private ImageInput _currentImage;
        private VideoInputStream _currentVideoStream;
        private AudioInputStream _currentAudioStream;
        private TextInput _currentText;
        private string _position;
        private readonly ILogger<MediaPreviewDialog> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaPreviewDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="historyService">The history service.</param>
        /// <param name="logger">The logger.</param>
        public MediaPreviewDialog(Settings settings, IHistoryService historyService, ILogger<MediaPreviewDialog> logger)
        {
            _logger = logger;
            Settings = settings;
            HistoryService = historyService;
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            PrevCommand = new AsyncRelayCommand(PrevAsync, CanMovePrev);
            NextCommand = new AsyncRelayCommand(NextAsync, CanMoveNext);
            Progress = new ProgressInfo();
            HistoryCollection = new ListCollectionView(HistoryService.HistoryCollection)
            {
                Filter = (obj) =>
                {
                    if (obj is not IHistoryItem item)
                        return false;
                    return true;
                }
            };
            Loaded += (s, e) => { MaxWidth = double.PositiveInfinity; MaxHeight = double.PositiveInfinity; };
            Closed += (s, e) => (HistoryCollection as ListCollectionView)?.DetachFromSourceCollection();
            InitializeComponent();
            InputBindings.Add(new KeyBinding(PrevCommand, Key.Left, ModifierKeys.None));
            InputBindings.Add(new KeyBinding(NextCommand, Key.Right, ModifierKeys.None));
        }

        public Settings Settings { get; }
        public IHistoryService HistoryService { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand PrevCommand { get; }
        public AsyncRelayCommand NextCommand { get; }
        public ListCollectionView HistoryCollection { get; }
        public ProgressInfo Progress { get; }

        public ImageInput CurrentImage
        {
            get { return _currentImage; }
            set { SetProperty(ref _currentImage, value); }
        }

        public AudioInputStream CurrentAudioStream
        {
            get { return _currentAudioStream; }
            set { SetProperty(ref _currentAudioStream, value); }
        }

        public TextInput CurrentText
        {
            get { return _currentText; }
            set { SetProperty(ref _currentText, value); }
        }

        public VideoInputStream CurrentVideoStream
        {
            get { return _currentVideoStream; }
            set { SetProperty(ref _currentVideoStream, value); }
        }

        public string Position
        {
            get { return _position; }
            set { SetProperty(ref _position, value); }
        }


        /// <summary>
        /// Shows the dialog with an item of the history selected.
        /// </summary>
        /// <param name="selectedItem">The history item to show first.</param>
        public async Task<bool> ShowDialogAsync(IHistoryItem selectedItem)
        {
            HistoryCollection.MoveCurrentTo(selectedItem);
            await SetCurrentImage();
            return await base.ShowDialogAsync();
        }


        /// <summary>
        /// Shows the previous history item.
        /// </summary>
        private async Task PrevAsync()
        {
            if (CanMovePrev())
            {
                HistoryCollection.MoveCurrentToPrevious();
                await SetCurrentImage();
            }
        }


        /// <summary>
        /// Determines whether there is a previous history item.
        /// </summary>
        private bool CanMovePrev()
        {
            return !HistoryCollection.IsCurrentBeforeFirst
                 && HistoryCollection.CurrentPosition > 0;
        }


        /// <summary>
        /// Shows the next history item.
        /// </summary>
        private async Task NextAsync()
        {
            if (CanMoveNext())
            {
                HistoryCollection.MoveCurrentToNext();
                await SetCurrentImage();
            }
        }


        /// <summary>
        /// Determines whether there is a next history item.
        /// </summary>
        private bool CanMoveNext()
        {
            return !HistoryCollection.IsCurrentAfterLast
                 && HistoryCollection.CurrentPosition < HistoryCollection.Count - 1;
        }


        /// <summary>
        /// Loads the media of the current history item.
        /// </summary>
        private async Task SetCurrentImage()
        {
            var currentItem = HistoryCollection.CurrentItem as IHistoryItem;
            if (currentItem == null)
                return;

            try
            {
                Progress.Indeterminate();
                ClearMedia();
                Position = $"{HistoryCollection.CurrentPosition + 1} / {HistoryCollection.Count}";
                if (currentItem.MediaType == MediaType.Text)
                    CurrentText = await TensorStack.Common.TextInput.CreateAsync(currentItem.MediaPath, Encoding.UTF8);
                if (currentItem.MediaType == MediaType.Image)
                    CurrentImage = await ImageInput.CreateAsync(currentItem.MediaPath);
                if (currentItem.MediaType == MediaType.Audio)
                    CurrentAudioStream = await AudioInputStream.CreateAsync(currentItem.MediaPath);
                if (currentItem.MediaType == MediaType.Video)
                    CurrentVideoStream = await VideoInputStream.CreateAsync(currentItem.MediaPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MediaPreviewDialog] [Load] Loading media failed, File: {File}", currentItem.MediaPath);
                await DialogService.ShowErrorAsync(AppErrors.UnexpectedErrorTitle, ex.Message);
            }
            finally
            {
                Progress.Clear();
            }
        }


        /// <summary>
        /// Closes the dialog, releasing the shown media.
        /// </summary>
        protected override async Task CloseAsync()
        {
            ClearMedia();
            await TextControl.CloseAsync();
            await base.CloseAsync();
        }


        /// <summary>
        /// Cancels the dialog, releasing the shown media.
        /// </summary>
        protected override async Task CancelAsync()
        {
            ClearMedia();
            await TextControl.CloseAsync();
            await base.CancelAsync();
        }


        /// <summary>
        /// Clears the shown media, disposing the image after it is no longer shown.
        /// </summary>
        private void ClearMedia()
        {
            var currentImage = _currentImage;
            CurrentText = default;
            CurrentImage = default;
            CurrentAudioStream = default;
            CurrentVideoStream = default;
            currentImage?.Dispose();
        }
    }
}
