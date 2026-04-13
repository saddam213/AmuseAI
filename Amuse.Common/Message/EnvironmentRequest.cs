using Amuse.Common.Config;
using TensorStack.Python.Common;
using TensorStack.Python.Config;

namespace Amuse.Common.Message
{

    public class EnvironmentRequest
    {
        public EnvironmentConfig Config { get; set; }
        public EnvironmentMode Mode { get; set; }
    }
}
