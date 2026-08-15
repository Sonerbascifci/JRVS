using Jarvis.Core.Audio;

namespace Jarvis.Core.Tests;

public sealed class AudioContractTests
{
    [Fact]
    public void AudioInput_WhenDataIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new AudioInput(ReadOnlyMemory<byte>.Empty, "wav"));
    }

    [Fact]
    public void AudioInput_WhenFormatIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new AudioInput(new byte[] { 1, 2, 3 }, " "));
    }
}
