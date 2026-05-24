using System.Net;
using System.Net.Sockets;
using System.Text;
using H2CursorRouter.Core.Domain;
using Xunit;

namespace H2CursorRouter.H2.Tests;

public sealed class H2DeviceClientIntegrationTests
{
    [Fact]
    public async Task FakeUdpServerAckOkSucceeds()
    {
        var result = await RunWithFakeServerAsync("[{\"cmd\":\"W0605\",\"deviceId\":0,\"ack\":\"Ok\"}]");
        Assert.True(result.IsSuccess, result.Message);
    }

    [Fact]
    public async Task FakeUdpServerAckErrorFails()
    {
        var result = await RunWithFakeServerAsync("[{\"cmd\":\"W0605\",\"deviceId\":0,\"ack\":\"Error\"}]");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task FakeUdpServerNoReplyTimesOut()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var device = new H2DeviceConfig("h2", "H2", "127.0.0.1", port, 0, TimeSpan.FromMilliseconds(50));

        var result = await new H2DeviceClient().LoadPresetAsync(device, 0, 0);

        Assert.False(result.IsSuccess);
        Assert.Contains("timed out", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<H2CommandResult> RunWithFakeServerAsync(string responseJson)
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var serverTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            var responseBytes = Encoding.UTF8.GetBytes(responseJson);
            await server.SendAsync(responseBytes, request.RemoteEndPoint);
        });

        var device = new H2DeviceConfig("h2", "H2", "127.0.0.1", port, 0, TimeSpan.FromSeconds(1));
        var result = await new H2DeviceClient().LoadPresetAsync(device, 0, 0);
        await serverTask;
        return result;
    }
}
