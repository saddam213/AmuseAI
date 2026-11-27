using Amuse.Common.Config;
using Amuse.Common.Message;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using TensorStack.Common;
using TensorStack.Common.Tensor;

namespace Amuse.Common
{
    public unsafe class PipelineTensorChannel
    {
        private readonly string _requestMapName;
        private readonly string _responseMapName;

        public PipelineTensorChannel(ServerConfig serverConfig)
        {
            _requestMapName = $"{serverConfig.ChannelTensor}.ClientToServer";
            _responseMapName = $"{serverConfig.ChannelTensor}.ServerToClient";
        }


        /// <summary>
        /// Writes the request tensors to shared memory.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>MemoryMappedFile.</returns>
        public MemoryMappedFile WriteRequest(PipelineRequest request)
        {
            var tensors = request.PackTensors();
            return WriteToMappedFile(_requestMapName, tensors ?? []);
        }


        /// <summary>
        /// Reads the request tensors from shared memory.
        /// </summary>
        /// <param name="request">The request.</param>
        public void ReadRequest(PipelineRequest request)
        {
            var metadata = request.TensorMetadata;
            if (request == null || metadata == null || metadata.Dimensions.IsNullOrEmpty())
                return;

            var packedTensors = ReadFromMappedFile(_requestMapName, metadata.Dimensions);
            request.UnpackTensors(packedTensors);
        }


        /// <summary>
        /// Writes the response tensors to shared memory.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>MemoryMappedFile.</returns>
        public MemoryMappedFile WriteResponse(IReadOnlyList<Tensor<float>> tensors)
        {
            return WriteToMappedFile(_responseMapName, tensors ?? []);
        }


        /// <summary>
        /// Reads the response tensors from shared memory.
        /// </summary>
        /// <param name="response">The response.</param>
        public void ReadResponse(PipelineResponse response)
        {
            var metadata = response.TensorMetadata;
            if (response == null || metadata == null || metadata.Dimensions.IsNullOrEmpty())
                return;

            var tensors = ReadFromMappedFile(_responseMapName, metadata.Dimensions);
            response.UnpackTensors(tensors);
        }


        /// <summary>
        /// Writes Tensors to a MemoryMappedFile
        /// Note: MemoryMappedFile should be kept alive untill response is returned
        /// </summary>
        /// <param name="mapName">Name of the MemoryMappedFile.</param>
        /// <param name="tensors">The tensors to write to shared memory.</param>
        /// <returns>MemoryMappedFile.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        private static MemoryMappedFile WriteToMappedFile(string mapName, IReadOnlyList<Tensor<float>> tensors)
        {
            ArgumentNullException.ThrowIfNull(tensors);
            ArgumentException.ThrowIfNullOrEmpty(mapName);

            long totalFloats = 0;
            foreach (var tensor in tensors)
                totalFloats = checked(totalFloats + tensor.Length);

            long totalBytes = checked(totalFloats * sizeof(float));
            if (totalBytes == 0)
                totalBytes = sizeof(float);

            var memoryMappedFile = MemoryMappedFile.CreateNew(mapName, totalBytes, MemoryMappedFileAccess.ReadWrite);
            try
            {
                using (var accessor = memoryMappedFile.CreateViewAccessor(0, totalBytes, MemoryMappedFileAccess.ReadWrite))
                {
                    byte* ptr = null;
                    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                    try
                    {
                        long floatOffset = 0;
                        foreach (var tensor in tensors)
                        {
                            var destination = new Span<float>(ptr + accessor.PointerOffset + floatOffset * sizeof(float), tensor.Span.Length);
                            tensor.Span.CopyTo(destination);
                            floatOffset += tensor.Length;
                        }
                    }
                    finally
                    {
                        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    }
                    accessor.Flush();
                    return memoryMappedFile;
                }
            }
            catch
            {
                memoryMappedFile.Dispose();
                throw;
            }
        }


        /// <summary>
        /// Reads Tensors from a MemoryMappedFile
        /// </summary>
        /// <param name="mapName">Name of the map.</param>
        /// <param name="tensorMetadata">The shapes.</param>
        /// <returns>List&lt;Tensor&lt;System.Single&gt;&gt;.</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        internal static List<Tensor<float>> ReadFromMappedFile(string mapName, IReadOnlyList<int[]> tensorMetadata)
        {
            ArgumentException.ThrowIfNullOrEmpty(mapName);
            ArgumentNullException.ThrowIfNull(tensorMetadata);

            long totalFloats = 0;
            foreach (var metadata in tensorMetadata)
                totalFloats = checked(totalFloats + GetElementCount(metadata));

            long totalBytes = checked(totalFloats * sizeof(float));
            if (totalBytes == 0)
                totalBytes = sizeof(float);

            using (var mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read))
            using (var accessor = mmf.CreateViewAccessor(0, totalBytes, MemoryMappedFileAccess.Read))
            {
                byte* ptr = null;
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                try
                {
                    long floatOffset = 0;
                    var tensors = new List<Tensor<float>>(tensorMetadata.Count);
                    foreach (var metadata in tensorMetadata)
                    {
                        int elementCount = GetElementCount(metadata);
                        var tensor = new Tensor<float>(metadata);
                        var source = new ReadOnlySpan<float>(ptr + accessor.PointerOffset + floatOffset * sizeof(float), elementCount);
                        source.CopyTo(tensor.Memory.Span);
                        tensors.Add(tensor);
                        floatOffset += elementCount;
                    }
                    return tensors;
                }
                finally
                {
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }
        }


        /// <summary>
        /// Gets the element count.
        /// </summary>
        /// <param name="dimensions">The dimensions.</param>
        /// <returns>System.Int32.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">dimensions - Tensor dimensions cannot be negative.</exception>
        private static int GetElementCount(int[] dimensions)
        {
            int count = 1;
            foreach (int dimension in dimensions)
            {
                if (dimension < 0)
                    throw new ArgumentOutOfRangeException(nameof(dimensions), "Tensor dimensions cannot be negative.");

                count = checked(count * dimension);
            }
            return count;
        }
    }
}
