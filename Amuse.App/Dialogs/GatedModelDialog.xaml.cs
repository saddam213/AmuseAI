// Copyright (c) TensorStack. All rights reserved.
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

        public Task<bool> ShowDialogAsync(DiffusionModel model)
        {
            ModelName = model.Name;
            ModelLink = model.Link;
            return base.ShowDialogAsync();
        }


        protected override async Task SaveAsync()
        {
            await _navigationService.NavigateAsync((int)View.General);
            await base.SaveAsync();
        }


        protected override async Task CancelAsync()
        {
            await base.CancelAsync();
        }
    }
}
