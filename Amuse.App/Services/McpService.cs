using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.App.Tools;
using Amuse.Common;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Amuse.App.Services
{
    /// <summary>
    /// Connects to the MCP servers in the settings and provides their tools to the language models.
    /// </summary>
    public sealed class McpService : ServiceBase, IMcpService, IDisposable
    {
        private static readonly TimeSpan _initializeTimeout = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(2);
        private static readonly string[] _executableExtensions = [".exe", ".com", ".cmd", ".bat"];
        private readonly Settings _settings;
        private readonly ILogger<McpService> _logger;
        private readonly ProcessHandler _processHandler;
        private readonly Dictionary<McpServerModel, McpConnection> _connections;
        private readonly object _serversLock = new object();
        private IReadOnlyList<McpToolCall> _tools = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="McpService"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="logger">The logger.</param>
        public McpService(Settings settings, ILogger<McpService> logger)
        {
            _settings = settings;
            _logger = logger;
            _processHandler = new ProcessHandler();
            _connections = new Dictionary<McpServerModel, McpConnection>();
            _settings.McpServers ??= [];
            Servers = new ObservableCollection<McpServerModel>();
            foreach (var config in _settings.McpServers)
                AddServer(config);
        }

        /// <summary>
        /// Occurs when the tools of the connected servers change.
        /// </summary>
        public event EventHandler ToolsChanged;

        public ObservableCollection<McpServerModel> Servers { get; }


        /// <summary>
        /// Gets the tools of the connected servers.
        /// </summary>
        public IReadOnlyList<McpToolCall> GetTools()
        {
            return _tools;
        }


        /// <summary>
        /// Connects the enabled servers that are not connected yet, a server that fails is skipped.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            var servers = Servers
                .Where(x => x.Config.IsEnabled && x.Status == McpServerStatus.Disconnected)
                .ToArray();
            if (servers.Length == 0)
                return;

            await Task.WhenAll(servers.Select(x => ConnectServerAsync(x, cancellationToken)));
        }


        /// <summary>
        /// Disconnects every server and stops the server processes.
        /// </summary>
        public async Task DisconnectAsync()
        {
            foreach (var server in Servers.ToArray())
                await DisconnectServerAsync(server);
        }


        /// <summary>
        /// Adds a server to the settings and connects it when enabled.
        /// </summary>
        /// <param name="config">The server settings.</param>
        public async Task<McpServerModel> AddAsync(McpServerConfig config)
        {
            _settings.McpServers.Add(config);
            var server = AddServer(config);
            await _settings.SaveAsync();
            await ConnectServerAsync(server);
            return server;
        }


        /// <summary>
        /// Replaces the settings of a server and connects it again.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <param name="config">The new server settings.</param>
        public async Task UpdateAsync(McpServerModel server, McpServerConfig config)
        {
            await DisconnectServerAsync(server);
            var index = _settings.McpServers.IndexOf(server.Config);
            if (index >= 0)
                _settings.McpServers[index] = config;
            else
                _settings.McpServers.Add(config);

            server.Config = config;
            server.Status = config.IsEnabled ? McpServerStatus.Disconnected : McpServerStatus.Disabled;
            await _settings.SaveAsync();
            await ConnectServerAsync(server);
        }


        /// <summary>
        /// Disconnects a server and removes it from the settings.
        /// </summary>
        /// <param name="server">The server.</param>
        public async Task RemoveAsync(McpServerModel server)
        {
            await DisconnectServerAsync(server);
            _settings.McpServers.Remove(server.Config);
            server.IsEnabledChanged -= OnServerIsEnabledChanged;
            lock (_serversLock)
            {
                _connections.Remove(server);
                Servers.Remove(server);
            }
            UpdateTools();
            await _settings.SaveAsync();
        }


        /// <summary>
        /// Disconnects a server and connects it again when enabled.
        /// </summary>
        /// <param name="server">The server.</param>
        public async Task ReconnectAsync(McpServerModel server)
        {
            await DisconnectServerAsync(server);
            server.Status = server.Config.IsEnabled ? McpServerStatus.Disconnected : McpServerStatus.Disabled;
            await ConnectServerAsync(server);
        }


        /// <summary>
        /// Connects to a server once and lists its tools, the server is not kept connected.
        /// </summary>
        /// <param name="config">The server settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task<McpTestResult> TestAsync(McpServerConfig config, CancellationToken cancellationToken = default)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var connection = new McpConnection();
            try
            {
                _logger.LogInformation("[McpService] [Test] Testing server, Server: {Server}, Transport: {Transport}", config.Name, config.Transport);
                await OpenAsync(connection, config, cancellationToken);
                var tools = await connection.Client.ListToolsAsync(cancellationToken: cancellationToken);
                _logger.LogInformation("[McpService] [Test] Testing server complete, Server: {Server}, Tools: {Tools}, Elapsed: {Elapsed:c}", config.Name, tools.Count, Stopwatch.GetElapsedTime(timestamp));
                return new McpTestResult(true, string.Format(AppDefault.McpStatusConnected, tools.Count), [.. tools.Select(x => new McpToolInfo(x.Name, x.Description))]);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("[McpService] [Test] Testing server cancelled, Server: {Server}, Elapsed: {Elapsed:c}", config.Name, Stopwatch.GetElapsedTime(timestamp));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[McpService] [Test] Testing server failed, Server: {Server}, Elapsed: {Elapsed:c}", config.Name, Stopwatch.GetElapsedTime(timestamp));
                return new McpTestResult(false, GetErrorMessage(ex, connection), []);
            }
            finally
            {
                await CloseAsync(connection);
            }
        }


        /// <summary>
        /// Releases the job that holds the server processes, any process still running is stopped.
        /// </summary>
        public void Dispose()
        {
            _processHandler.Dispose();
        }


        /// <summary>
        /// Adds a server to the list.
        /// </summary>
        /// <param name="config">The server settings.</param>
        private McpServerModel AddServer(McpServerConfig config)
        {
            var server = new McpServerModel(config);
            server.IsEnabledChanged += OnServerIsEnabledChanged;
            lock (_serversLock)
            {
                _connections.Add(server, new McpConnection());
                Servers.Add(server);
            }
            return server;
        }


        /// <summary>
        /// Called when a server is switched on or off, the setting is saved and the server is connected or disconnected.
        /// </summary>
        /// <param name="sender">The server.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private async void OnServerIsEnabledChanged(object sender, EventArgs e)
        {
            var server = (McpServerModel)sender;
            try
            {
                _logger.LogInformation("[McpService] [SetEnabled] Server switched, Server: {Server}, IsEnabled: {IsEnabled}", server.Config.Name, server.IsEnabled);
                await _settings.SaveAsync();
                await ReconnectAsync(server);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[McpService] [SetEnabled] An exception occurred switching the server, Server: {Server}", server.Config.Name);
            }
        }


        /// <summary>
        /// Connects a server and lists its tools, a failure is recorded on the server and not thrown.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task ConnectServerAsync(McpServerModel server, CancellationToken cancellationToken = default)
        {
            if (!_connections.TryGetValue(server, out var connection))
                return;

            var timestamp = Stopwatch.GetTimestamp();
            var connectSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await connection.Gate.WaitAsync(cancellationToken);
            try
            {
                if (!server.Config.IsEnabled || server.Status == McpServerStatus.Connected)
                    return;

                _logger.LogInformation("[McpService] [Connect] Connecting server, Server: {Server}, Transport: {Transport}", server.Config.Name, server.Config.Transport);
                connection.ConnectSource = connectSource;
                server.Message = null;
                server.Status = McpServerStatus.Connecting;
                connection.Key = GetServerKey(server);
                await OpenAsync(connection, server.Config, connectSource.Token);
                var client = connection.Client;
                client.RegisterNotificationHandler(NotificationMethods.ToolListChangedNotification, (notification, token) => OnToolListChanged(server, connection, client));
                server.Tools = await GetToolsAsync(connection, server.Config, connectSource.Token);
                server.Status = McpServerStatus.Connected;
                _ = WatchConnectionAsync(server, connection, connection.Client);
                _logger.LogInformation("[McpService] [Connect] Connecting server complete, Server: {Server}, Tools: {Tools}, Elapsed: {Elapsed:c}", server.Config.Name, server.Tools.Count, Stopwatch.GetElapsedTime(timestamp));
            }
            catch (OperationCanceledException) when (connectSource.IsCancellationRequested)
            {
                _logger.LogInformation("[McpService] [Connect] Connecting server cancelled, Server: {Server}, Elapsed: {Elapsed:c}", server.Config.Name, Stopwatch.GetElapsedTime(timestamp));
                await CloseAsync(connection);
                server.Tools = [];
                server.Status = McpServerStatus.Disconnected;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[McpService] [Connect] Connecting server failed, Server: {Server}, Elapsed: {Elapsed:c}", server.Config.Name, Stopwatch.GetElapsedTime(timestamp));
                server.Message = GetErrorMessage(ex, connection);
                await CloseAsync(connection);
                server.Tools = [];
                server.Status = McpServerStatus.Failed;
            }
            finally
            {
                connection.ConnectSource = null;
                connection.Gate.Release();
                connectSource.Dispose();
            }
            UpdateTools();
        }


        /// <summary>
        /// Disconnects a server and stops its process.
        /// </summary>
        /// <param name="server">The server.</param>
        private async Task DisconnectServerAsync(McpServerModel server)
        {
            if (!_connections.TryGetValue(server, out var connection))
                return;

            CancelConnect(connection);
            await connection.Gate.WaitAsync();
            try
            {
                if (connection.Client != null || connection.Process != null)
                    _logger.LogInformation("[McpService] [Disconnect] Disconnecting server, Server: {Server}", server.Config.Name);

                await CloseAsync(connection);
                server.Tools = [];
                server.Message = null;
                server.Status = server.Config.IsEnabled ? McpServerStatus.Disconnected : McpServerStatus.Disabled;
            }
            finally
            {
                connection.Gate.Release();
            }
            UpdateTools();
        }


        /// <summary>
        /// Waits for a connection to end, a server that stops by itself is marked as not connected so it is connected again later.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <param name="connection">The connection.</param>
        /// <param name="client">The client of the connection.</param>
        private async Task WatchConnectionAsync(McpServerModel server, McpConnection connection, McpClient client)
        {
            try
            {
                await client.Completion;
            }
            catch (Exception)
            {
                // The connection ended with an error, handled the same as a clean end
            }

            if (!ReferenceEquals(connection.Client, client))
                return;

            await connection.Gate.WaitAsync();
            try
            {
                if (!ReferenceEquals(connection.Client, client))
                    return;

                _logger.LogWarning("[McpService] [Watch] Server disconnected, Server: {Server}, Error: {Error}", server.Config.Name, connection.GetErrorLine());
                await CloseAsync(connection);
                server.Tools = [];
                server.Status = server.Config.IsEnabled ? McpServerStatus.Disconnected : McpServerStatus.Disabled;
            }
            finally
            {
                connection.Gate.Release();
            }
            UpdateTools();
        }


        /// <summary>
        /// Called when a server reports that its tools changed, the tools are listed again without holding up the server messages.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <param name="connection">The connection.</param>
        /// <param name="client">The client of the connection.</param>
        private ValueTask OnToolListChanged(McpServerModel server, McpConnection connection, McpClient client)
        {
            _ = RefreshToolsAsync(server, connection, client);
            return ValueTask.CompletedTask;
        }


        /// <summary>
        /// Lists the tools of a connected server again.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <param name="connection">The connection.</param>
        /// <param name="client">The client of the connection.</param>
        private async Task RefreshToolsAsync(McpServerModel server, McpConnection connection, McpClient client)
        {
            await connection.Gate.WaitAsync();
            try
            {
                if (!ReferenceEquals(connection.Client, client) || server.Status != McpServerStatus.Connected)
                    return;

                server.Tools = await GetToolsAsync(connection, server.Config, CancellationToken.None);
                _logger.LogInformation("[McpService] [Refresh] Server tools changed, Server: {Server}, Tools: {Tools}", server.Config.Name, server.Tools.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[McpService] [Refresh] Failed to list the server tools, Server: {Server}", server.Config.Name);
            }
            finally
            {
                connection.Gate.Release();
            }
            UpdateTools();
        }


        /// <summary>
        /// Opens the connection to a server.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="config">The server settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task OpenAsync(McpConnection connection, McpServerConfig config, CancellationToken cancellationToken)
        {
            var options = new McpClientOptions
            {
                ClientInfo = new Implementation { Name = App.AppName, Version = App.AppVersion },
                InitializationTimeout = _initializeTimeout
            };
            var transport = config.Transport == McpTransportType.Http
                ? CreateHttpTransport(config)
                : CreateStdioTransport(connection, config);
            connection.Client = await McpClient.CreateAsync(transport, options, cancellationToken: cancellationToken);
        }


        /// <summary>
        /// Creates the transport of a remote server.
        /// </summary>
        /// <param name="config">The server settings.</param>
        private static IClientTransport CreateHttpTransport(McpServerConfig config)
        {
            if (!Uri.TryCreate(config.Url, UriKind.Absolute, out var endpoint) || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
                throw new Exception(string.Format(AppErrors.McpUrlInvalid, config.Url));

            var options = new HttpClientTransportOptions
            {
                Name = config.Name,
                Endpoint = endpoint,
                AdditionalHeaders = new Dictionary<string, string>(config.Headers ?? [])
            };
            return new HttpClientTransport(options);
        }


        /// <summary>
        /// Starts the process of a local server and creates the transport over its standard input and output.
        /// The process is added to a job so it is stopped with the application, even when the application is killed.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="config">The server settings.</param>
        private IClientTransport CreateStdioTransport(McpConnection connection, McpServerConfig config)
        {
            var commandPath = FindCommand(config.Command);
            if (commandPath == null)
                throw new Exception(GetCommandNotFoundMessage(config.Command));

            var process = new Process { StartInfo = CreateStartInfo(commandPath, config) };
            process.ErrorDataReceived += (s, e) => OnErrorLine(connection, config.Name, e.Data);
            process.Start();
            connection.Process = process;
            _processHandler.AddProcess(process);
            process.BeginErrorReadLine();
            return new StreamClientTransport(process.StandardInput.BaseStream, process.StandardOutput.BaseStream);
        }


        /// <summary>
        /// Creates the start info of a server process, batch files such as npx are run through the command interpreter.
        /// </summary>
        /// <param name="commandPath">The full path of the command.</param>
        /// <param name="config">The server settings.</param>
        private static ProcessStartInfo CreateStartInfo(string commandPath, McpServerConfig config)
        {
            var workingDirectory = Directory.Exists(config.WorkingDirectory)
                ? config.WorkingDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = workingDirectory
            };

            var arguments = config.Arguments ?? [];
            var extension = Path.GetExtension(commandPath);
            if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
            {
                var commandLine = string.Join(' ', arguments.Prepend(commandPath).Select(QuoteArgument));
                startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                startInfo.Arguments = $"/d /s /c \"{commandLine}\"";
            }
            else
            {
                startInfo.FileName = commandPath;
                foreach (var argument in arguments)
                    startInfo.ArgumentList.Add(argument);
            }

            foreach (var variable in config.Environment ?? [])
                startInfo.Environment[variable.Key] = variable.Value;

            return startInfo;
        }


        /// <summary>
        /// Lists the tools of a connected server, limited to the included tools when any are set.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="config">The server settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task<IReadOnlyList<McpToolCall>> GetToolsAsync(McpConnection connection, McpServerConfig config, CancellationToken cancellationToken)
        {
            var tools = new List<McpToolCall>();
            var includeTools = config.IncludeTools ?? [];
            foreach (var clientTool in await connection.Client.ListToolsAsync(cancellationToken: cancellationToken))
            {
                if (includeTools.Count > 0 && !includeTools.Contains(clientTool.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                var tool = new McpToolCall(config.Name, connection.Key, clientTool);
                if (tools.Any(x => x.Name == tool.Name))
                {
                    _logger.LogWarning("[McpService] [Connect] Skipped tool with a duplicate name, Server: {Server}, Tool: {Tool}", config.Name, clientTool.Name);
                    continue;
                }
                tools.Add(tool);
            }
            return tools;
        }


        /// <summary>
        /// Closes a connection, the server process is stopped if it does not exit by itself.
        /// </summary>
        /// <param name="connection">The connection.</param>
        private async Task CloseAsync(McpConnection connection)
        {
            var client = connection.Client;
            var process = connection.Process;
            connection.Client = null;
            connection.Process = null;
            if (client != null)
            {
                try
                {
                    await client.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[McpService] [Close] Failed to close the client.");
                }
            }

            if (process != null)
            {
                try
                {
                    using (var timeoutSource = new CancellationTokenSource(_shutdownTimeout))
                    {
                        process.StandardInput.Close();
                        await process.WaitForExitAsync(timeoutSource.Token);
                    }
                }
                catch (Exception)
                {
                    // The process did not exit in time or has already exited, it is killed below
                }

                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[McpService] [Close] Failed to stop the server process.");
                }
                process.Dispose();
            }
        }


        /// <summary>
        /// Cancels the connect of a server that is still connecting.
        /// </summary>
        /// <param name="connection">The connection.</param>
        private static void CancelConnect(McpConnection connection)
        {
            try
            {
                connection.ConnectSource?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The connect finished while it was being cancelled
            }
        }


        /// <summary>
        /// Updates the tool list from the connected servers and raises <see cref="ToolsChanged"/>.
        /// </summary>
        private void UpdateTools()
        {
            lock (_serversLock)
            {
                _tools = [.. Servers.Where(x => x.IsConnected).SelectMany(x => x.Tools)];
            }
            ToolsChanged?.Invoke(this, EventArgs.Empty);
        }


        /// <summary>
        /// Gets the key of a server, the prefix of its tool names, unique among the servers.
        /// </summary>
        /// <param name="server">The server.</param>
        private string GetServerKey(McpServerModel server)
        {
            var baseKey = McpToolCall.SanitizeName(server.Config.Name ?? string.Empty).ToLowerInvariant().Replace('-', '_').Trim('_');
            if (baseKey.Length == 0)
                baseKey = "mcp";

            var usedKeys = _connections
                .Where(x => x.Key != server)
                .Select(x => x.Value.Key)
                .ToHashSet();
            var key = baseKey;
            for (int index = 2; usedKeys.Contains(key); index++)
                key = $"{baseKey}{index}";

            return key;
        }


        /// <summary>
        /// Records a line the server process wrote to standard error.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="serverName">The name of the server.</param>
        /// <param name="line">The line.</param>
        private void OnErrorLine(McpConnection connection, string serverName, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            connection.AddErrorLine(line.Trim());
            _logger.LogDebug("[McpService] [{Server}] {Line}", serverName, line);
        }


        /// <summary>
        /// Gets the message shown for a failed connection, with the error the server wrote to standard error.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <param name="connection">The connection.</param>
        private static string GetErrorMessage(Exception exception, McpConnection connection)
        {
            var message = exception.GetBaseException().Message;
            var errorLine = connection.GetErrorLine();
            if (!string.IsNullOrEmpty(errorLine) && !message.Contains(errorLine))
                message = $"{message} ({errorLine})";

            return ToolHelper.Truncate(message, 300, out _);
        }


        /// <summary>
        /// Gets the message for a command that is not installed, the runtime to install is named for the common launchers.
        /// </summary>
        /// <param name="command">The command.</param>
        private static string GetCommandNotFoundMessage(string command)
        {
            var name = Path.GetFileNameWithoutExtension(command ?? string.Empty).ToLowerInvariant();
            return name switch
            {
                "npx" or "npm" or "node" => AppErrors.McpRequiresNode,
                "uvx" or "uv" => AppErrors.McpRequiresUv,
                _ => string.Format(AppErrors.McpCommandNotFound, command)
            };
        }


        /// <summary>
        /// Finds the full path of a command, the folders of the PATH variable are searched for a command without a folder.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The full path, null when the command is not found.</returns>
        private static string FindCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return null;

            command = command.Trim().Trim('"');
            var hasDirectory = Path.IsPathRooted(command) || command.Contains('\\') || command.Contains('/');
            var directories = hasDirectory
                ? [string.Empty]
                : (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var hasExtension = _executableExtensions.Contains(Path.GetExtension(command), StringComparer.OrdinalIgnoreCase);
            foreach (var directory in directories)
            {
                try
                {
                    var basePath = Path.GetFullPath(Path.Combine(directory, command));
                    if (hasExtension && File.Exists(basePath))
                        return basePath;

                    var commandPath = _executableExtensions
                        .Select(x => basePath + x)
                        .FirstOrDefault(File.Exists);
                    if (commandPath != null)
                        return commandPath;
                }
                catch (Exception)
                {
                    // Not a valid folder in the PATH variable
                }
            }
            return null;
        }


        /// <summary>
        /// Quotes an argument of a command line run by the command interpreter.
        /// </summary>
        /// <param name="argument">The argument.</param>
        private static string QuoteArgument(string argument)
        {
            const string safeCharacters = "-_./:\\@=+,";
            if (argument.Length > 0 && argument.All(x => char.IsAsciiLetterOrDigit(x) || safeCharacters.Contains(x)))
                return argument;

            return $"\"{argument.Replace("\"", "\\\"")}\"";
        }


        /// <summary>
        /// The connection of a server.
        /// </summary>
        private sealed class McpConnection
        {
            private const int MaxErrorLines = 8;
            private readonly Queue<string> _errorLines = new Queue<string>();

            public SemaphoreSlim Gate { get; } = new SemaphoreSlim(1, 1);
            public string Key { get; set; }
            public McpClient Client { get; set; }
            public Process Process { get; set; }
            public CancellationTokenSource ConnectSource { get; set; }


            /// <summary>
            /// Adds a line the server process wrote to standard error, only the last lines are kept.
            /// </summary>
            /// <param name="line">The line.</param>
            public void AddErrorLine(string line)
            {
                lock (_errorLines)
                {
                    _errorLines.Enqueue(line);
                    while (_errorLines.Count > MaxErrorLines)
                        _errorLines.Dequeue();
                }
            }


            /// <summary>
            /// Gets the line that best explains a failure, the first of the last lines that reports an error, otherwise the last line.
            /// </summary>
            public string GetErrorLine()
            {
                lock (_errorLines)
                {
                    return _errorLines.FirstOrDefault(x => x.Contains("error", StringComparison.OrdinalIgnoreCase) && !x.Contains("complete log", StringComparison.OrdinalIgnoreCase))
                        ?? _errorLines.LastOrDefault();
                }
            }
        }
    }


    /// <summary>
    /// The result of testing a server.
    /// </summary>
    public sealed record McpTestResult(bool IsSuccess, string Message, IReadOnlyList<McpToolInfo> Tools);


    /// <summary>
    /// The name and description of a server tool.
    /// </summary>
    public sealed record McpToolInfo(string Name, string Description);


    public interface IMcpService
    {
        event EventHandler ToolsChanged;
        ObservableCollection<McpServerModel> Servers { get; }
        IReadOnlyList<McpToolCall> GetTools();
        Task ConnectAsync(CancellationToken cancellationToken = default);
        Task DisconnectAsync();
        Task<McpServerModel> AddAsync(McpServerConfig config);
        Task UpdateAsync(McpServerModel server, McpServerConfig config);
        Task RemoveAsync(McpServerModel server);
        Task ReconnectAsync(McpServerModel server);
        Task<McpTestResult> TestAsync(McpServerConfig config, CancellationToken cancellationToken = default);
    }
}
