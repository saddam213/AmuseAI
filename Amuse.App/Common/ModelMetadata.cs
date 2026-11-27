using System.Text.Json.Serialization;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    /// <summary>
    /// Information about a model that is not needed to run it, who publishes it, its licence and where its files come from.
    /// </summary>
    public sealed class ModelMetadata : BaseModel
    {
        private string _publisher;
        private string _license;
        private string _licenseLink;
        private string _sourceLink;
        private PublisherModel _publisherModel;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Publisher
        {
            get { return _publisher; }
            set { SetProperty(ref _publisher, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string License
        {
            get { return _license; }
            set { SetProperty(ref _license, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LicenseLink
        {
            get { return _licenseLink; }
            set { SetProperty(ref _licenseLink, value); }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string SourceLink
        {
            get { return _sourceLink; }
            set { SetProperty(ref _sourceLink, value); }
        }

        [JsonIgnore]
        public PublisherModel PublisherModel
        {
            get { return _publisherModel; }
            private set { SetProperty(ref _publisherModel, value); }
        }


        /// <summary>
        /// Resolves the publisher from the settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public void Initialize(Settings settings)
        {
            PublisherModel = settings.GetPublisher(_publisher);
        }


        /// <summary>
        /// Creates a copy of the metadata.
        /// </summary>
        public ModelMetadata DeepClone()
        {
            return new ModelMetadata
            {
                Publisher = Publisher,
                License = License,
                LicenseLink = LicenseLink,
                SourceLink = SourceLink,
                PublisherModel = PublisherModel
            };
        }
    }
}
