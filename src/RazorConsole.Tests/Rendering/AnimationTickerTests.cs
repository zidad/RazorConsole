// Copyright (c) RazorConsole. All rights reserved.

using RazorConsole.Core.Controllers;
using RazorConsole.Core.Rendering;
using RazorConsole.Core.Rendering.ComponentMarkup;
using RazorConsole.Core.Vdom;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace RazorConsole.Tests.Rendering;

public sealed class AnimationTickerTests
{
    [Fact]
    public void ResolveInterval_UsesShortestPositiveInterval()
    {
        var animations = new[]
        {
            new FakeAnimation(TimeSpan.FromMilliseconds(120)),
            new FakeAnimation(TimeSpan.Zero),
            new FakeAnimation(TimeSpan.FromMilliseconds(80)),
            new FakeAnimation(TimeSpan.FromMilliseconds(100)),
        };

        AnimationTicker.ResolveInterval(animations).ShouldBe(TimeSpan.FromMilliseconds(80));
    }

    [Fact]
    public void ResolveInterval_IsNullWithoutAnimations()
    {
        AnimationTicker.ResolveInterval(null).ShouldBeNull();
        AnimationTicker.ResolveInterval([]).ShouldBeNull();
        AnimationTicker.ResolveInterval([new FakeAnimation(TimeSpan.Zero)]).ShouldBeNull();
    }

    [Fact]
    public void ResolveInterval_ClampsToMinimum()
    {
        AnimationTicker.ResolveInterval([new FakeAnimation(TimeSpan.FromMilliseconds(1))])
            .ShouldBe(AnimationTicker.MinimumInterval);
    }

    [Fact]
    public void Update_StartsAndStopsWithAnimations()
    {
        using var ticker = new AnimationTicker(static () => { });

        ticker.Update([new FakeAnimation(TimeSpan.FromSeconds(10))]);
        ticker.Interval.ShouldBe(TimeSpan.FromSeconds(10));

        ticker.Update([]);
        ticker.Interval.ShouldBeNull();
    }

    [Fact]
    public async Task Tick_RefreshesCanvas()
    {
        var ticked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var ticker = new AnimationTicker(() => ticked.TrySetResult());

        ticker.Update([new FakeAnimation(TimeSpan.FromMilliseconds(20))]);

        await ticked.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Tick_SlowerThanInterval_NeverOverlapsTheNextTick()
    {
        var running = 0;
        var maxRunning = 0;
        var ticks = 0;
        var threeTicks = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var ticker = new AnimationTicker(() =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref maxRunning, now);
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
            Interlocked.Decrement(ref running);
            if (Interlocked.Increment(ref ticks) == 3)
            {
                threeTicks.TrySetResult();
            }
        });

        ticker.Update([new FakeAnimation(AnimationTicker.MinimumInterval)]);
        await threeTicks.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        ticker.Update([]);

        maxRunning.ShouldBe(1);
    }

    [Fact]
    public void Dispose_StopsTicker()
    {
        var ticker = new AnimationTicker(static () => { });
        ticker.Update([new FakeAnimation(TimeSpan.FromSeconds(10))]);

        ticker.Dispose();
        ticker.Update([new FakeAnimation(TimeSpan.FromSeconds(10))]);

        ticker.Interval.ShouldBeNull();
    }

    [Fact]
    public void LiveDisplayContext_ManyAnimationsShareOneTimer()
    {
        var animations = Enumerable.Range(0, 50)
            .Select(index => (IAnimatedConsoleRenderable)new FakeAnimation(TimeSpan.FromSeconds(10 + index)))
            .ToArray();
        var view = CreateView("spinners", animations);

        using var context = ConsoleLiveDisplayContext.CreateForTesting(new NoopCanvas(), view, new VdomDiffService());

        context.AnimationInterval.ShouldBe(TimeSpan.FromSeconds(10));

        context.UpdateView(CreateView("static", []));
        context.AnimationInterval.ShouldBeNull();
    }

    [Fact]
    public void LiveDisplayContext_UpdateRenderableStopsAnimations()
    {
        var view = CreateView("spinner", [new FakeAnimation(TimeSpan.FromSeconds(10))]);
        using var context = ConsoleLiveDisplayContext.CreateForTesting(new NoopCanvas(), view, new VdomDiffService());

        context.UpdateRenderable(new Text("custom"));

        context.AnimationInterval.ShouldBeNull();
    }

    private static void InterlockedMax(ref int target, int value)
    {
        var current = Volatile.Read(ref target);
        while (value > current)
        {
            var seen = Interlocked.CompareExchange(ref target, value, current);
            if (seen == current)
            {
                return;
            }

            current = seen;
        }
    }

    private static ConsoleViewResult CreateView(string text, IReadOnlyCollection<IAnimatedConsoleRenderable> animations)
    {
        var root = VNode.CreateElement("div");
        root.AddChild(VNode.CreateText(text));
        return ConsoleViewResult.Create(text, root, new Text(text), animations);
    }

    private sealed class FakeAnimation(TimeSpan interval) : IAnimatedConsoleRenderable
    {
        public TimeSpan RefreshInterval { get; } = interval;

        public Measurement Measure(RenderOptions options, int maxWidth) => new(1, 1);

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            yield return new Segment("*");
        }
    }

    private sealed class NoopCanvas : ConsoleLiveDisplayContext.ILiveDisplayCanvas
    {
        public void UpdateTarget(IRenderable? renderable)
        {
        }

        public void Refresh() => Refreshed?.Invoke();

        public void RefreshAnimationFrame() => Refreshed?.Invoke();

        public event Action? Refreshed;
    }
}
