// Copyright (c) TensorStack. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for ControlNetModelDialog.xaml
    /// </summary>
    public partial class ControlNetModelDialog : DialogControl
    {
        private ControlNetModel _controlNetModel;
        private ControlNetModel _originalControlNetModel;

        public ControlNetModelDialog(Settings settings)
        {
            Settings = settings;
            ModelSources = [ModelSourceType.HuggingFace, ModelSourceType.SingleFile, ModelSourceType.Folder];
            Pipelines = new ObservableCollection<string>(Settings.GetPipelines());
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            Errors = new ObservableCollection<string>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public ObservableCollection<string> Pipelines { get; }
        public bool IsUpdateMode => _originalControlNetModel is not null;
        public ModelSourceType[] ModelSources { get; }

        public ControlNetModel ControlNetModel
        {
            get { return _controlNetModel; }
            set { SetProperty(ref _controlNetModel, value); }
        }

        public Task<bool> AddAsync()
        {
            var modelId = GetNextModelId();
            ControlNetModel = new ControlNetModel
            {
                Id = modelId,
                Backend = BackendType.Pytorch,
                Source = ModelSourceType.HuggingFace
            };
            return base.ShowDialogAsync();
        }


        public Task<bool> UpdateAsync(ControlNetModel controlNetModel)
        {
            var modelId = controlNetModel.Id;
            _originalControlNetModel = controlNetModel;
            ControlNetModel = DeepClone(controlNetModel, modelId);
            return base.ShowDialogAsync();
        }


        public Task<bool> CopyAsync(ControlNetModel controlNetModel)
        {
            var modelId = GetNextModelId();
            ControlNetModel = DeepClone(controlNetModel, modelId);
            return base.ShowDialogAsync();
        }


        internal Task<bool> ImportAsync(ControlNetModel controlNetModel)
        {
            controlNetModel.Id = GetNextModelId();
            ControlNetModel = controlNetModel;
            return base.ShowDialogAsync();
        }


        protected override Task SaveAsync()
        {
            var index = Settings.ControlNetModels.Count;
            if (IsUpdateMode)
            {
                index = Settings.ControlNetModels.IndexOf(_originalControlNetModel);
                Settings.ControlNetModels.Remove(_originalControlNetModel);
            }
            Settings.ControlNetModels.Insert(index, ControlNetModel);
            return base.SaveAsync();
        }


        protected override bool CanExecuteSave()
        {
            if (ControlNetModel == null)
                return false;

            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        protected override Task CancelAsync()
        {
            ControlNetModel = default;
            _originalControlNetModel = null;
            return base.CancelAsync();
        }


        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        private int GetNextModelId()
        {
            return Math.Max(Utils.FixedIdRange, Settings.ControlNetModels.Max(x => x.Id)) + 1;
        }


        private IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(ControlNetModel.Name))
                yield return "Name cannot be empty";
            if (string.IsNullOrWhiteSpace(ControlNetModel.Path))
                yield return "Path cannot be empty";
            if (string.IsNullOrWhiteSpace(ControlNetModel.Pipeline))
                yield return "Pipeline cannot be empty";
            if (!IsUpdateMode && Settings.ControlNetModels.Any(x => x.Pipeline == ControlNetModel.Pipeline && x.Name.Equals(ControlNetModel.Name, StringComparison.OrdinalIgnoreCase)))
                yield return $"Model with name '{ControlNetModel.Name}' already exists";

            if (!string.IsNullOrWhiteSpace(ControlNetModel.Path))
            {
                if (ControlNetModel.Source == ModelSourceType.Folder && !Directory.Exists(ControlNetModel.Path))
                    yield return "Model folder not found";
                else if (ControlNetModel.Source == ModelSourceType.SingleFile && !File.Exists(ControlNetModel.Path))
                    yield return "Model file not found";
                else if (ControlNetModel.Source == ModelSourceType.HuggingFace && !IsLControlNetValid(ControlNetModel.Path))
                    yield return "HuggingFace repository not found";
            }
        }


        private bool IsLControlNetValid(string controlNetPath)
        {
            return File.Exists(controlNetPath) || Utils.IsCheckpointInstalled(Settings.DirectoryCache, controlNetPath) || Utils.IsHuggingFaceLink(controlNetPath);
        }


        private static ControlNetModel DeepClone(ControlNetModel controlNetModel, int modelId)
        {
            return new ControlNetModel
            {
                Id = modelId,
                Name = controlNetModel.Name,
                Path = controlNetModel.Path,
                Pipeline = controlNetModel.Pipeline,
                Backend = controlNetModel.Backend,
                Source = controlNetModel.Source,
                IsGated = controlNetModel.IsGated,
                Link = controlNetModel.Link,
            };
        }
    }
}
