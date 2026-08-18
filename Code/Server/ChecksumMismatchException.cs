using System;

namespace UDM10.Server
{
    public class ChecksumMismatchException : Exception
    {
        public ChecksumMismatchException(string message) : base(message) { }
    }
}