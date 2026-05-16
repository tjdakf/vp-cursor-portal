using System.Net.Sockets;
using System.Text;
using H2CursorRouter.Core.Domain;

namespace H2CursorRouter.H2;

public sealed class H2DeviceClient : IH2DeviceClient
{
    private readonly H2CommandBuilder _commandBuilder;
    private readonly H2ResponseParser _responseParser;
    private readonly SemaphoreSlim _commandLock = new(1, 1);

    public H2DeviceClient()
        : this(new H2CommandBuilder(), new H2ResponseParser())
    {
    }

    public H2DeviceClient(H2CommandBuilder commandBuilder, H2ResponseParser responseParser)
    {
        _commandBuilder = commandBuilder;
        _responseParser = responseParser;
    }

    public Task<H2CommandResult> LoadPresetAsync(
        H2DeviceConfig device,
        int screenId,
        int presetId,
        CancellationToken cancellationToken = default)
    {
        var requestJson = _commandBuilder.BuildLoadPreset(device.DeviceId, screenId, presetId);
        return SendAckCommandAsync(device, requestJson, "W0605", cancellationToken);
    }

    public Task<H2CommandResult> GetPresetEnumAsync(
        H2DeviceConfig device,
        int param0 = 0,
        int param1 = 0,
        CancellationToken cancellationToken = default)
    {
        var requestJson = _commandBuilder.BuildGetPresetEnum(param0, param1);
        return SendRawCommandAsync(device, requestJson, cancellationToken);
    }

    private async Task<H2CommandResult> SendAckCommandAsync(
        H2DeviceConfig device,
        string requestJson,
        string expectedCommand,
        CancellationToken cancellationToken)
    {
        var rawResult = await SendRawCommandAsync(device, requestJson, cancellationToken).ConfigureAwait(false);
        if (!rawResult.IsSuccess || rawResult.ResponseJson is null)
        {
            return rawResult;
        }

        var ack = _responseParser.ParseAck(rawResult.ResponseJson, expectedCommand);
        return ack.IsSuccess
            ? H2CommandResult.Success(requestJson, rawResult.ResponseJson)
            : H2CommandResult.Failure(ack.Message, requestJson, rawResult.ResponseJson);
    }

    private async Task<H2CommandResult> SendRawCommandAsync(
        H2DeviceConfig device,
        string requestJson,
        CancellationToken cancellationToken)
    {
        await _commandLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var udp = new UdpClient();
            udp.Connect(device.Host, device.Port <= 0 ? H2DeviceConfig.DefaultPort : device.Port);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);
            await udp.SendAsync(requestBytes, cancellationToken).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(device.Timeout);
            try
            {
                var response = await udp.ReceiveAsync(timeoutCts.Token).ConfigureAwait(false);
                var responseJson = Encoding.UTF8.GetString(response.Buffer);
                return H2CommandResult.Success(requestJson, responseJson);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return H2CommandResult.Failure($"H2 command timed out after {device.Timeout.TotalMilliseconds:0} ms.", requestJson);
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException or InvalidOperationException)
        {
            return H2CommandResult.Failure($"H2 command failed: {exception.Message}", requestJson);
        }
        finally
        {
            _commandLock.Release();
        }
    }
}
