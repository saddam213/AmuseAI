using Amuse.Common.Message;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.Common.Tensor;

namespace Amuse.Common
{
    public static partial class Extensions
    {
        /// <summary>
        /// Sends a PythonMessage.
        /// </summary>
        /// <typeparam name="T">IPythonMessage</typeparam>
        /// <param name="pipe">The pipe.</param>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        internal static async Task SendMessage<T>(this PipeStream pipe, T message, CancellationToken cancellationToken = default) where T : IPipelineMessage
        {
            var intBuffer = new byte[4];
            var tensors = message.Tensors ?? [];
            var jsonData = JsonSerializer.Serialize(message);
            var jsonBytes = Encoding.UTF8.GetBytes(jsonData);

            // Write tensor count
            BitConverter.TryWriteBytes(intBuffer, tensors.Count);
            await pipe.WriteAsync(intBuffer, cancellationToken);

            // Write JSON length
            BitConverter.TryWriteBytes(intBuffer, jsonBytes.Length);
            await pipe.WriteAsync(intBuffer, cancellationToken);

            // Tensors
            foreach (var tensor in tensors)
            {
                // Rank
                BitConverter.TryWriteBytes(intBuffer, tensor.Rank);
                await pipe.WriteAsync(intBuffer, cancellationToken);

                // Dimensions
                foreach (var dim in tensor.Dimensions.ToArray())
                {
                    BitConverter.TryWriteBytes(intBuffer, dim);
                    await pipe.WriteAsync(intBuffer, cancellationToken);
                }

                // Tensor buffer
                await pipe.WriteAsync(new byte[] { 0 }, cancellationToken); // float32
                var tensorBytes = MemoryMarshal.AsBytes(tensor.Memory.Span).ToArray();
                BitConverter.TryWriteBytes(intBuffer, tensorBytes.Length);
                await pipe.WriteAsync(intBuffer, cancellationToken);
                await pipe.WriteAsync(tensorBytes, cancellationToken);
            }

            // JSON
            await pipe.WriteAsync(jsonBytes, cancellationToken);
            await pipe.FlushAsync(cancellationToken);
        }


        /// <summary>
        /// Receives a PythonMessage message.
        /// </summary>
        /// <typeparam name="T">IPythonMessage</typeparam>
        /// <param name="pipe">The pipe.</param>
        /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        internal static async Task<T> ReceiveMessage<T>(this PipeStream pipe, CancellationToken cancellationToken = default) where T : IPipelineMessage
        {
            var tensorCountBytes = await pipe.ReadExactlyAsync(4, cancellationToken);
            int tensorCount = BitConverter.ToInt32(tensorCountBytes);
            var jsonLengthBytes = await pipe.ReadExactlyAsync(4, cancellationToken);
            int jsonLength = BitConverter.ToInt32(jsonLengthBytes);

            // Tensors
            var tensors = new List<Tensor<float>>();
            for (int t = 0; t < tensorCount; t++)
            {
                // Rank
                var rankBytes = await pipe.ReadExactlyAsync(4, cancellationToken);
                int rank = BitConverter.ToInt32(rankBytes);

                // Dimensions
                int[] dims = new int[rank];
                for (int d = 0; d < rank; d++)
                {
                    var dimBytes = await pipe.ReadExactlyAsync(4, cancellationToken);
                    dims[d] = BitConverter.ToInt32(dimBytes);
                }

                // Type byte
                var typeByte = await pipe.ReadExactlyAsync(1, cancellationToken);
                if (typeByte[0] != 0)
                    throw new NotSupportedException("Only float32 tensors are supported.");

                // Tensor buffer length
                var bufferLenBytes = await pipe.ReadExactlyAsync(4, cancellationToken);
                int bufferLen = BitConverter.ToInt32(bufferLenBytes);

                // Tensor buffer
                var floats = new float[bufferLen / 4];
                var tensorBytes = await pipe.ReadExactlyAsync(bufferLen, cancellationToken);
                Buffer.BlockCopy(tensorBytes, 0, floats, 0, bufferLen);

                var tensor = new Tensor<float>(dims);
                floats.CopyTo(tensor.Memory.Span);
                tensors.Add(tensor);
            }

            // JSON
            var jsonBytes = await pipe.ReadExactlyAsync(jsonLength, cancellationToken);
            string json = Encoding.UTF8.GetString(jsonBytes);
            var response = JsonSerializer.Deserialize<T>(json);
            response.Tensors = tensors;
            return response;
        }


        /// <summary>
        /// Sends a object as JSON.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="pipe">The pipe.</param>
        /// <param name="dataObject">The object to send.</param>
        internal static async Task SendObject<T>(this PipeStream pipe, T dataObject, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(dataObject);
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var lengthBytes = BitConverter.GetBytes(jsonBytes.Length);
            await pipe.WriteAsync(lengthBytes, cancellationToken);
            await pipe.WriteAsync(jsonBytes, cancellationToken);
            await pipe.FlushAsync(cancellationToken);
        }


        /// <summary>
        /// Receives the object as JSON.
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="pipe">The pipe.</param>
        internal static async Task<T> ReceiveObject<T>(this PipeStream pipe, CancellationToken cancellationToken)
        {
            var lengthBytes = new byte[4];
            await pipe.ReadExactlyAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);
            int jsonLength = BitConverter.ToInt32(lengthBytes);

            byte[] jsonData = new byte[jsonLength];
            await pipe.ReadExactlyAsync(jsonData, 0, jsonLength, cancellationToken);

            string jsonString = Encoding.UTF8.GetString(jsonData);
            return JsonSerializer.Deserialize<T>(jsonString);
        }


        /// <summary>
        /// Sends an empty response.
        /// </summary>
        /// <param name="pipe">The pipe.</param>
        /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Task.</returns>
        internal static Task SendResponse(this PipeStream pipe, CancellationToken cancellationToken = default)
        {
            return pipe.SendMessage(new PipelineResponse { Tensors = [] }, cancellationToken);
        }


        /// <summary>
        /// Read exactly n bytes
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="count">The count.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        internal static async Task<byte[]> ReadExactlyAsync(this Stream stream, int count, CancellationToken cancellationToken = default)
        {
            int offset = 0;
            var buffer = new byte[count];
            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken);
                if (read == 0)
                    throw new EndOfStreamException();
                offset += read;
            }
            return buffer;
        }
    
    }
}
