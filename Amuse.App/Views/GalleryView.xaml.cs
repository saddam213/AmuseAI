using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using TensorStack.Common;
using TensorStack.Media.Audio;
using TensorStack.Media.Video;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Image;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Interaction logic for GalleryView.xaml
    /// </summary>
    public partial class GalleryView : ViewBase
    {
        private const string FilterAllModels = "";
        private IHistoryItem _selectedItem;
        private ImageInput _currentImage;
        private AudioInputStream _currentAudioStream;
        private VideoInputStream _currentVideoStream;
        private string _filterText;
        private string _filterModelName;
        private MediaType? _filterMediaType;
        private View? _filterProcessType;
        private TextInput _currentText;

        public GalleryView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IHistoryService historyService, ILogger<GalleryView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, historyService, logger)
        {
            RemoveFiltersCommand = new AsyncRelayCommand(RemoveFilters, CanRemoveFilters);
            DeleteItemCommand = new AsyncRelayCommand<IHistoryItem>(RemoveItemAsync, (x) => SelectedItem is not null);
            DeleteItemsCommand = new AsyncRelayCommand(RemoveItemsAsync, () => HistoryCollection.Count > 0);
            FilterModelNames = new ObservableCollection<string>();
            FilterProcessTypes = GetProcessTypes();
            HistoryCollection = new ListCollectionView(HistoryService.HistoryCollection) { Filter = HistoryCollectionFilter() };
            HistoryCollection.SortDescriptions.Add(new SortDescription(nameof(IHistoryItem.Timestamp), ListSortDirection.Descending));
            Populate();
            InitializeComponent();
            HistoryCollection.MoveCurrentToFirst();
            SelectedItem = HistoryCollection.CurrentItem as IHistoryItem;
        }

        public override View View => View.Gallery;
        public ListCollectionView HistoryCollection { get; }
        public View[] FilterProcessTypes { get; }
        public AsyncRelayCommand RemoveFiltersCommand { get; }
        public AsyncRelayCommand<IHistoryItem> DeleteItemCommand { get; }
        public AsyncRelayCommand DeleteItemsCommand { get; }
        public ObservableCollection<string> FilterModelNames { get; }


        public IHistoryItem SelectedItem
        {
            get { return _selectedItem; }
            set { SetProperty(ref _selectedItem, value); }
        }

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

        public VideoInputStream CurrentVideoStream
        {
            get { return _currentVideoStream; }
            set { SetProperty(ref _currentVideoStream, value); }
        }

        public TextInput CurrentText
        {
            get { return _currentText; }
            set { SetProperty(ref _currentText, value); }
        }

        public string FilterText
        {
            get { return _filterText; }
            set { SetProperty(ref _filterText, value); HistoryCollection?.Refresh(); }
        }

        public string FilterModelName
        {
            get { return _filterModelName; }
            set { SetProperty(ref _filterModelName, value); HistoryCollection?.Refresh(); }
        }

        public MediaType? FilterMediaType
        {
            get { return _filterMediaType; }
            set { SetProperty(ref _filterMediaType, value); HistoryCollection?.Refresh(); }
        }

        public View? FilterProcessType
        {
            get { return _filterProcessType; }
            set { SetProperty(ref _filterProcessType, value); HistoryCollection?.Refresh(); }
        }


        public override async Task OpenAsync(OpenViewArgs args = null)
        {
            if (CurrentText != null && TextControl.Length == 0)
                await TextControl.SetTextAsync(CurrentText.Text);

            await base.OpenAsync(args);
        }


        public override async Task CloseAsync()
        {
            await AudioControl.StopAsync();
            await VideoControl.StopAsync();
            await TextControl.CloseAsync();
            await base.CloseAsync();
        }


        private async Task SetCurrentItem()
        {
            if (_selectedItem == null)
                return;

            try
            {
                Progress.Indeterminate();

                CurrentText = default;
                CurrentImage = default;
                CurrentAudioStream = default;
                CurrentVideoStream = default;
                if (_selectedItem?.MediaType == MediaType.Text)
                    CurrentText = await TensorStack.Common.TextInput.CreateAsync(_selectedItem.MediaPath, Encoding.UTF8);
                if (_selectedItem?.MediaType == MediaType.Image)
                    CurrentImage = await ImageInput.CreateAsync(_selectedItem.MediaPath);
                if (_selectedItem?.MediaType == MediaType.Audio)
                    CurrentAudioStream = await AudioInputStream.CreateAsync(_selectedItem.MediaPath);
                if (_selectedItem?.MediaType == MediaType.Video)
                    CurrentVideoStream = await VideoInputStream.CreateAsync(_selectedItem.MediaPath);
            }
            finally
            {
                Progress.Clear();
            }
        }


        private Predicate<object> HistoryCollectionFilter()
        {
            return (obj) =>
            {
                if (obj is not IHistoryItem historyItem)
                    return false;

                if (historyItem is RecentHistory)
                    return false;

                var isValid = true;
                if (_filterMediaType.HasValue)
                    isValid = historyItem.MediaType == _filterMediaType.Value;
                if (_filterProcessType.HasValue)
                    isValid = isValid && historyItem.Source == _filterProcessType.Value;
                if (_filterModelName?.Equals(FilterAllModels) == false)
                    isValid = isValid && historyItem.Model == _filterModelName;

                if (!string.IsNullOrEmpty(_filterText))
                {
                    var diffusionOptions = (historyItem as DiffusionHistory)?.Options;
                    var isPromptMatch = diffusionOptions?.Prompt?.Contains(_filterText, StringComparison.OrdinalIgnoreCase) == true;
                    var isNegativePromptMatch = diffusionOptions?.NegativePrompt?.Contains(_filterText, StringComparison.OrdinalIgnoreCase) == true;
                    isValid = isValid && (isPromptMatch || isNegativePromptMatch);
                }

                return isValid;
            };
        }


        private void Populate()
        {
            FilterModelNames.Clear();
            FilterModelNames.Add(FilterAllModels);
            foreach (var modelName in HistoryService.HistoryCollection.Select(x => x.Model).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x))
            {
                FilterModelNames.Add(modelName);
            }
            FilterModelName = FilterModelNames[0];
        }


        private async Task RemoveItemAsync(IHistoryItem historyItem)
        {
            if (!HistoryCollection.MoveCurrentToNext())
                HistoryCollection.MoveCurrentToPrevious();

            await HistoryService.DeleteAsync(historyItem);
        }


        private async Task RemoveItemsAsync()
        {
            var message = HistoryCollection.Count < HistoryService.HistoryCollection.Count
                ? string.Format(AppDefault.DeleteGalleryItemsFiltered, HistoryCollection.Count)
                : string.Format(AppDefault.DeleteGalleryItems, HistoryCollection.Count);
            if (await DialogService.ShowMessageAsync(AppDefault.DeleteItems, message, TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                var toRemove = new List<IHistoryItem>();
                foreach (var item in HistoryCollection)
                {
                    if (item is IHistoryItem historyItem)
                        toRemove.Add(historyItem);
                }

                foreach (var historyItem in toRemove)
                {
                    await HistoryService.DeleteAsync(historyItem);
                }
                toRemove.Clear();
                await RemoveFilters();
            }
        }


        private Task RemoveFilters()
        {
            _filterText = null;
            _filterMediaType = null;
            _filterProcessType = null;
            _filterModelName = FilterAllModels;

            HistoryCollection.Refresh();
            NotifyPropertyChanged(nameof(FilterText));
            NotifyPropertyChanged(nameof(FilterModelName));
            NotifyPropertyChanged(nameof(FilterMediaType));
            NotifyPropertyChanged(nameof(FilterProcessType));
            return Task.CompletedTask;
        }


        private bool CanRemoveFilters()
        {
            return !string.IsNullOrWhiteSpace(_filterText)
                || _filterModelName?.Equals(FilterAllModels) == false
                || _filterMediaType.HasValue
                || _filterProcessType.HasValue;
        }


        private static View[] GetProcessTypes()
        {
            return Enum.GetValues<View>().Except([View.Home, View.SettingsImage, View.SettingsVideo, View.SettingsAudio, View.SettingsControlNet, View.SettingsLoraAdapter, View.SettingsExtract, View.SettingsUpscale, View.SettingsGeneral, View.SettingsEnvironment, View.SettingsTool, View.Gallery, View.Recent]).ToArray();
        }


        private async void ListBoxControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await SetCurrentItem();
        }
    }
}