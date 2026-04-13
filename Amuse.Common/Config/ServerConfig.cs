namespace Amuse.Common.Config
{
    public class ChannelConfig
    {
        public int ChunkSize { get; } = 32 * 1024 * 1024; // 32 MB
        public string Name { get; init; }
        public string Executable { get; init; }
        public string[] Arguments { get; set; }
        public string ChannelCommand { get; init; }
        public string ChannelPipeName { get; init; }
        public string ChannelProgress { get; init; }


        public readonly static ChannelConfig PipelineConfig = new ChannelConfig
        {
            Name = "AmuseHost",
            Executable = "AmuseHost.exe",
            ChannelCommand = "AmuseHost.Command",
            ChannelPipeName = "AmuseHost.PipeName",
            ChannelProgress = "AmuseHost.Progress"
        };


        public readonly static ChannelConfig DownloadConfig = new ChannelConfig
        {
            Name = "AmuseDownload",
            Executable = "AmuseHost.exe",
            Arguments = ["download"],
            ChannelCommand = "AmuseDownload.Command",
            ChannelPipeName = "AmuseDownload.PipeName",
            ChannelProgress = "AmuseDownload.Progress"
        };
    }
}
