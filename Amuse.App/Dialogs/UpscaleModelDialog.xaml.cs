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
    /// Interaction logic for UpscaleModelDialog.xaml
    /// </summary>
    public partial class UpscaleModelDialog : DialogControl
    {
        private UpscaleModel _upscaleModel;
        private UpscaleModel _originalUpscaleModel;
        private string _selectedFile;

        public UpscaleModelDialog(Settings settings)
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
        public bool IsUpdateMode => _originalUpscaleModel is not null;

        public UpscaleModel UpscaleModel
        {
            get { return _upscaleModel; }
            set { SetProperty(ref _upscaleModel, value); }
        }

        public string SelectedFile
        {
            get { return _selectedFile; }
            set { SetProperty(ref _selectedFile, value); }
        }


        public Task<bool> AddAsync()
        {
            var modelId = GetNextModelId();
            UpscaleModel = new UpscaleModel
            {
                Id = modelId,
                Backend = BackendType.OnnxRuntime,
                DefaultOptions = new UpscaleInputOptions
                {
                    IsTileEnabled = true,
                    TileOverlap = 16,
                    TileSize = 512
                }
            };
            return base.ShowDialogAsync();
        }


        public Task<bool> UpdateAsync(UpscaleModel upscaleModel)
        {
            var modelId = upscaleModel.Id;
            _originalUpscaleModel = upscaleModel;
            UpscaleModel = DeepClone(upscaleModel, modelId);
            foreach (var path in UpscaleModel.UrlPaths)
            {
                Files.Add(path);
            }
            return base.ShowDialogAsync();
        }


        public Task<bool> CopyAsync(UpscaleModel upscaleModel)
        {
            var modelId = GetNextModelId();
            UpscaleModel = DeepClone(upscaleModel, modelId);
            foreach (var path in UpscaleModel.UrlPaths)
            {
                Files.Add(path);
            }
            return base.ShowDialogAsync();
        }


        public Task<bool> ImportAsync(UpscaleModel upscaleModel)
        {
            upscaleModel.Id = GetNextModelId();
            UpscaleModel = upscaleModel;
            foreach (var path in UpscaleModel.UrlPaths)
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
            var index = Settings.UpscaleModels.Count;
            if (IsUpdateMode)
            {
                index = Settings.UpscaleModels.IndexOf(_originalUpscaleModel);
                Settings.UpscaleModels.Remove(_originalUpscaleModel);
            }

            UpscaleModel.UrlPaths = Files.ToArray();
            Settings.UpscaleModels.Insert(index, UpscaleModel);
            return base.SaveAsync();
        }


        protected override bool CanExecuteSave()
        {
            if (UpscaleModel == null)
                return false;

            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        protected override Task CancelAsync()
        {
            UpscaleModel = default;
            _originalUpscaleModel = null;
            return base.CancelAsync();
        }


        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        private int GetNextModelId()
        {
            return Math.Max(Utils.FixedIdRange, Settings.UpscaleModels.Max(x => x.Id)) + 1;
        }


        private IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(UpscaleModel.Name))
                yield return "Name cannot be empty";
            if (UpscaleModel.Channels < 3)
                yield return "Channels must be >= 3";
            if (UpscaleModel.ScaleFactor <= 0)
                yield return "Scale Factor must be > 0";
            if (UpscaleModel.SampleSize < 0)
                yield return "Sample Size must be >= 0";
            if (Files.IsNullOrEmpty())
                yield return "No files have been added";
            if (!IsUpdateMode && Settings.UpscaleModels.Any(x => x.Name.Equals(UpscaleModel.Name, StringComparison.OrdinalIgnoreCase)))
                yield return $"Model with name '{UpscaleModel.Name}' already exists";
        }


        private static UpscaleModel DeepClone(UpscaleModel upscaleModel, int modelId)
        {
            return new UpscaleModel
            {
                Id = modelId,
                Backend = upscaleModel.Backend,
                Name = upscaleModel.Name,
                Path = upscaleModel.Path,
                Channels = upscaleModel.Channels,
                IsDefault = upscaleModel.IsDefault,
                Normalization = upscaleModel.Normalization,
                OutputNormalization = upscaleModel.OutputNormalization,
                SampleSize = upscaleModel.SampleSize,
                ScaleFactor = upscaleModel.ScaleFactor,
                UrlPaths = upscaleModel.UrlPaths.ToArray(),
                IsGated = upscaleModel.IsGated,
                Link = upscaleModel.Link,
                DefaultOptions = new UpscaleInputOptions
                {
                    IsTileEnabled = upscaleModel.DefaultOptions.IsTileEnabled,
                    TileOverlap = upscaleModel.DefaultOptions.TileOverlap,
                    TileSize = upscaleModel.DefaultOptions.TileSize,
                }
            };
        }
    }
}
