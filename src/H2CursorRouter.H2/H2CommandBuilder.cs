using System.Buffers;
using System.Text;
using System.Text.Json;

namespace H2CursorRouter.H2;

public sealed class H2CommandBuilder
{
    public string BuildLoadPreset(int deviceId, int screenId, int presetId)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteString("cmd", "W0605");
        writer.WriteNumber("deviceId", deviceId);
        writer.WriteNumber("screenId", screenId);
        writer.WriteNumber("presetId", presetId);
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public string BuildGetPresetEnum(int param0 = 0, int param1 = 0)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteString("cmd", "R0600");
        writer.WriteNumber("param0", param0);
        writer.WriteNumber("param1", param1);
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
