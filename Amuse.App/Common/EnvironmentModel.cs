using System.Collections.Generic;
using System.Text.Json.Serialization;
using TensorStack.Common;
using TensorStack.Python.Common;

namespace Amuse.App.Common
{
    public class EnvironmentModel
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Id { get; set; }
        public string Name { get; set; }
        public EnvironmentType Type { get; set; }
        public VendorType Vendor { get; set; }
        public int Version { get; set; }
        public EnvironmentMode Status { get; set; }
        public int Device { get; set; }
        public string Pipeline { get; set; }
        public bool IsDefault { get; set; }
        public string Environment { get; set; }
        public string[] Requirements { get; set; }
        public Dictionary<string, string> Variables { get; set; }
      
    }
}
