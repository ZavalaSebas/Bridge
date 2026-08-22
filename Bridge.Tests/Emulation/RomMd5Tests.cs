using Bridge.Emulation;

namespace Bridge.Tests.Emulation;

public class RomMd5Tests
{
    [Fact]
    public void ComputeHex_ReturnsLowercaseMd5()
    {
        var md5 = RomMd5.ComputeHex("test"u8.ToArray());
        Assert.Equal("098f6bcd4621d373cade4e832627b4f6", md5);
    }
}
