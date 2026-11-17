// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Views;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for GatedModelDialog.xaml
    /// </summary>
    public partial class GatedModelDialog : DialogControl
    {
        private readonly NavigationService _navigationService;
        private string _modelName;
        private string _modelLink;
        private string _message;

        public GatedModelDialog(NavigationService navigationService)
        {
            _navigationService = navigationService;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync, CanExecuteCancel);
            InitializeComponent();
        }

        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }

        public string ModelName
        {
            get { return _modelName; }
            set { SetProperty(ref _modelName, value); }
        }

        public string ModelLink
        {
            get { return _modelLink; }
            set { SetProperty(ref _modelLink, value); }
        }

        public string Message
        {
            get { return _message; }
            set { SetProperty(ref _message, value); }
        }

        public Task<bool> ShowDialogAsync(IDownloadModel model)
        {
            ModelName = model.Name;
            ModelLink = model.Link;
            Message = $"This model requires a {model.AccessToken} Access Token, you can add this on the settings page";
            return base.ShowDialogAsync();
        }


        protected override async Task SaveAsync()
        {
            await _navigationService.NavigateAsync((int)View.SettingsGeneral);
            await base.CancelAsync();
        }


        protected override async Task CancelAsync()
        {
            await base.CancelAsync();
        }
    }
}
