namespace Amuse.App.Common
{
    public interface IDownloadModel
    {
        int Id { get; set; }
        string Name { get; set; }
        ModelStatusType Status { get; set; }
        string Link { get; set; }
        ModelMetadata Metadata { get; set; }

        void Initialize(Settings settings);
        void Delete(Settings settings);
        string GetDirectory(Settings settings);
    }
}
