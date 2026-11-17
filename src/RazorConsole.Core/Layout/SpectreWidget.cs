// Copyright (c) RazorConsole. All rights reserved.

using Spectre.Console;
using Spectre.Console.Rendering;

namespace RazorConsole.Core.Layout;

public sealed class SpectreWidget : Widget
{
    private readonly IRenderable _renderable;
    private readonly bool _renderOnce;
    private int _lastMeasuredMaxWidth;
    private RenderedLines? _rendered;

    /// <param name="vnodeId">The id of the node this widget renders.</param>
    /// <param name="renderable">The Spectre.Console renderable to draw.</param>
    /// <param name="key">The node's key.</param>
    /// <param name="attributes">The node's attributes.</param>
    /// <param name="zIndex">The paint order among siblings.</param>
    /// <param name="renderOnce">
    /// The renderable's output depends only on the width and height it is given: render it once per size and reuse the lines
    /// for measuring, painting and repainting. Off by default, so a renderable that changes between paints is drawn anew each time.
    /// </param>
    public SpectreWidget(
        string vnodeId,
        IRenderable renderable,
        string? key = null,
        IReadOnlyDictionary<string, string?>? attributes = null,
        int zIndex = 0,
        bool renderOnce = false)
        : base(vnodeId, key, attributes, zIndex: zIndex)
    {
        _renderable = renderable ?? throw new ArgumentNullException(nameof(renderable));
        _renderOnce = renderOnce;
    }

    protected override LayoutSize MeasureCore(LayoutContext context, BoxConstraints constraints)
    {
        if (constraints.MaxWidth == 0 || constraints.MaxHeight == 0)
        {
            return constraints.Constrain(LayoutSize.Empty);
        }

        _lastMeasuredMaxWidth = constraints.MaxWidth;
        var lines = RenderLines(constraints.MaxWidth, constraints.MaxHeight);
        var width = lines
            .Select(line => Segment.CellCount(line.Segments))
            .DefaultIfEmpty(0)
            .Max();
        var height = Math.Min(lines.Count, constraints.MaxHeight);

        return constraints.Constrain(new LayoutSize(Math.Min(width, constraints.MaxWidth), height));
    }

    protected override void ArrangeCore(LayoutContext context, LayoutRect bounds)
    {
    }

    protected override void PaintCore(PaintContext context)
    {
        if (Bounds.IsEmpty)
        {
            return;
        }

        var renderWidth = Math.Max(Bounds.Width, _lastMeasuredMaxWidth);
        var lines = RenderLines(renderWidth, Bounds.Height);
        var maxLines = Math.Min(Bounds.Height, lines.Count);
        for (var row = 0; row < maxLines; row++)
        {
            var line = lines[row];
            context.Canvas.WriteSegments(Bounds.X, Bounds.Y + row, line.Segments, Bounds.Width);
        }
    }

    private IReadOnlyList<RenderedLine> RenderLines(int maxWidth, int maxHeight)
    {
        if (maxWidth <= 0 || maxHeight <= 0)
        {
            return [];
        }

        if (!_renderOnce)
        {
            return RenderAllLines(maxWidth, maxHeight);
        }

        // Measure, paint and every animation frame repaint ask for the same lines; rendering them each time
        // costs the whole renderable per pass, which a long syntax-highlighted document makes slower than a frame.
        if (_rendered is { } rendered && rendered.Covers(maxWidth, maxHeight))
        {
            return rendered.Take(maxHeight);
        }

        var lines = RenderAllLines(maxWidth, maxHeight);
        _rendered = new RenderedLines(maxWidth, maxHeight, lines);
        return lines;
    }

    private List<RenderedLine> RenderAllLines(int maxWidth, int maxHeight)
    {
        var options = CreateRenderOptions(maxWidth, maxHeight);
        var lines = new List<RenderedLine>();
        var sourceLines = Segment.SplitLines(_renderable.Render(options, maxWidth));

        foreach (var line in sourceLines)
        {
            AppendWrappedLine(line, maxWidth, maxHeight, lines);
            if (lines.Count >= maxHeight)
            {
                break;
            }
        }

        return lines;
    }

    private static void AppendWrappedLine(
        IReadOnlyList<Segment> source,
        int maxWidth,
        int maxHeight,
        List<RenderedLine> lines)
    {
        var current = new List<Segment>();
        var currentWidth = 0;

        foreach (var segment in source)
        {
            AppendWrappedSegment(segment, maxWidth, maxHeight, lines, current, ref currentWidth);
            if (lines.Count >= maxHeight)
            {
                return;
            }
        }

        if (lines.Count < maxHeight)
        {
            lines.Add(new RenderedLine(current.ToArray()));
        }
    }

    private static void AppendWrappedSegment(
        Segment segment,
        int maxWidth,
        int maxHeight,
        List<RenderedLine> lines,
        List<Segment> current,
        ref int currentWidth)
    {
        if (string.IsNullOrEmpty(segment.Text))
        {
            current.Add(segment);
            return;
        }

        foreach (var rune in segment.Text.EnumerateRunes())
        {
            var text = rune.ToString();
            var width = Math.Max(1, Segment.CellCount([new Segment(text)]));
            if (currentWidth > 0 && currentWidth + width > maxWidth)
            {
                lines.Add(new RenderedLine(current.ToArray()));
                current.Clear();
                currentWidth = 0;
                if (lines.Count >= maxHeight)
                {
                    return;
                }
            }

            current.Add(new Segment(text, segment.Style, segment.Link));
            currentWidth += width;
        }
    }

    private static RenderOptions CreateRenderOptions(int maxWidth, int maxHeight)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });

        return new RenderOptions(console.Profile.Capabilities, new Spectre.Console.Size(maxWidth, Math.Max(1, maxHeight)));
    }

    private sealed record RenderedLine(IReadOnlyList<Segment> Segments);

    /// <summary>
    /// Lines rendered at one width and height.
    /// </summary>
    private sealed record RenderedLines(int Width, int Height, IReadOnlyList<RenderedLine> Lines)
    {
        /// <summary>
        /// Whether these lines answer a render at <paramref name="width"/> and <paramref name="height"/>:
        /// the same width and a height they were cut at or below, or any height when the output ended first.
        /// </summary>
        public bool Covers(int width, int height)
        {
            if (width != Width)
            {
                return false;
            }

            return height <= Height || Lines.Count < Height;
        }

        public IReadOnlyList<RenderedLine> Take(int height)
        {
            if (Lines.Count <= height)
            {
                return Lines;
            }

            return Lines.Take(height).ToArray();
        }
    }
}
