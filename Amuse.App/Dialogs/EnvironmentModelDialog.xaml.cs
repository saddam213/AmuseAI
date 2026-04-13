// Copyright (c) TensorStack. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for EnvironmentModelDialog.xaml
    /// </summary>
    public partial class EnvironmentModelDialog : DialogControl
    {
        private EnvironmentModel _environmentModel;
        private EnvironmentModel _originalEnvironmentModel;
        private VariableModel _currentVariable;
        private VariableModel _selectedVariable;
        private string _requirements;
        private bool _isFixedEnvironment;

        public EnvironmentModelDialog(Settings settings)
        {
            Settings = settings;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            AddVariableCommand = new AsyncRelayCommand(AddVariableAsync, CanAddVariable);
            RemoveVariableCommand = new AsyncRelayCommand<VariableModel>(RemoveVariableAsync);
            Variables = new ObservableCollection<VariableModel>();
            Pipelines = new ObservableCollection<string>();
            Errors = new ObservableCollection<string>();
            CurrentVariable = new VariableModel();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public AsyncRelayCommand AddVariableCommand { get; }
        public AsyncRelayCommand<VariableModel> RemoveVariableCommand { get; }
        public ObservableCollection<VariableModel> Variables { get; }
        public ObservableCollection<string> Pipelines { get; }
        public bool IsUpdateMode => _originalEnvironmentModel is not null;

        public EnvironmentModel EnvironmentModel
        {
            get { return _environmentModel; }
            set { SetProperty(ref _environmentModel, value); }
        }

        public string Requirements
        {
            get { return _requirements; }
            set { SetProperty(ref _requirements, value); }
        }

        public VariableModel CurrentVariable
        {
            get { return _currentVariable; }
            set { SetProperty(ref _currentVariable, value); }
        }

        public VariableModel SelectedVariable
        {
            get { return _selectedVariable; }
            set
            {
                SetProperty(ref _selectedVariable, value);
                if (_selectedVariable is not null)
                {
                    CurrentVariable.Name = _selectedVariable.Name;
                    CurrentVariable.Value = _selectedVariable.Value;
                }
            }
        }

        public bool IsFixedEnvironment
        {
            get { return _isFixedEnvironment; }
            set { SetProperty(ref _isFixedEnvironment, value); }
        }


        public Task<bool> AddAsync()
        {
            var modelId = GetNextModelId();
            EnvironmentModel = new EnvironmentModel
            {
                Id = modelId,
                Name = "My Environment",
                Environment = "environment-new"
            };
            Populate();
            return base.ShowDialogAsync();
        }


        public Task<bool> UpdateAsync(EnvironmentModel environmentModel)
        {
            var modelId = environmentModel.Id;
            _originalEnvironmentModel = environmentModel;
            EnvironmentModel = DeepClone(environmentModel, modelId);
            IsFixedEnvironment = modelId <= Utils.FixedIdRange;
            Populate();
            return base.ShowDialogAsync();
        }


        public Task<bool> CopyAsync(EnvironmentModel environmentModel)
        {
            var modelId = GetNextModelId();
            EnvironmentModel = DeepClone(environmentModel, modelId);
            Populate();
            EnvironmentModel.Name += " copy";
            EnvironmentModel.Environment += "-copy";
            return base.ShowDialogAsync();
        }


        public Task<bool> ImportAsync(EnvironmentModel environmentModel)
        {
            environmentModel.Id = GetNextModelId();
            EnvironmentModel = environmentModel;
            Populate();
            return base.ShowDialogAsync();
        }


        private Task AddVariableAsync()
        {
            var existing = Variables.FirstOrDefault(x => x.Name == _currentVariable.Name);
            if (existing != null)
                Variables.Remove(existing);

            Variables.Add(new VariableModel
            {
                Name = _currentVariable.Name,
                Value = _currentVariable.Value,
            });

            CurrentVariable.Clear();
            return Task.CompletedTask;
        }


        private bool CanAddVariable()
        {
            return !string.IsNullOrEmpty(_currentVariable.Name)
                && !string.IsNullOrEmpty(_currentVariable.Value)
                && !Variables.Any(x => x.Name == _currentVariable.Name && x.Value == _currentVariable.Value);
        }


        private Task RemoveVariableAsync(VariableModel variable)
        {
            Variables.Remove(variable);
            CurrentVariable.Clear();
            return Task.CompletedTask;
        }


        protected override Task SaveAsync()
        {
            var index = Settings.Environments.Count;
            if (IsUpdateMode)
            {
                index = Settings.Environments.IndexOf(_originalEnvironmentModel);
                Settings.Environments.Remove(_originalEnvironmentModel);
            }

            if (EnvironmentModel.Type == EnvironmentType.Vendor)
            {
                EnvironmentModel.Device = 0;
                EnvironmentModel.Pipeline = null;
            }
            else if (EnvironmentModel.Type == EnvironmentType.Device)
            {
                EnvironmentModel.Pipeline = null;
            }
            else if (EnvironmentModel.Type == EnvironmentType.Pipeline)
            {
                EnvironmentModel.Device = 0;
            }

            EnvironmentModel.Variables = Variables.Count > 0 ? Variables.ToDictionary(k => k.Name, k => k.Value) : null;
            EnvironmentModel.Requirements = Requirements.Split(Environment.NewLine, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            // Defaults
            UpdateDefaults(EnvironmentModel);

            Settings.Environments.Insert(index, EnvironmentModel);
            return base.SaveAsync();
        }


        protected override bool CanExecuteSave()
        {
            if (EnvironmentModel == null)
                return false;

            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        protected override Task CancelAsync()
        {
            EnvironmentModel = default;
            _originalEnvironmentModel = null;
            return base.CancelAsync();
        }


        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        private int GetNextModelId()
        {
            return Math.Max(Utils.FixedIdRange, Settings.Environments.Max(x => x.Id)) + 1;
        }


        private void Populate()
        {
            Variables.Clear();
            Pipelines.Clear();
            Requirements = EnvironmentModel.Requirements != null ? string.Join(Environment.NewLine, EnvironmentModel.Requirements) : string.Empty;
            if (!EnvironmentModel.Variables.IsNullOrEmpty())
            {
                foreach (var variable in EnvironmentModel.Variables)
                {
                    Variables.Add(new VariableModel { Name = variable.Key, Value = variable.Value });
                }
            }

            foreach (var pipeline in Settings.GetPipelines())
            {
                Pipelines.Add(pipeline);
            }
        }


        private void UpdateDefaults(EnvironmentModel environment)
        {
            if (environment.IsDefault)
            {
                var isDefault = environment.IsDefault;
                var environments = environment.Type switch
                {
                    EnvironmentType.Vendor => Settings.Environments.Where(x => x.Type == EnvironmentType.Vendor && x.Vendor == environment.Vendor),
                    EnvironmentType.Device => Settings.Environments.Where(x => x.Type == EnvironmentType.Device && x.Device == environment.Device),
                    _ => Settings.Environments.Where(x => x.Type == EnvironmentType.Pipeline && x.Pipeline == environment.Pipeline),
                };

                foreach (var env in environments)
                    env.IsDefault = false;

                environment.IsDefault = isDefault;
            }
        }


        private IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(Requirements))
                yield return "Requirements cannot be empty";
            if (string.IsNullOrWhiteSpace(EnvironmentModel.Name))
                yield return "Name cannot be empty";
            if (string.IsNullOrWhiteSpace(EnvironmentModel.Environment))
                yield return "Environment cannot be empty";
            if (!string.IsNullOrWhiteSpace(EnvironmentModel.Environment))
            {
                if (!EnvironmentModel.Environment.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                    yield return "Environment can only contain letters, numbers, '_' & '-'";
                if (!IsUpdateMode && Settings.Environments.Any(x => x.Environment.Equals(EnvironmentModel.Environment, StringComparison.OrdinalIgnoreCase)))
                    yield return $"Environment with name '{EnvironmentModel.Environment}' already exists";
            }

            if (EnvironmentModel.Type == EnvironmentType.Pipeline)
            {
                if (string.IsNullOrEmpty(EnvironmentModel.Pipeline))
                    yield return "Pipeline cannot be empty";
            }
        }


        private static EnvironmentModel DeepClone(EnvironmentModel environmentModel, int modelId)
        {
            return new EnvironmentModel
            {
                Id = modelId,
                Name = environmentModel.Name,
                IsDefault = environmentModel.IsDefault,
                Environment = environmentModel.Environment,
                Vendor = environmentModel.Vendor,
                Variables = environmentModel.Variables?.ToDictionary() ?? new Dictionary<string, string>(),
                Requirements = environmentModel.Requirements.ToArray(),
                Pipeline = environmentModel.Pipeline,
                Device = environmentModel.Device,
                Type = environmentModel.Type,
            };
        }


        public class VariableModel : BaseModel
        {
            private string _name;
            private string _value;

            public string Name
            {
                get { return _name; }
                set { SetProperty(ref _name, value); }
            }

            public string Value
            {
                get { return _value; }
                set { SetProperty(ref _value, value); }
            }

            public void Clear()
            {
                Name = null;
                Value = null;
            }
        }
    }
}
