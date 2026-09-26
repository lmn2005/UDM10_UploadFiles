using System;

namespace UDM10.Server
{
    public sealed class StorageException : Exception
    {
        public StorageException(
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
