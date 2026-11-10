// Copyright (c) RazorConsole. All rights reserved.

using RazorConsole.Testing;
using RazorConsole.Tests.Integration.Fixtures;

namespace RazorConsole.Tests.Integration;

public sealed class ColumnsGapTests
{
    [Fact]
    public async Task Columns_PutTheirGapBetweenChildren()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var terminal = await HeadlessTerminal.StartAsync<ColumnsGapHost>(
            new HeadlessTerminalOptions { Width = 20, Height = 3 },
            ct);

        var frame = await terminal.WaitForTextAsync("d", cancellationToken: ct);

        frame.ScreenText.ShouldContain("a b");
        frame.ScreenText.ShouldContain("c   d");
    }
}
