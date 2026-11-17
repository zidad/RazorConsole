// Copyright (c) RazorConsole. All rights reserved.

using RazorConsole.Testing;
using RazorConsole.Tests.Integration.Fixtures;

namespace RazorConsole.Tests.Integration;

public sealed class KeyPreventDefaultTests
{
    [Fact]
    public async Task PreventDefault_KeepsTabAndEnter_ButNotTyping()
    {
        var ct = TestContext.Current.CancellationToken;
        var events = new List<string>();
        await using var terminal = await StartAsync(events, preventDefault: true, ct);
        var focus = terminal.FocusKey;

        await terminal.SendKeyAsync(ConsoleKey.Tab, '\t', cancellationToken: ct);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: ct);
        await terminal.TypeAsync("a", ct);
        await terminal.WaitUntilAsync(_ => Snapshot(events).Contains("input a"), cancellationToken: ct);

        terminal.FocusKey.ShouldBe(focus);
        Snapshot(events).ShouldBe(["Tab", "Enter", "a", "input a"]);
    }

    [Fact]
    public async Task WithoutPreventDefault_TabMovesFocus()
    {
        var ct = TestContext.Current.CancellationToken;
        var events = new List<string>();
        await using var terminal = await StartAsync(events, preventDefault: false, ct);
        var focus = terminal.FocusKey;

        await terminal.SendKeyAsync(ConsoleKey.Tab, '\t', cancellationToken: ct);
        await terminal.WaitUntilAsync(_ => terminal.FocusKey != focus, cancellationToken: ct);

        Snapshot(events).ShouldBe(["Tab"]);
    }

    private static async Task<HeadlessTerminal> StartAsync(List<string> events, bool preventDefault, CancellationToken ct)
    {
        var terminal = await HeadlessTerminal.StartAsync<KeyPreventDefaultHost>(
            new HeadlessTerminalOptions
            {
                Width = 30,
                Height = 8,
                Parameters = new Dictionary<string, object?>
                {
                    [nameof(KeyPreventDefaultHost.Events)] = events,
                    [nameof(KeyPreventDefaultHost.PreventDefault)] = preventDefault,
                },
            },
            ct);
        await terminal.WaitUntilAsync(_ => terminal.FocusKey is not null, cancellationToken: ct);
        return terminal;
    }

    private static string[] Snapshot(List<string> events) => [.. events];
}
