namespace Amuse.App.Common
{
    /// <summary>
    /// The connection state of an MCP server.
    /// </summary>
    public enum McpServerStatus
    {
        /// <summary>
        /// The server is switched off.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// The server is switched on but not connected yet.
        /// </summary>
        Disconnected = 1,

        /// <summary>
        /// The server is starting or connecting.
        /// </summary>
        Connecting = 2,

        /// <summary>
        /// The server is connected and its tools are listed.
        /// </summary>
        Connected = 3,

        /// <summary>
        /// The server could not be started or connected.
        /// </summary>
        Failed = 4
    }
}
