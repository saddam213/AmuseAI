using System;

namespace Amuse.Common
{
    /// <summary>
    /// An <see cref="IProgress{T}"/> that runs the handler on the reporting thread, so reports are handled in the order they are made.
    /// Unlike <see cref="Progress{T}"/>, which posts each report to the thread pool when there is no synchronization context.
    /// </summary>
    /// <typeparam name="T">The progress type.</typeparam>
    public sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncProgress{T}"/> class.
        /// </summary>
        /// <param name="handler">The handler.</param>
        public SyncProgress(Action<T> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }


        /// <summary>
        /// Reports a progress update.
        /// </summary>
        /// <param name="value">The value.</param>
        public void Report(T value)
        {
            _handler(value);
        }
    }
}
