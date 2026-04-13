// Copyright (c) TensorStack. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for ExtractModelDialog.xaml
    /// </summary>
    public partial class ExtractModelDialog : DialogControl
    {
        private ExtractModel _extractModel;
        private ExtractModel _originalExtractModel;
        private string _selectedFile;

        public ExtractModelDialog(Settings settings)
        {
            Settings = settings;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            AddFileCommand = new AsyncRelayCommand(AddFileAsync, CanAddFile);
            RemoveFileCommand = new AsyncRelayCommand<string>(RemoveFileAsync);
            Files = new ObservableCollection<string>();
            Errors = new ObservableCollection<string>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public AsyncRelayCommand AddFileCommand { get; }
        public AsyncRelayCommand<string> RemoveFileCommand { get; }
        public ObservableCollection<string> Files { get; }
        public bool IsUpdateMode => _originalExtractModel is not null;

        public ExtractModel ExtractModel
        {
            get { return _extractModel; }
            set { SetProperty(ref _extractModel, value); }
        }

        public string SelectedFile
        {
            get { return _selectedFile; }
            set { SetProperty(ref _selectedFile, value); }
        }


        public Task<bool> AddAsync()
        {
            var modelId = GetNextModelId();
            ExtractModel = new ExtractModel
            {
                Id = modelId,
                Backend = BackendType.OnnxRuntime,
                DefaultOptions = new ExtractInputOptions
                {

                }
            };
            return base.ShowDialogAsync();
        }


        public Task<bool> UpdateAsync(ExtractModel extractModel)
        {
            var modelId = extractModel.Id;
            _originalExtractModel = extractModel;
            ExtractModel = DeepClone(extractModel, modelId);
            foreach (var path in ExtractModel.UrlPaths)
            {
                Files.Add(path);
            }
            return base.ShowDialogAsync();
        }


        public Task<bool> CopyAsync(ExtractModel extractModel)
        {
            var modelId = GetNextModelId();
            ExtractModel = DeepClone(extractModel, modelId);
            foreach (var path in ExtractModel.UrlPaths)
            {
                Files.Add(path);
            }
            return base.ShowDialogAsync();
        }


        public Task<bool> ImportAsync(ExtractModel extractModel)
        {
            extractModel.Id = GetNextModelId();
            ExtractModel = extractModel;
            foreach (var path in ExtractModel.UrlPaths)
            {
                Files.Add(path);
            }
            return base.ShowDialogAsync();
        }


        private Task AddFileAsync()
        {
            Files.Add(SelectedFile);
            SelectedFile = null;
            return Task.CompletedTask;
        }


        private bool CanAddFile()
        {
            return !Files.Any(x => x == SelectedFile);
        }


        private Task RemoveFileAsync(string file)
        {
            Files.Remove(file);
            SelectedFile = null;
            return Task.CompletedTask;
        }


        protected override Task SaveAsync()
        {
            var index = Settings.ExtractModels.Count;
            if (IsUpdateMode)
            {
                index = Settings.ExtractModels.IndexOf(_originalExtractModel);
                Settings.ExtractModels.Remove(_originalExtractModel);
            }

            ExtractModel.UrlPaths = Files.ToArray();
            Settings.ExtractModels.Insert(index, ExtractModel);
            return base.SaveAsync();
        }


        protected override bool CanExecuteSave()
        {
            if (ExtractModel == null)
                return false;

            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        protected override Task CancelAsync()
        {
            ExtractModel = default;
            _originalExtractModel = null;
            return base.CancelAsync();
        }


        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        private int GetNextModelId()
        {
            return Math.Max(Utils.FixedIdRange, Settings.ExtractModels.Max(x => x.Id)) + 1;
        }


        private IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(ExtractModel.Name))
                yield return "Name cannot be empty";
            if (ExtractModel.Channels < 3)
                yield return "Channels must be >= 3";
            if (ExtractModel.SampleSize < 0)
                yield return "Sample Size must be >= 0";
            if (Files.IsNullOrEmpty())
                yield return "No files have been added";
            if (!IsUpdateMode && Settings.ExtractModels.Any(x => x.Name.Equals(ExtractModel.Name, StringComparison.OrdinalIgnoreCase)))
                yield return $"Model with name '{ExtractModel.Name}' already exists";
        }


        private static ExtractModel DeepClone(ExtractModel extractModel, int modelId)
        {
            return new ExtractModel
            {
                Id = modelId,
                Backend = extractModel.Backend,
                Name = extractModel.Name,
                Path = extractModel.Path,
                Channels = extractModel.Channels,
                IsDefault = extractModel.IsDefault,
                Normalization = extractModel.Normalization,
                OutputNormalization = extractModel.OutputNormalization,
                SampleSize = extractModel.SampleSize,
                OutputChannels = extractModel.OutputChannels,
                IsDynamicOutput = extractModel.IsDynamicOutput,
                Type = extractModel.Type,
                UrlPaths = extractModel.UrlPaths.ToArray(),
                IsGated = extractModel.IsGated,
                Link = extractModel.Link,
                DefaultOptions = new ExtractInputOptions
                {
                    BodyConfidence = extractModel.DefaultOptions.BodyConfidence,
                    BoneRadius = extractModel.DefaultOptions.BoneRadius,
                    BoneThickness = extractModel.DefaultOptions.BoneThickness,
                    ColorAlpha = extractModel.DefaultOptions.ColorAlpha,
                    Detections = extractModel.DefaultOptions.Detections,
                    IsInverted = extractModel.DefaultOptions.IsInverted,
                    IsTransparent = extractModel.DefaultOptions.IsTransparent,
                    JointConfidence = extractModel.DefaultOptions.JointConfidence,
                    JointRadius = extractModel.DefaultOptions.JointRadius,
                    MergeInput = extractModel.DefaultOptions.MergeInput,
                    Mode = extractModel.DefaultOptions.Mode,
                    IsTileEnabled = extractModel.DefaultOptions.IsTileEnabled,
                    TileOverlap = extractModel.DefaultOptions.TileOverlap,
                    TileSize = extractModel.DefaultOptions.TileSize
                }
            };
        }
    }
}
