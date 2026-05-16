using H2CursorRouter.H2;
using Xunit;

namespace H2CursorRouter.H2.Tests;

public sealed class H2ResponseParserTests
{
    private readonly H2ResponseParser _parser = new();

    [Fact]
    public void AckParserAcceptsOkCaseInsensitively()
    {
        var result = _parser.ParseAck("[{\"cmd\":\"W0605\",\"deviceId\":0,\"ack\":\" ok \"}]", "W0605");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void AckParserRejectsError()
    {
        var result = _parser.ParseAck("[{\"cmd\":\"W0605\",\"deviceId\":0,\"ack\":\"Error\"}]", "W0605");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void AckParserRejectsMalformedJson()
    {
        var result = _parser.ParseAck("[{\"cmd\":\"W0605\"", "W0605");
        Assert.False(result.IsSuccess);
        Assert.Contains("malformed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AckParserRejectsUnexpectedCommand()
    {
        var result = _parser.ParseAck("[{\"cmd\":\"R0600\",\"ack\":\"Ok\"}]", "W0605");
        Assert.False(result.IsSuccess);
        Assert.Contains("expected command", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
