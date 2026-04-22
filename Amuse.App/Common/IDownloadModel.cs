namespace Amuse.App.Common
{
    public interface IDownloadModel
    {
        int Id { get; set; }
        string Name { get; set; }
        string Pipeline { get; set; }
        ModelStatusType Status { get; set; }
    }
}
