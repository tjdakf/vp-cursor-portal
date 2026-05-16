using System.Text.Json;

namespace H2CursorRouter.H2;

public sealed class H2ResponseParser
{
    public H2AckResult ParseAck(string responseJson, string expectedCommand)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return H2AckResult.Failure("H2 response is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                return H2AckResult.Failure("H2 response must be a non-empty JSON array.");
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var command = item.TryGetProperty("cmd", out var commandElement)
                    ? commandElement.GetString()
                    : null;
                if (!string.Equals(command, expectedCommand, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!item.TryGetProperty("ack", out var ackElement))
                {
                    return H2AckResult.Failure($"H2 response for '{expectedCommand}' did not include an ack.", command);
                }

                var ack = ackElement.GetString()?.Trim();
                if (string.Equals(ack, "Ok", StringComparison.OrdinalIgnoreCase))
                {
                    return H2AckResult.Success(command, ack ?? string.Empty);
                }

                if (string.Equals(ack, "Error", StringComparison.OrdinalIgnoreCase))
                {
                    return H2AckResult.Failure($"H2 command '{expectedCommand}' returned ack Error.", command, ack);
                }

                return H2AckResult.Failure($"H2 command '{expectedCommand}' returned unexpected ack '{ack}'.", command, ack);
            }

            return H2AckResult.Failure($"H2 response did not contain expected command '{expectedCommand}'.");
        }
        catch (JsonException exception)
        {
            return H2AckResult.Failure($"H2 response JSON is malformed: {exception.Message}");
        }
    }
}
