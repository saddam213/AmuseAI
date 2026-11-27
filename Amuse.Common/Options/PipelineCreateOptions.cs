using System.Collections.Generic;
using TensorStack.Common;


namespace Amuse.Common
{
    public sealed record PipelineCreateOptions
    {
        public bool IsDebug { get; set; }
        public string Directory { get; set; }
        public string Environment { get; set; }
        public string HostVersion { get; set; }
        public string[] Requirements { get; set; }
        public Dictionary<string, string> Variables { get; set; }
        public BackendStatus Status { get; set; }
    }
}
