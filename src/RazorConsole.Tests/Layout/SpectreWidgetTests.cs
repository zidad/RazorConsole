// Copyright (c) RazorConsole. All rights reserved.

using RazorConsole.Core.Layout;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace RazorConsole.Tests.Layout;

public sealed class SpectreWidgetTests
{
    [Fact]
    public void LayoutAndRepaints_RenderTheRenderableOnce()
    {
        var renderable = new CountingRenderable(lineCount: 50);
        var result = new LayoutEngine().Layout(new SpectreWidget("doc", renderable, renderOnce: true), new BoxConstraints(0, 40, 0, 10).WithUnboundedHeight());

        Paint(result);
        Paint(result);
        Paint(result);

        renderable.Renders.ShouldBe(1);
    }

    [Fact]
    public void Measure_AtAnotherWidth_RendersAgain()
    {
        var renderable = new CountingRenderable(lineCount: 3);
        var widget = new SpectreWidget("doc", renderable, renderOnce: true);
        var context = new LayoutContext();

        widget.Measure(context, new BoxConstraints(0, 40, 0, 10));
        widget.Measure(context, new BoxConstraints(0, 30, 0, 10));

        renderable.Renders.ShouldBe(2);
    }

    [Fact]
    public void Measure_ShorterThanACompleteRender_KeepsTheFirstLines()
    {
        var widget = new SpectreWidget("doc", new CountingRenderable(lineCount: 8), renderOnce: true);
        var context = new LayoutContext();

        widget.Measure(context, new BoxConstraints(0, 40, 0, 10).WithUnboundedHeight()).Height.ShouldBe(8);
        var size = widget.Measure(context, new BoxConstraints(0, 40, 0, 3));

        size.Height.ShouldBe(3);
        RenderLines(new LayoutEngine().Layout(widget, new BoxConstraints(0, 40, 0, 3))).Take(3).ShouldBe(["line 0", "line 1", "line 2"]);
    }

    [Fact]
    public void Measure_TallerThanACutRender_RendersAgain()
    {
        var renderable = new CountingRenderable(lineCount: 8);
        var widget = new SpectreWidget("doc", renderable, renderOnce: true);
        var context = new LayoutContext();

        widget.Measure(context, new BoxConstraints(0, 40, 0, 3)).Height.ShouldBe(3);
        widget.Measure(context, new BoxConstraints(0, 40, 0, 6)).Height.ShouldBe(6);

        renderable.Renders.ShouldBe(2);
    }

    [Fact]
    public void WithoutRenderOnce_RendersOnEveryPaint()
    {
        var renderable = new CountingRenderable(lineCount: 1);
        var result = new LayoutEngine().Layout(new SpectreWidget("live", renderable), new BoxConstraints(0, 10, 0, 1));
        var rendersAfterLayout = renderable.Renders;

        Paint(result);
        Paint(result);

        renderable.Renders.ShouldBe(rendersAfterLayout + 2);
    }

    private static void Paint(LayoutResult result)
        => RenderLines(result);

    private static string[] RenderLines(LayoutResult result)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
        var options = new RenderOptions(console.Profile.Capabilities, new Spectre.Console.Size(40, 25));
        var text = string.Concat(result.PaintToRenderable().Render(options, 40).Select(s => s.IsLineBreak ? "\n" : s.Text));
        return text.Split('\n').Select(line => line.TrimEnd()).ToArray();
    }

    private sealed class CountingRenderable(int lineCount) : IRenderable
    {
        public int Renders { get; private set; }

        public Measurement Measure(RenderOptions options, int maxWidth) => new(6, 6);

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            Renders++;
            for (var index = 0; index < lineCount; index++)
            {
                yield return new Segment($"line {index}");
                yield return Segment.LineBreak;
            }
        }
    }
}
