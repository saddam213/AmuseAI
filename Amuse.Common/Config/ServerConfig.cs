using System.Collections.Generic;

namespace Amuse.Common.Config
{
    public sealed record ServerConfig
    {
        public int ChunkSize { get; } = 32 * 1024 * 1024; // 32 MB
        public string Name { get; init; }
        public string Executable { get; init; }
        public string[] Arguments { get; set; }
        public string ChannelCommand { get; init; }
        public string ChannelPipeName { get; init; }
        public string ChannelProgress { get; init; }
        public string DirectoryBase { get; init; }

        public static ServerConfig GetConfig(ServerType serverType, string directoryBase = null)
        {
            return _configurations[serverType] with
            {
                DirectoryBase = directoryBase,
            };
        }


        private readonly static Dictionary<ServerType, ServerConfig> _configurations = new Dictionary<ServerType, ServerConfig>
        {
            {
                ServerType.OnnxRuntime,  new ServerConfig
                {
                    Name = "OnnxRuntime",
                    Arguments = [nameof(ServerType.OnnxRuntime)],
                    Executable = "AmuseHost.OnnxRuntime.exe",
                    ChannelCommand = "AmuseHost.OnnxRuntime.Command",
                    ChannelPipeName = "AmuseHost.OnnxRuntime.PipeName",
                    ChannelProgress = "AmuseHost.OnnxRuntime.Progress"
                }
            },
            {
                ServerType.HuggingFace,  new ServerConfig
                {
                    Name = "HuggingFace",
                    Arguments = [nameof(ServerType.HuggingFace)],
                    Executable = "AmuseHost.HuggingFace.exe",
                    ChannelCommand = "AmuseHost.HuggingFace.Command",
                    ChannelPipeName = "AmuseHost.HuggingFace.PipeName",
                    ChannelProgress = "AmuseHost.HuggingFace.Progress"
                }
            },
            {
                ServerType.StableDiffusionCpp,  new ServerConfig
                {
                    Name = "StableDiffusionCpp",
                    Arguments = [nameof(ServerType.StableDiffusionCpp)],
                    Executable = "AmuseHost.StableDiffusionCpp.exe",
                    ChannelCommand = "AmuseHost.StableDiffusionCpp.Command",
                    ChannelPipeName = "AmuseHost.StableDiffusionCpp.PipeName",
                    ChannelProgress = "AmuseHost.StableDiffusionCpp.Progress"
                }
            }
        };
    }
}
