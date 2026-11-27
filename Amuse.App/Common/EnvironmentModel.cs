using Amuse.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TensorStack.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public sealed class EnvironmentModel : BaseModel
    {
        private string _name;
        private string _description;
        private EnvironmentType _type;
        private VendorType _vendor;
        private int _device;
        private PipelineType? _pipeline;
        private bool _isDefault;
        private string _environment;
        private string _hostVersion;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Id { get; set; }
        public int Version { get; set; }
        public BackendStatus Status { get; set; }
        public BackendType Backend { get; set; }

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        public string Description
        {
            get { return _description; }
            set { SetProperty(ref _description, value); }
        }

        /// <summary>
        /// Gets or sets the approximate download size in GB.
        /// </summary>
        public float DownloadSize { get; set; }

        /// <summary>
        /// Gets the download size for display, in MB below 1 GB, 0 MB when unknown.
        /// </summary>
        [JsonIgnore]
        public string DownloadSizeText => DownloadSize < 1
            ? $"{DownloadSize * 1024:N0} MB"
            : $"{DownloadSize:0.0} GB";

        public EnvironmentType Type
        {
            get { return _type; }
            set { SetProperty(ref _type, value); }
        }

        public VendorType Vendor
        {
            get { return _vendor; }
            set { SetProperty(ref _vendor, value); }
        }

        public int Device
        {
            get { return _device; }
            set { SetProperty(ref _device, value); }
        }

        public PipelineType? Pipeline
        {
            get { return _pipeline; }
            set { SetProperty(ref _pipeline, value); }
        }

        public bool IsDefault
        {
            get { return _isDefault; }
            set { SetProperty(ref _isDefault, value); }
        }

        public string Environment
        {
            get { return _environment; }
            set { SetProperty(ref _environment, value); }
        }

        public string HostVersion
        {
            get { return _hostVersion; }
            set { SetProperty(ref _hostVersion, value); }
        }

        public string[] Requirements { get; set; }
        public Dictionary<string, string> Variables { get; set; }


        public EnvironmentModel DeepClone(int id)
        {
            return new EnvironmentModel
            {
                Id = id,
                Name = Name,
                Description = Description,
                DownloadSize = DownloadSize,
                IsDefault = IsDefault,
                Environment = Environment,
                HostVersion = HostVersion,
                Vendor = Vendor,
                Variables = Variables?.ToDictionary() ?? new Dictionary<string, string>(),
                Requirements = Requirements.ToArray(),
                Pipeline = Pipeline,
                Device = Device,
                Type = Type,
                Backend = Backend,
                Version = Version,
            };
        }


        /// <summary>
        /// Determines whether another environment shares this environment's default, only one environment per group is the default.
        /// Groups are the same backend, vendor and type, with the same device or pipeline, the way an environment is selected for a pipeline.
        /// </summary>
        /// <param name="other">The other environment.</param>
        public bool IsSameDefaultGroup(EnvironmentModel other)
        {
            if (other.Backend != Backend || other.Vendor != Vendor || other.Type != Type)
                return false;

            return Type switch
            {
                EnvironmentType.Device => other.Device == Device,
                EnvironmentType.Pipeline => other.Pipeline == Pipeline,
                _ => true
            };
        }
    }
}
