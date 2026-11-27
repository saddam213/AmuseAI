using Amuse.Common.Config;
using Amuse.Common.Message;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Amuse.Common
{
    /// <summary>
    /// Streams items in both directions between a PipelineClient and a PipelineServer so TensorStack streaming pipelines can run in a host.
    /// Headers travel over a duplex pipe and tensors through a reusable memory mapped file, each item is acknowledged
    /// when the reader takes it, so only a single item is ever in flight per direction.
    /// </summary>
    public sealed class PipelineStreamChannel : IDisposable
    {
        private readonly PipeStream _pipe;
        private readonly string _mapName;
        private readonly object _stateLock = new object();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private Channel<PipelineStreamItem> _inbound;
        private TaskCompletionSource _readySource;
        private TaskCompletionSource _ackSource;
        private MemoryMappedFile _map;
        private string _mapCurrentName;
        private long _mapCapacity;
        private int _mapGeneration;
        private int _streamId;
        private bool _isOpen;

        /// <summary>
        /// Initializes a new instance of the <see cref="PipelineStreamChannel"/> class.
        /// </summary>
        /// <param name="pipe">The duplex stream pipe, owned by the caller.</param>
        /// <param name="serverConfig">The server configuration.</param>
        /// <param name="isServer">if set to <c>true</c> this is the server end of the channel.</param>
        public PipelineStreamChannel(PipeStream pipe, ServerConfig serverConfig, bool isServer)
        {
            _pipe = pipe;
            _mapName = isServer
                ? $"{serverConfig.ChannelStream}.ServerToClient"
                : $"{serverConfig.ChannelStream}.ClientToServer";
        }


        /// <summary>
        /// Runs the receive loop until the pipe closes, call once after the pipe is connected.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var message = await _pipe.ReceiveMessage<PipelineStreamMessage>(cancellationToken);
                    Receive(message);
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }


        /// <summary>
        /// Opens a stream, messages for any other stream are ignored.
        /// </summary>
        /// <param name="streamId">The stream identifier, shared by both ends.</param>
        public void Open(int streamId)
        {
            lock (_stateLock)
            {
                _streamId = streamId;
                _inbound = Channel.CreateUnbounded<PipelineStreamItem>();
                _readySource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _ackSource = null;
                _isOpen = true;
            }
        }


        /// <summary>
        /// Tells the other end this end has opened the stream and is ready to receive items.
        /// </summary>
        public Task SendReadyAsync()
        {
            return SendAsync(PipelineStreamMessageType.Ready);
        }


        /// <summary>
        /// Waits for the other end to open the stream.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public Task WaitReadyAsync(CancellationToken cancellationToken = default)
        {
            lock (_stateLock)
            {
                ThrowIfClosed();
                return _readySource.Task.WaitAsync(cancellationToken);
            }
        }


        /// <summary>
        /// Writes an item to the stream and waits until the other end has taken it.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task WriteAsync(PipelineStreamItem item, CancellationToken cancellationToken = default)
        {
            PipelineStreamMessage message;
            TaskCompletionSource ackSource;
            lock (_stateLock)
            {
                ThrowIfClosed();
                message = WriteItem(item);
                ackSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _ackSource = ackSource;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await SendAsync(message);
            await ackSource.Task.WaitAsync(cancellationToken);
        }


        /// <summary>
        /// Completes this end of the stream, the other end stops reading after the items already sent.
        /// </summary>
        /// <param name="exception">The exception that ended the stream, null when it completed.</param>
        public Task CompleteAsync(Exception exception = null)
        {
            return SendAsync(PipelineStreamMessageType.End, exception?.Message);
        }


        /// <summary>
        /// Reads the items written by the other end until it completes its stream.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async IAsyncEnumerable<PipelineStreamItem> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Channel<PipelineStreamItem> inbound;
            lock (_stateLock)
            {
                ThrowIfClosed();
                inbound = _inbound;
            }

            await foreach (var item in inbound.Reader.ReadAllAsync(cancellationToken))
            {
                await SendAsync(PipelineStreamMessageType.Ack);
                yield return item;
            }
        }


        /// <summary>
        /// Closes the stream, pending reads complete and pending writes are cancelled.
        /// </summary>
        public void Close()
        {
            lock (_stateLock)
            {
                _isOpen = false;
                _inbound?.Writer.TryComplete();
                _readySource?.TrySetCanceled();
                _ackSource?.TrySetCanceled();
                _map?.Dispose();
                _map = null;
                _mapCapacity = 0;
            }
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Close();
            _sendLock.Dispose();
        }


        /// <summary>
        /// Handles a message from the other end.
        /// </summary>
        /// <param name="message">The message.</param>
        private void Receive(PipelineStreamMessage message)
        {
            lock (_stateLock)
            {
                if (!_isOpen || message.StreamId != _streamId)
                    return;

                if (message.Type == PipelineStreamMessageType.Ready)
                {
                    _readySource.TrySetResult();
                }
                else if (message.Type == PipelineStreamMessageType.Ack)
                {
                    _ackSource?.TrySetResult();
                }
                else if (message.Type == PipelineStreamMessageType.End)
                {
                    _inbound.Writer.TryComplete(message.Error is null ? null : new Exception(message.Error));
                }
                else if (message.Type == PipelineStreamMessageType.Item)
                {
                    try
                    {
                        _inbound.Writer.TryWrite(ReadItem(message));
                    }
                    catch (Exception ex)
                    {
                        _inbound.Writer.TryComplete(ex);
                    }
                }
            }
        }


        /// <summary>
        /// Fails the open stream when the pipe closes or breaks.
        /// </summary>
        /// <param name="exception">The exception.</param>
        private void Fail(Exception exception)
        {
            lock (_stateLock)
            {
                if (!_isOpen)
                    return;

                _inbound.Writer.TryComplete(exception);
                _readySource.TrySetCanceled();
                _ackSource?.TrySetCanceled();
            }
        }


        /// <summary>
        /// Sends a message without item data for the open stream.
        /// </summary>
        /// <param name="type">The message type.</param>
        /// <param name="error">The error, End messages only.</param>
        private Task SendAsync(PipelineStreamMessageType type, string error = null)
        {
            PipelineStreamMessage message;
            lock (_stateLock)
            {
                if (!_isOpen)
                    return Task.CompletedTask;

                message = new PipelineStreamMessage
                {
                    StreamId = _streamId,
                    Type = type,
                    Error = error
                };
            }
            return SendAsync(message);
        }


        /// <summary>
        /// Sends a message, the write is never cancelled so a message is always written whole.
        /// </summary>
        /// <param name="message">The message.</param>
        private async Task SendAsync(PipelineStreamMessage message)
        {
            await _sendLock.WaitAsync();
            try
            {
                await _pipe.SendMessage(message, CancellationToken.None);
            }
            finally
            {
                _sendLock.Release();
            }
        }


        /// <summary>
        /// Throws if no stream is open.
        /// </summary>
        private void ThrowIfClosed()
        {
            if (!_isOpen)
                throw new InvalidOperationException("The pipeline stream is not open.");
        }


        /// <summary>
        /// Copies the item tensors to shared memory and creates the header that describes them.
        /// </summary>
        /// <param name="item">The item.</param>
        private unsafe PipelineStreamMessage WriteItem(PipelineStreamItem item)
        {
            var tensors = item.Tensors ?? [];
            var dimensions = new List<int[]>(tensors.Count);
            long totalBytes = 0;
            foreach (var tensor in tensors)
            {
                dimensions.Add(tensor.Dimensions.ToArray());
                totalBytes = checked(totalBytes + tensor.Length * sizeof(float));
            }

            if (totalBytes > 0)
            {
                EnsureMap(totalBytes);
                using (var accessor = _map.CreateViewAccessor(0, totalBytes, MemoryMappedFileAccess.ReadWrite))
                {
                    byte* pointer = null;
                    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
                    try
                    {
                        long offset = 0;
                        foreach (var tensor in tensors)
                        {
                            var destination = new Span<float>(pointer + accessor.PointerOffset + offset, tensor.Span.Length);
                            tensor.Span.CopyTo(destination);
                            offset += tensor.Length * sizeof(float);
                        }
                    }
                    finally
                    {
                        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    }
                }
            }

            return new PipelineStreamMessage
            {
                StreamId = _streamId,
                Type = PipelineStreamMessageType.Item,
                Index = item.Index,
                Rate = item.Rate,
                Text = item.Text,
                MapName = totalBytes > 0 ? _mapCurrentName : null,
                Dimensions = dimensions
            };
        }


        /// <summary>
        /// Reads the item described by the header, copying its tensors out of shared memory.
        /// </summary>
        /// <param name="message">The message.</param>
        private static PipelineStreamItem ReadItem(PipelineStreamMessage message)
        {
            return new PipelineStreamItem
            {
                Index = message.Index,
                Rate = message.Rate,
                Text = message.Text,
                Tensors = message.MapName is null ? [] : PipelineTensorChannel.ReadFromMappedFile(message.MapName, message.Dimensions)
            };
        }


        /// <summary>
        /// Ensures the shared memory can hold an item of the given size, the map is kept and reused between items.
        /// </summary>
        /// <param name="totalBytes">The item size in bytes.</param>
        private void EnsureMap(long totalBytes)
        {
            if (_map != null && _mapCapacity >= totalBytes)
                return;

            _map?.Dispose();
            _mapGeneration++;
            _mapCurrentName = $"{_mapName}.{_mapGeneration}";
            _map = MemoryMappedFile.CreateNew(_mapCurrentName, totalBytes, MemoryMappedFileAccess.ReadWrite);
            _mapCapacity = totalBytes;
        }
    }
}
