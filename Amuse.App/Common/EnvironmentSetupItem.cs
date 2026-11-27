using Amuse.App.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TensorStack.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class EnvironmentSetupItem : BaseModel
    {
        private bool _isSelected;
        private EnvironmentModel _environment;
        private EnvironmentSetupStatus _status;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentSetupItem"/> class.
        /// </summary>
        /// <param name="backend">The backend.</param>
        /// <param name="vendor">The vendor.</param>
        /// <param name="environments">The environments that can be set up, in settings order.</param>
        /// <param name="isUpdateRequired">Is environment update required</param>
        /// <param name="isSelected">if set to <c>true</c> the item is selected for setup.</param>
        /// <param name="isInstalled">Determines whether an environment is installed.</param>
        public EnvironmentSetupItem(BackendType backend, VendorType vendor, IReadOnlyList<EnvironmentModel> environments, bool isUpdateRequired, bool isSelected, Func<EnvironmentModel, bool> isInstalled)
        {
            Backend = backend;
            Vendor = vendor;
            Environments = environments;
            IsUpdateRequired = isUpdateRequired;
            _isSelected = isSelected;

            // Selected, the environment in use first: the installed default, any installed, then the default
            var installed = environments.Where(isInstalled).ToList();
            _environment = installed.FirstOrDefault(x => x.IsDefault)
                ?? installed.FirstOrDefault()
                ?? environments.FirstOrDefault(x => x.IsDefault)
                ?? environments[0];

            // Options, in settings order with the environment in use selected
            Options = [.. environments.Select(x => new EnvironmentSetupOption(x, GetShortName(backend, x.Name), x == _environment, installed.Contains(x)))];
            foreach (var option in Options)
                option.PropertyChanged += OnOptionPropertyChanged;
        }

        public bool IsUpdateRequired { get; }
        public BackendType Backend { get; }
        public VendorType Vendor { get; }
        public IReadOnlyList<EnvironmentModel> Environments { get; }
        public IReadOnlyList<EnvironmentSetupOption> Options { get; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value); }
        }

        public EnvironmentModel Environment
        {
            get { return _environment; }
            private set { SetProperty(ref _environment, value); }
        }

        public EnvironmentSetupStatus Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value); }
        }


        /// <summary>
        /// Handles an option being selected, deselecting the others.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void OnOptionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(EnvironmentSetupOption.IsSelected) || sender is not EnvironmentSetupOption selectedOption || !selectedOption.IsSelected)
                return;

            foreach (var option in Options.Where(x => x != selectedOption))
                option.IsSelected = false;

            Environment = selectedOption.Environment;
        }


        /// <summary>
        /// Gets the environment name without the backend prefix, the backend is already shown on the row.
        /// </summary>
        /// <param name="backend">The backend.</param>
        /// <param name="name">The environment name.</param>
        private static string GetShortName(BackendType backend, string name)
        {
            var prefix = backend switch
            {
                BackendType.StableDiffusionCpp => "StableDiffusion.cpp ",
                BackendType.AudioCpp => "Audio.cpp ",
                BackendType.LlamaCpp => "Llama.cpp ",
                _ => null
            };

            if (prefix is null || name is null || name.Length <= prefix.Length || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return name;

            return name[prefix.Length..];
        }
    }


    /// <summary>
    /// A backend section of the app setup, with one item per vendor.
    /// </summary>
    public sealed class EnvironmentSetupGroup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentSetupGroup"/> class.
        /// </summary>
        /// <param name="backend">The backend.</param>
        /// <param name="items">The items, one per vendor.</param>
        public EnvironmentSetupGroup(BackendType backend, IReadOnlyList<EnvironmentSetupItem> items)
        {
            Backend = backend;
            Description = GetDescription(backend);
            Items = items;
        }

        public BackendType Backend { get; }
        public string Description { get; }
        public IReadOnlyList<EnvironmentSetupItem> Items { get; }


        /// <summary>
        /// Gets a plain description of what the backend is used for.
        /// </summary>
        /// <param name="backend">The backend.</param>
        private static string GetDescription(BackendType backend)
        {
            return backend switch
            {
                BackendType.StableDiffusionCpp => AppDefault.BackendDescriptionStableDiffusionCpp,
                BackendType.AudioCpp => AppDefault.BackendDescriptionAudioCpp,
                BackendType.LlamaCpp => AppDefault.BackendDescriptionLlamaCpp,
                _ => null
            };
        }
    }


    public sealed class EnvironmentSetupOption : BaseModel
    {
        private bool _isSelected;

        public EnvironmentSetupOption(EnvironmentModel environment, string displayName, bool isSelected, bool isInstalled)
        {
            DisplayName = displayName;
            Environment = environment;
            IsInstalled = isInstalled;
            _isSelected = isSelected;
        }
        public string DisplayName { get; }
        public EnvironmentModel Environment { get; }
        public bool IsInstalled { get; }
        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value); }
        }
    }


    public enum EnvironmentSetupStatus
    {
        Pending = 0,
        Installing = 1,
        Installed = 2,
        Failed = 3
    }
}
