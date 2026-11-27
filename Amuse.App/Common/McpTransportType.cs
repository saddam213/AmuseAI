using Amuse.App.Resources;
using System.ComponentModel.DataAnnotations;

namespace Amuse.App.Common
{
    /// <summary>
    /// How an MCP server is reached.
    /// </summary>
    public enum McpTransportType
    {
        /// <summary>
        /// A local command, started as a child process and spoken to over standard input and output.
        /// </summary>
        [Display(Name = "McpTransportType0", Description = "McpTransportType0_Description", ResourceType = typeof(AppEnums))]
        Stdio = 0,

        /// <summary>
        /// A remote server, reached by URL over Streamable HTTP or SSE.
        /// </summary>
        [Display(Name = "McpTransportType1", Description = "McpTransportType1_Description", ResourceType = typeof(AppEnums))]
        Http = 1
    }
}
