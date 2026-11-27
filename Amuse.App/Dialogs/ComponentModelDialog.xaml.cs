// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for ComponentModelDialog.xaml
    /// </summary>
    public partial class ComponentModelDialog : DialogControl
    {
        private ComponentModel _componentModel;
        private ComponentModel _originalComponentModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentModelDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public ComponentModelDialog(Settings settings)
        {
            Settings = settings;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            Errors = new ObservableCollection<string>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public CheckpointType[] CheckpointTypes { get; } = [CheckpointType.OnlineFolder, CheckpointType.OnlineFile, CheckpointType.LocalFolder, CheckpointType.LocalFile];
        public bool IsUpdateMode => _originalComponentModel is not null;
        public bool IsKeyReadOnly => _originalComponentModel?.Id <= Utils.FixedIdRange;

        public ComponentModel ComponentModel
        {
            get { return _componentModel; }
            set { SetProperty(ref _componentModel, value); }
        }


        /// <summary>
        /// Shows the dialog to update a component.
        /// </summary>
        /// <param name="componentModel">The component to update.</param>
        public Task<bool> UpdateAsync(ComponentModel componentModel)
        {
            var modelId = componentModel.Id;
            _originalComponentModel = componentModel;
            ComponentModel = componentModel.DeepClone(modelId);
            NotifyPropertyChanged(nameof(IsKeyReadOnly));
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Shows the dialog to add a copy of a component.
        /// </summary>
        /// <param name="componentModel">The component to copy.</param>
        public Task<bool> CopyAsync(ComponentModel componentModel)
        {
            var modelId = Settings.Components.NextId();
            ComponentModel = componentModel.DeepClone(modelId);
            ComponentModel.Name += " copy";
            ComponentModel.Key += "_COPY";
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Imports components, showing the dialog for a single component and adding several directly.
        /// </summary>
        /// <param name="modelImports">The components to import.</param>
        public async Task<bool> ImportAsync(ComponentModel[] modelImports)
        {
            var modelId = Settings.Components.NextId();
            if (modelImports.Length == 1)
            {
                var modelImport = modelImports[0];
                modelImport.Id = modelId;
                ComponentModel = modelImport;
                return await base.ShowDialogAsync();
            }
            else
            {
                var imported = 0;
                foreach (var modelImport in modelImports)
                {
                    if (Settings.Components.Any(x => x.Name == modelImport.Name || x.Key.Equals(modelImport.Key, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    imported++;
                    modelImport.Id = modelId++;
                    Settings.Components.Add(modelImport);
                }

                await DialogService.ShowMessageAsync(AppDefault.ImportCompleteTitle, string.Format(AppDefault.ImportCompleteComponents, imported, modelImports.Length));
                return true;
            }
        }


        /// <summary>
        /// Saves the component, replacing the original when updating.
        /// </summary>
        protected override Task SaveAsync()
        {
            ComponentModel.Name = ComponentModel.Name.Trim();
            if (!IsKeyReadOnly)
                ComponentModel.Key = ComponentModel.Key.Trim().ToUpperInvariant();

            var index = Settings.Components.Count;
            if (IsUpdateMode)
            {
                if (_originalComponentModel.Key != ComponentModel.Key)
                    Settings.RenameComponentKey(_originalComponentModel.Key, ComponentModel.Key);

                index = Settings.Components.IndexOf(_originalComponentModel);
                Settings.Components.Remove(_originalComponentModel);
            }
            Settings.Components.Insert(index, ComponentModel);
            ComponentModel.Initialize(Settings);
            return base.SaveAsync();
        }


        /// <summary>
        /// Determines whether the component can be saved, listing the validation errors.
        /// </summary>
        protected override bool CanExecuteSave()
        {
            if (ComponentModel == null)
                return false;

            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        /// <summary>
        /// Cancels the dialog.
        /// </summary>
        protected override Task CancelAsync()
        {
            ComponentModel = default;
            _originalComponentModel = null;
            return base.CancelAsync();
        }


        /// <summary>
        /// Closes the dialog.
        /// </summary>
        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        /// <summary>
        /// Gets the validation errors of the current input.
        /// </summary>
        private IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(ComponentModel.Name))
                yield return AppErrors.ValidationNameEmpty;
            else if (Settings.Components.Any(x => x != _originalComponentModel && x.Name.Equals(ComponentModel.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationModelNameExists, ComponentModel.Name.Trim());

            if (string.IsNullOrWhiteSpace(ComponentModel.Key))
                yield return AppErrors.ValidationKeyEmpty;
            else if (Settings.Components.Any(x => x != _originalComponentModel && x.Key.Equals(ComponentModel.Key.Trim(), StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationKeyExists, ComponentModel.Key.Trim());

            if (!ComponentModel.Checkpoint.IsValid(out var checkpointValidation))
                yield return checkpointValidation;

            var isFolder = ComponentModel.Checkpoint.Type == CheckpointType.OnlineFolder || ComponentModel.Checkpoint.Type == CheckpointType.LocalFolder;
            if (isFolder && string.IsNullOrEmpty(ComponentModel.Checkpoint.MainFile))
                yield return AppErrors.ValidationMainFileEmpty;
        }
    }
}
