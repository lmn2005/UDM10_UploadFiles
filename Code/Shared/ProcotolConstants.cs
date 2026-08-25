namespace UDM10.Shared
{
    public static class ProtocolConstants
    {
        public const string CurrentVersion = "V3";

    
        public const int MaxMetadataLength = 4096;

        public const int DefaultChunkSize = 64 * 1024;

        public const int ChunkSize = DefaultChunkSize;

        public const int MaxRequestIdLength = 128;

        public const int MaxFileNameLength = 255;
    }
}