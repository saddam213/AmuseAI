// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

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
        private readonly string[] _stableDiffusionCppVersions = ["Vulkan", "CUDA", "ROCM"];
        private readonly string[] _audioCppVersions = ["Vulkan", "CUDA", "CPU"];
        private readonly string[] _llamaCppVersions = ["Vulkan", "CUDA", "ROCM"];
        private List<string> _hostVersions;
        private BackendType _backendType;
        private bool _isCreate;

        public EnvironmentModelDialog(Settings settings)
        {
            Settings = settings;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            AddVariableCommand = new AsyncRelayCommand(AddVariableAsync, CanAddVariable);
            RemoveVariableCommand = new AsyncRelayCommand<VariableModel>(RemoveVariableAsync);
            Variables = new ObservableCollection<VariableModel>();
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

        public BackendType BackendType
        {
            get { return _backendType; }
            set
            {
                var hostVersion = EnvironmentModel?.HostVersion;
                SetProperty(ref _backendType, value);
                if (_backendType == BackendType.StableDiffusionCpp)
                    HostVersions = [.. _stableDiffusionCppVersions];
                else if (_backendType == BackendType.AudioCpp)
                    HostVersions = [.. _audioCppVersions];
                else if (_backendType == BackendType.LlamaCpp)
                    HostVersions = [.. _llamaCppVersions];
                else
                    HostVersions = [];

                SetHostVersion(hostVersion);
            }
        }

        public bool IsFixedEnvironment
        {
            get { return _isFixedEnvironment; }
            set { SetProperty(ref _isFixedEnvironment, value); }
        }


        public List<string> HostVersions
        {
            get { return _hostVersions; }
            set { SetProperty(ref _hostVersions, value); }
        }

        public bool IsCreate
        {
            get { return _isCreate; }
            set { SetProperty(ref _isCreate, value); }
        }


        public Task<bool> AddAsync()
        {
            IsCreate = true;
            var environmentId = Settings.Environments.NextId(x => x.Id);
            EnvironmentModel = new EnvironmentModel
            {
                Id = environmentId,
                Name = AppDefault.MyEnvironment,
                Environment = "environment-new",
                Vendor = Settings.Vendors.FirstOrDefault(),
                Type = EnvironmentType.Vendor,
                Backend = BackendType.StableDiffusionCpp
            };
            Populate();
            return base.ShowDialogAsync();
        }


        public Task<bool> UpdateAsync(EnvironmentModel environmentModel)
        {
            var environmentId = environmentModel.Id;
            _originalEnvironmentModel = environmentModel;
            EnvironmentModel = environmentModel.DeepClone(environmentId);
            IsFixedEnvironment = environmentId <= Utils.FixedIdRange;
            Populate();
            return base.ShowDialogAsync();
        }


        public Task<bool> CopyAsync(EnvironmentModel environmentModel)
        {
            var environmentId = Settings.Environments.NextId(x => x.Id);
            EnvironmentModel = environmentModel.DeepClone(environmentId);
            Populate();
            EnvironmentModel.Name += " copy";
            EnvironmentModel.Environment += "-copy";
            return base.ShowDialogAsync();
        }


        public async Task<bool> ImportAsync(EnvironmentModel[] environmentImports)
        {
            var environmentId = Settings.Environments.NextId(x => x.Id);
            if (environmentImports.Length == 1)
            {
                var environmentImport = environmentImports[0];
                environmentImport.Id = environmentId++;
                EnvironmentModel = environmentImport;
                Populate();
                return await base.ShowDialogAsync();
            }
            else
            {
                var imported = 0;
                foreach (var environmentImport in environmentImports)
                {
                    if (!IsValidImport(environmentImport))
                        continue;

                    imported++;
                    environmentImport.Id = environmentId++;
                    Settings.Environments.Add(environmentImport);
                }

                await DialogService.ShowMessageAsync(AppDefault.ImportCompleteTitle, string.Format(AppDefault.ImportCompleteEnvironments, imported, environmentImports.Length));
                return true;
            }
        }


        private Task AddVariableAsync()
        {
            var existing = Variables.FirstOrDefault(x => x.Name == _currentVariable.Name);
            if (existing != null)
                Variables.Remove(existing);

            Variables.Add(new VariableModel
            {
                Name = _currentVariable.Name,
                Value = _currentVariable.Value ?? string.Empty,
            });

            CurrentVariable.Clear();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether the entered variable can be added, its name has no spaces or equals signs and the value may be empty.
        /// </summary>
        private bool CanAddVariable()
        {
            var name = _currentVariable.Name;
            if (string.IsNullOrWhiteSpace(name) || name.Any(c => char.IsWhiteSpace(c) || c == '='))
                return false;

            return !Variables.Any(x => x.Name == name && x.Value == (_currentVariable.Value ?? string.Empty));
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

            EnvironmentModel.Backend = _backendType;
            EnvironmentModel.Variables = Variables.Count > 0 ? Variables.ToDictionary(k => k.Name, k => k.Value) : null;
            EnvironmentModel.Requirements = Requirements.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

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


        private void Populate()
        {
            Variables.Clear();
            Requirements = EnvironmentModel.Requirements != null ? string.Join(Environment.NewLine, EnvironmentModel.Requirements) : string.Empty;
            if (!EnvironmentModel.Variables.IsNullOrEmpty())
            {
                foreach (var variable in EnvironmentModel.Variables)
                {
                    Variables.Add(new VariableModel { Name = variable.Key, Value = variable.Value });
                }
            }

            BackendType = EnvironmentModel.Backend;
        }


        /// <summary>
        /// Selects the host version again after the host versions change, the first one when the backend does not have it.
        /// </summary>
        /// <param name="hostVersion">The host version selected before the change.</param>
        private void SetHostVersion(string hostVersion)
        {
            if (EnvironmentModel is null || HostVersions.Count == 0)
                return;

            var selectedVersion = HostVersions.Contains(hostVersion) ? hostVersion : HostVersions[0];
            EnvironmentModel.HostVersion = null;
            EnvironmentModel.HostVersion = selectedVersion;
        }


        private void UpdateDefaults(EnvironmentModel environment)
        {
            if (environment.IsDefault)
            {
                var isDefault = environment.IsDefault;
                var environments = environment.Type switch
                {
                    EnvironmentType.Vendor => Settings.Environments.Where(x => x.Backend == environment.Backend && x.Type == EnvironmentType.Vendor && x.Vendor == environment.Vendor),
                    EnvironmentType.Device => Settings.Environments.Where(x => x.Backend == environment.Backend && x.Type == EnvironmentType.Device && x.Device == environment.Device),
                    _ => Settings.Environments.Where(x => x.Backend == environment.Backend && x.Type == EnvironmentType.Pipeline && x.Pipeline == environment.Pipeline),
                };

                foreach (var env in environments)
                    env.IsDefault = false;

                environment.IsDefault = isDefault;
            }
        }


        /// <summary>
        /// Determines whether an imported environment can be added as is, its folder name is set, valid and not used by another environment of the same backend and vendor.
        /// </summary>
        /// <param name="environment">The imported environment.</param>
        private bool IsValidImport(EnvironmentModel environment)
        {
            if (string.IsNullOrWhiteSpace(environment.Name) || string.IsNullOrWhiteSpace(environment.Environment))
                return false;

            if (!environment.Environment.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'))
                return false;

            return !Settings.Environments.Any(x => x.Backend == environment.Backend && x.Vendor == environment.Vendor && x.Environment.Equals(environment.Environment, StringComparison.OrdinalIgnoreCase));
        }


        private IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(Requirements))
                yield return AppErrors.ValidationRequirementsEmpty;
            if (EnvironmentModel.DownloadSize < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.DownloadSizeGB);
            if (string.IsNullOrWhiteSpace(EnvironmentModel.Name))
                yield return AppErrors.ValidationNameEmpty;
            if (string.IsNullOrWhiteSpace(EnvironmentModel.Environment))
                yield return AppErrors.ValidationEnvironmentEmpty;
            if (!string.IsNullOrWhiteSpace(EnvironmentModel.Environment))
            {
                if (!EnvironmentModel.Environment.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'))
                    yield return AppErrors.ValidationEnvironmentInvalidCharacters;
                if (Settings.Environments.Any(x => x != _originalEnvironmentModel && x.Backend == _backendType && x.Vendor == EnvironmentModel.Vendor && x.Environment.Equals(EnvironmentModel.Environment, StringComparison.OrdinalIgnoreCase)))
                    yield return string.Format(AppErrors.ValidationEnvironmentExists, EnvironmentModel.Environment);
            }

            if (EnvironmentModel.Type == EnvironmentType.Device)
            {
                if (EnvironmentModel.Device == 0)
                    yield return AppErrors.ValidationDeviceEmpty;
            }
            else if (EnvironmentModel.Type == EnvironmentType.Pipeline)
            {
                if (EnvironmentModel.Pipeline == null)
                    yield return AppErrors.ValidationPipelineEmpty;
            }
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
