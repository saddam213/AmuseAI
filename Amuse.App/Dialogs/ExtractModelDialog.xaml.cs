// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Resources;
using TensorStack.WPF.Services;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for ExtractModelDialog.xaml
    /// </summary>
    public partial class ExtractModelDialog : DialogControl
    {
        private ExtractModel _extractModel;
        private ExtractModel _originalExtractModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtractModelDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public ExtractModelDialog(Settings settings)
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
        public CheckpointType[] CheckpointTypes { get; } = [CheckpointType.OnlineFile, CheckpointType.LocalFile];
        public string FileFilter { get; } = DownloadLink.GetFileFilter(AppDefault.ModelFiles, [".onnx"]);
        public bool IsUpdateMode => _originalExtractModel is not null;

        public ExtractModel ExtractModel
        {
            get { return _extractModel; }
            set { SetProperty(ref _extractModel, value); }
        }

        /// <summary>
        /// Shows the dialog to update an extractor.
        /// </summary>
        /// <param name="extractModel">The extractor to update.</param>
        public Task<bool> UpdateAsync(ExtractModel extractModel)
        {
            var modelId = extractModel.Id;
            _originalExtractModel = extractModel;
            ExtractModel = extractModel.DeepClone(modelId);
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Shows the dialog to add a copy of an extractor.
        /// </summary>
        /// <param name="extractModel">The extractor to copy.</param>
        public Task<bool> CopyAsync(ExtractModel extractModel)
        {
            var modelId = Settings.ExtractModels.NextId();
            ExtractModel = extractModel.DeepClone(modelId);
            ExtractModel.Name += " copy";
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Imports extractors, showing the dialog for a single extractor and adding several directly.
        /// </summary>
        /// <param name="modelImports">The extractors to import.</param>
        public async Task<bool> ImportAsync(ExtractModel[] modelImports)
        {
            var modelId = Settings.ExtractModels.NextId();
            if (modelImports.Length == 1)
            {
                var modelImport = modelImports[0];
                modelImport.Id = modelId;
                ExtractModel = modelImport;
                return await base.ShowDialogAsync();
            }
            else
            {
                var imported = 0;
                foreach (var modelImport in modelImports)
                {
                    if (Settings.ExtractModels.Any(x => x.Backend == modelImport.Backend && x.Name == modelImport.Name))
                        continue;

                    imported++;
                    modelImport.Id = modelId++;
                    Settings.ExtractModels.Add(modelImport);
                }

                await DialogService.ShowMessageAsync(AppDefault.ImportCompleteTitle, string.Format(AppDefault.ImportCompleteModels, imported, modelImports.Length));
                return true;
            }
        }


        /// <summary>
        /// Saves the extractor, replacing the original when updating.
        /// </summary>
        protected override async Task SaveAsync()
        {
            ExtractModel.Name = ExtractModel.Name.Trim();
            var index = Settings.ExtractModels.Count;
            if (IsUpdateMode)
            {
                index = Settings.ExtractModels.IndexOf(_originalExtractModel);
                Settings.ExtractModels.Remove(_originalExtractModel);
            }
            Settings.ExtractModels.Insert(index, ExtractModel);
            ExtractModel.Initialize(Settings);
            await Settings.SaveAsync();
            await base.SaveAsync();
        }


        /// <summary>
        /// Determines whether the extractor can be saved, listing the validation errors.
        /// </summary>
        protected override bool CanExecuteSave()
        {
            if (ExtractModel == null)
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
            ExtractModel = default;
            _originalExtractModel = null;
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
            // Name
            if (string.IsNullOrWhiteSpace(ExtractModel.Name))
                yield return AppErrors.ValidationNameEmpty;
            else if (Settings.ExtractModels.Any(x => x != _originalExtractModel && x.Name.Equals(ExtractModel.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationModelNameExists, ExtractModel.Name.Trim());

            // Options
            if (ExtractModel.Channels <= 0)
                yield return string.Format(AppErrors.ValidationGreaterThanZero, Default.Channels);
            if (ExtractModel.SampleSize < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.SampleSize);
            if (ExtractModel.MemorySize < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.MemorySize);

            //DefaultOptions
            if (ExtractModel.DefaultOptions.TileSize < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, Default.TileSize);
            if (ExtractModel.DefaultOptions.TileOverlap < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, Default.TileOverlap);

            // Checkpoint
            if (!ExtractModel.Checkpoint.IsValid(out var checkpointValidation))
                yield return checkpointValidation;
        }

    }
}
