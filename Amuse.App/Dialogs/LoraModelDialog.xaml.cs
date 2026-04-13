// Copyright (c) TensorStack. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for LoraModelDialog.xaml
    /// </summary>
    public partial class LoraModelDialog : DialogControl
    {
        private LoraAdapterModel _loraModel;
        private LoraAdapterModel _originalLoraModel;
        private string _selectedTrigger;
        private string _selectedPath;
        private string _selectedWeights;

        public LoraModelDialog(Settings settings)
        {
            Settings = settings;
            ModelSources = [ModelSourceType.HuggingFace, ModelSourceType.SingleFile, ModelSourceType.Folder];
            Trigger = new ObservableCollection<string>();
            Pipelines = new ObservableCollection<string>(Settings.GetPipelines());
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            AddTriggerCommand = new AsyncRelayCommand(AddTriggerAsync, CanAddTrigger);
            RemoveTriggerCommand = new AsyncRelayCommand<string>(RemoveTriggerAsync);
            Errors = new ObservableCollection<string>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public AsyncRelayCommand AddTriggerCommand { get; }
        public AsyncRelayCommand<string> RemoveTriggerCommand { get; }
        public ObservableCollection<string> Trigger { get; }
        public ObservableCollection<string> Pipelines { get; }
        public bool IsUpdateMode => _originalLoraModel is not null;
        public ModelSourceType[] ModelSources { get; }

        public LoraAdapterModel LoraModel
        {
            get { return _loraModel; }
            set { SetProperty(ref _loraModel, value); }
        }

        public string SelectedTrigger
        {
            get { return _selectedTrigger; }
            set { SetProperty(ref _selectedTrigger, value); }
        }

        public string SelectedPath
        {
            get { return _selectedPath; }
            set
            {
                SetProperty(ref _selectedPath, value);
                if (_loraModel.Source == ModelSourceType.SingleFile)
                {
                    if (string.IsNullOrEmpty(_selectedPath))
                    {
                        SelectedWeights = null;
                        return;
                    }
                    SelectedWeights = Path.GetFileName(_selectedPath);
                }
            }
        }

        public string SelectedWeights
        {
            get { return _selectedWeights; }
            set { SetProperty(ref _selectedWeights, value); }
        }


        public Task<bool> AddAsync()
        {
            var modelId = GetNextModelId();
            LoraModel = new LoraAdapterModel
            {
                Id = modelId,
                Backend = BackendType.Pytorch,
                Pipeline = Pipelines.First(),
                Source = ModelSourceType.SingleFile
            };
            return base.ShowDialogAsync();
        }


        public Task<bool> UpdateAsync(LoraAdapterModel loraModel)
        {
            var modelId = loraModel.Id;
            _originalLoraModel = loraModel;
            LoraModel = DeepClone(loraModel, modelId);
            Populate();
            return base.ShowDialogAsync();
        }


        public Task<bool> CopyAsync(LoraAdapterModel loraModel)
        {
            var modelId = GetNextModelId();
            LoraModel = DeepClone(loraModel, modelId);
            LoraModel.Name += " copy";
            Populate();
            return base.ShowDialogAsync();
        }


        public Task<bool> ImportAsync(LoraAdapterModel loraModel)
        {
            loraModel.Id = GetNextModelId();
            LoraModel = loraModel;
            Populate();
            return base.ShowDialogAsync();
        }


        protected override Task SaveAsync()
        {
            var index = Settings.LoraAdapterModels.Count;
            if (IsUpdateMode)
            {
                index = Settings.LoraAdapterModels.IndexOf(_originalLoraModel);
                Settings.LoraAdapterModels.Remove(_originalLoraModel);
            }

            LoraModel.Key = CreateKey();
            LoraModel.Path = _selectedPath;
            if (LoraModel.Source == ModelSourceType.SingleFile)
                LoraModel.Path = Path.GetDirectoryName(_selectedPath);
            LoraModel.Weights = _selectedWeights;
            LoraModel.Triggers = Trigger.Count == 0 ? default : Trigger.ToArray();
            Settings.LoraAdapterModels.Insert(index, LoraModel);
            return base.SaveAsync();
        }


        protected override bool CanExecuteSave()
        {
            if (LoraModel == null)
                return false;

            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        protected override Task CancelAsync()
        {
            LoraModel = default;
            _originalLoraModel = null;
            return base.CancelAsync();
        }


        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        private Task AddTriggerAsync()
        {
            Trigger.Add(_selectedTrigger);
            SelectedTrigger = null;
            return Task.CompletedTask;
        }


        private bool CanAddTrigger()
        {
            return !Trigger.Contains(_selectedTrigger);
        }


        private Task RemoveTriggerAsync(string trigger)
        {
            Trigger.Remove(trigger);
            SelectedTrigger = null;
            return Task.CompletedTask;
        }


        private int GetNextModelId()
        {
            return Math.Max(Utils.FixedIdRange, Settings.LoraAdapterModels.Max(x => x.Id)) + 1;
        }


        private void Populate()
        {
            SelectedPath = LoraModel.Path;
            SelectedWeights = LoraModel.Weights;
            if (LoraModel.Source == ModelSourceType.SingleFile)
                SelectedPath = Path.Combine(SelectedPath, SelectedWeights);

            if (!LoraModel.Triggers.IsNullOrEmpty())
            {
                foreach (var trigger in LoraModel.Triggers)
                {
                    Trigger.Add(trigger);
                }
            }
        }

        private IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(LoraModel.Name))
                yield return "Name cannot be empty";
            if (string.IsNullOrWhiteSpace(_selectedPath))
                yield return "Path cannot be empty";
            if (string.IsNullOrWhiteSpace(_selectedWeights))
                yield return "Weights cannot be empty";
            if (string.IsNullOrWhiteSpace(LoraModel.Pipeline))
                yield return "Pipeline cannot be empty";
            if (!IsUpdateMode && Settings.LoraAdapterModels.Any(x => x.Pipeline == LoraModel.Pipeline && x.Name.Equals(LoraModel.Name, StringComparison.OrdinalIgnoreCase)))
                yield return $"Model with name '{LoraModel.Name}' already exists";

            if (!string.IsNullOrWhiteSpace(_selectedPath))
            {
                if (LoraModel.Source == ModelSourceType.Folder && !Directory.Exists(_selectedPath))
                    yield return "Model folder not found";
                else if (LoraModel.Source == ModelSourceType.SingleFile && !File.Exists(_selectedPath))
                    yield return "Model file not found";
                else if (LoraModel.Source == ModelSourceType.HuggingFace && !IsLoraAdapterValid(_selectedPath, _selectedWeights))
                    yield return "HuggingFace repository not found";
            }
        }


        private string CreateKey()
        {
            return $"{new string([.. LoraModel.Name.Where(char.IsLetterOrDigit)])}{LoraModel.Id}".ToLower();
        }


        private bool IsLoraAdapterValid(string loraAdapterPath, string loraWeightsPath)
        {
            return File.Exists(loraAdapterPath) || Utils.IsLoraAdapterInstalled(Settings.DirectoryCache, loraAdapterPath, loraWeightsPath) || Utils.IsHuggingFaceLink(loraAdapterPath);
        }


        private static LoraAdapterModel DeepClone(LoraAdapterModel loraModel, int modelId)
        {
            return new LoraAdapterModel
            {
                Id = modelId,
                Name = loraModel.Name,
                Key = loraModel.Key,
                Path = loraModel.Path,
                Pipeline = loraModel.Pipeline,
                Weights = loraModel.Weights,
                Triggers = loraModel.Triggers,
                Source = loraModel.Source,
                Backend = loraModel.Backend,
                IsGated = loraModel.IsGated,
                Link = loraModel.Link,
            };
        }
    }
}
