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
        public string ChannelTensor { get; init; }
        public string ChannelStream { get; init; }
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
                    ChannelProgress = "AmuseHost.OnnxRuntime.Progress",
                    ChannelTensor = "AmuseHost.OnnxRuntime.Tensor",
                    ChannelStream = "AmuseHost.OnnxRuntime.Stream"
                }
            },
            {
                ServerType.OnnxRuntimeUpscale,  new ServerConfig
                {
                    Name = "OnnxRuntimeUpscale",
                    Arguments = [nameof(ServerType.OnnxRuntimeUpscale)],
                    Executable = "AmuseHost.OnnxRuntime.exe",
                    ChannelCommand = "AmuseHost.OnnxRuntimeUpscale.Command",
                    ChannelPipeName = "AmuseHost.OnnxRuntimeUpscale.PipeName",
                    ChannelProgress = "AmuseHost.OnnxRuntimeUpscale.Progress",
                    ChannelTensor = "AmuseHost.OnnxRuntimeUpscale.Tensor",
                    ChannelStream = "AmuseHost.OnnxRuntimeUpscale.Stream"
                }
            },
            {
                ServerType.OnnxRuntimeExtract,  new ServerConfig
                {
                    Name = "OnnxRuntimeExtract",
                    Arguments = [nameof(ServerType.OnnxRuntimeExtract)],
                    Executable = "AmuseHost.OnnxRuntime.exe",
                    ChannelCommand = "AmuseHost.OnnxRuntimeExtract.Command",
                    ChannelPipeName = "AmuseHost.OnnxRuntimeExtract.PipeName",
                    ChannelProgress = "AmuseHost.OnnxRuntimeExtract.Progress",
                    ChannelTensor = "AmuseHost.OnnxRuntimeExtract.Tensor",
                    ChannelStream = "AmuseHost.OnnxRuntimeExtract.Stream"
                }
            },
            {
                ServerType.OnnxRuntimeInterpolate,  new ServerConfig
                {
                    Name = "OnnxRuntimeInterpolate",
                    Arguments = [nameof(ServerType.OnnxRuntimeInterpolate)],
                    Executable = "AmuseHost.OnnxRuntime.exe",
                    ChannelCommand = "AmuseHost.OnnxRuntimeInterpolate.Command",
                    ChannelPipeName = "AmuseHost.OnnxRuntimeInterpolate.PipeName",
                    ChannelProgress = "AmuseHost.OnnxRuntimeInterpolate.Progress",
                    ChannelTensor = "AmuseHost.OnnxRuntimeInterpolate.Tensor",
                    ChannelStream = "AmuseHost.OnnxRuntimeInterpolate.Stream"
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
                    ChannelProgress = "AmuseHost.StableDiffusionCpp.Progress",
                    ChannelTensor = "AmuseHost.StableDiffusionCpp.Tensor",
                    ChannelStream = "AmuseHost.StableDiffusionCpp.Stream"
                }
            },
            {
                ServerType.StableDiffusionCppUpscale,  new ServerConfig
                {
                    Name = "StableDiffusionCppUpscale",
                    Arguments = [nameof(ServerType.StableDiffusionCppUpscale)],
                    Executable = "AmuseHost.StableDiffusionCpp.exe",
                    ChannelCommand = "AmuseHost.StableDiffusionCppUpscale.Command",
                    ChannelPipeName = "AmuseHost.StableDiffusionCppUpscale.PipeName",
                    ChannelProgress = "AmuseHost.StableDiffusionCppUpscale.Progress",
                    ChannelTensor = "AmuseHost.StableDiffusionCppUpscale.Tensor",
                    ChannelStream = "AmuseHost.StableDiffusionCppUpscale.Stream"
                }
            },
            {
                ServerType.AudioCpp,  new ServerConfig
                {
                    Name = "AudioCpp",
                    Arguments = [nameof(ServerType.AudioCpp)],
                    Executable = "AmuseHost.AudioCpp.exe",
                    ChannelCommand = "AmuseHost.AudioCpp.Command",
                    ChannelPipeName = "AmuseHost.AudioCpp.PipeName",
                    ChannelProgress = "AmuseHost.AudioCpp.Progress",
                    ChannelTensor = "AmuseHost.AudioCpp.Tensor",
                    ChannelStream = "AmuseHost.AudioCpp.Stream"
                }
            },
            {
                ServerType.LlamaCpp,  new ServerConfig
                {
                    Name = "LlamaCpp",
                    Arguments = [nameof(ServerType.LlamaCpp)],
                    Executable = "AmuseHost.LlamaCpp.exe",
                    ChannelCommand = "AmuseHost.LlamaCpp.Command",
                    ChannelPipeName = "AmuseHost.LlamaCpp.PipeName",
                    ChannelProgress = "AmuseHost.LlamaCpp.Progress",
                    ChannelTensor = "AmuseHost.LlamaCpp.Tensor",
                    ChannelStream = "AmuseHost.LlamaCpp.Stream"
                }
            }
        };
    }
}
