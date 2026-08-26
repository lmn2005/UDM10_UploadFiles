using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UDM10.Shared
{
    internal static class ProtocolSerialization
    {
        internal static JsonSerializerOptions JsonOptions { get; } =
            new(JsonSerializerDefaults.Web)
            {
                MaxDepth = 16,
                UnmappedMemberHandling =
                    JsonUnmappedMemberHandling.Disallow
            };

        internal static UTF8Encoding Utf8 { get; } =
            new(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);
    }
}
