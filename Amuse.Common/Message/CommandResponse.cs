using System;
using System.Text.Json.Serialization;

namespace Amuse.Common.Message
{
    public class CommandResponse
    {
        public CommandResponse() { }
        public CommandResponse(Exception ex) : this(ex.Message)
        {
            IsCanceled = ex is OperationCanceledException;
        }
        public CommandResponse(string errorMessage)
        {
            Error = errorMessage;
        }
        public string Error { get; init; }
        public bool IsCanceled { get; init; }

        [JsonIgnore]
        public bool IsError => !string.IsNullOrEmpty(Error);
    }
}
