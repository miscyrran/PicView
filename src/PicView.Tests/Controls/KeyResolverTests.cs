using Avalonia.Input;
using PicView.Avalonia.Input;

namespace PicView.Tests.Controls;

public class KeyResolverTests
{
    private static KeyEventArgs Event(Key key, string? symbol, PhysicalKey physical = PhysicalKey.None) =>
        new() { Key = key, KeySymbol = symbol, PhysicalKey = physical };

    [Fact]
    public void Resolve_NormalKey_ReturnsItUnchanged() =>
        Assert.Equal(Key.S, KeyResolver.Resolve(Event(Key.S, "s", PhysicalKey.S)));

    [Theory]
    [InlineData("s", Key.S)]
    [InlineData("S", Key.S)]
    [InlineData("7", Key.D7)]
    [InlineData("+", Key.OemPlus)]
    [InlineData(" ", Key.Space)]
    public void Resolve_UnicodePacketWithoutKeyCode_RecoversKeyFromSymbol(string symbol, Key expected) =>
        Assert.Equal(expected, KeyResolver.Resolve(Event(Key.None, symbol)));

    [Fact]
    public void Resolve_NoKeyCodeButPhysicalKey_UsesPhysicalKey() =>
        Assert.Equal(Key.R, KeyResolver.Resolve(Event(Key.None, null, PhysicalKey.R)));

    [Fact]
    public void Resolve_NothingUsable_ReturnsNone() =>
        Assert.Equal(Key.None, KeyResolver.Resolve(Event(Key.None, "é")));
}
