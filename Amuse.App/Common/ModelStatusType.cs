namespace Amuse.App.Common
{
    public enum ModelStatusType
    {
        Pending = 0,
        Unknown = 1,
        Verifying = 10,
        Downloading = 11,
        DownloadQueue = 12,
        DownloadFailed = 13,
        Installed = 20,
    }
}
