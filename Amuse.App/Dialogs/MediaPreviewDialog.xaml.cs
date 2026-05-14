// Copyright (c) TensorStack. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Services;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using TensorStack.Audio;
using TensorStack.Common;
using TensorStack.Image;
using TensorStack.Video;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

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

        public MediaPreviewDialog(Settings settings, IHistoryService historyService)
        {

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
            InitializeComponent();
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


        public async Task<bool> ShowDialogAsync(IHistoryItem selectedItem)
        {
            HistoryCollection.MoveCurrentTo(selectedItem);
            await SetCurrentImage();
            return await base.ShowDialogAsync();
        }


        private async Task PrevAsync()
        {
            if (CanMovePrev())
            {
                HistoryCollection.MoveCurrentToPrevious();
                await SetCurrentImage();
            }
        }


        private bool CanMovePrev()
        {
            return !HistoryCollection.IsCurrentBeforeFirst
                 && HistoryCollection.CurrentPosition > 0;
        }


        private async Task NextAsync()
        {
            if (CanMoveNext())
            {
                HistoryCollection.MoveCurrentToNext();
                await SetCurrentImage();
            }
        }


        private bool CanMoveNext()
        {
            return !HistoryCollection.IsCurrentAfterLast
                 && HistoryCollection.CurrentPosition < HistoryCollection.Count - 1;
        }


        private async Task SetCurrentImage()
        {
            var currentItem = HistoryCollection.CurrentItem as IHistoryItem;
            if (currentItem == null)
                return;

            try
            {
                Progress.Indeterminate();

                CurrentText = default;
                CurrentImage = default;
                CurrentAudioStream = default;
                CurrentVideoStream = default;
                if (currentItem.MediaType == MediaType.Text)
                    CurrentText = await TensorStack.Common.TextInput.CreateAsync(currentItem.MediaPath, Encoding.UTF8);
                if (currentItem.MediaType == MediaType.Image)
                    CurrentImage = await ImageInput.CreateAsync(currentItem.MediaPath);
                if (currentItem.MediaType == MediaType.Audio)
                    CurrentAudioStream = await AudioInputStream.CreateAsync(currentItem.MediaPath);
                if (currentItem.MediaType == MediaType.Video)
                    CurrentVideoStream = await VideoInputStream.CreateAsync(currentItem.MediaPath);
            }
            finally
            {
                Progress.Clear();
            }
        }


        protected override Task CloseAsync()
        {
            CurrentText = default;
            CurrentImage = default;
            CurrentAudioStream = default;
            CurrentVideoStream = default;
            return base.CloseAsync();
        }


        protected override Task CancelAsync()
        {
            CurrentText = default;
            CurrentImage = default;
            CurrentAudioStream = default;
            CurrentVideoStream = default;
            return base.CancelAsync();
        }
    }
}
