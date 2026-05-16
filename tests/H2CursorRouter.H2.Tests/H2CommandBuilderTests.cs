using H2CursorRouter.H2;
using Xunit;

namespace H2CursorRouter.H2.Tests;

public sealed class H2CommandBuilderTests
{
    [Fact]
    public void W0605SerializationMatchesProtocolShape()
    {
        var json = new H2CommandBuilder().BuildLoadPreset(0, 0, 0);
        Assert.Equal("[{\"cmd\":\"W0605\",\"deviceId\":0,\"screenId\":0,\"presetId\":0}]", json);
    }

    [Fact]
    public void R0600SerializationMatchesProtocolShape()
    {
        var json = new H2CommandBuilder().BuildGetPresetEnum();
        Assert.Equal("[{\"cmd\":\"R0600\",\"param0\":0,\"param1\":0}]", json);
    }
}
