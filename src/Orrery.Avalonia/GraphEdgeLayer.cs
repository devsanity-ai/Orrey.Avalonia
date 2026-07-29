using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orrery.Avalonia;

/// <summary>
/// Draws the edges beneath the node visuals. Owned and fed by <see cref="GraphView"/>.
/// </summary>
/// <remarks>
/// This is a separate control because Avalonia seals <c>Panel.Render</c>: a panel cannot paint
/// behind its own children, so the edges have to live in a sibling drawn first.
/// <para>
/// It performs no layout maths of its own. <see cref="GraphView"/> arranges the nodes, records where
/// they actually landed, and hands those pixel positions over — so edges cannot drift away from the
/// nodes they connect, which is the classic failure when two controls each compute the transform.
/// </para>
/// </remarks>
public sealed class GraphEdgeLayer : Control
{
    private IReadOnlyList<GraphLink> _links = [];
    private IReadOnlyDictionary<GraphNode, GraphNodeVisual> _visuals =
        new Dictionary<GraphNode, GraphNodeVisual>();
    private IReadOnlySet<GraphLink>? _highlighted;

    /// <summary>Creates the layer. It never takes pointer input; the host view handles that.</summary>
    public GraphEdgeLayer() => IsHitTestVisible = false;

    /// <summary>Default line colour.</summary>
    public IBrush Brush { get; set; } = Brushes.SlateGray;

    /// <summary>Colour of edge label text.</summary>
    public IBrush LabelBrush { get; set; } = Brushes.Gray;

    /// <summary>Fill drawn behind edge label text, so a label stays readable over an edge.</summary>
    public IBrush? LabelBackground { get; set; }

    /// <summary>Default line width.</summary>
    public double Thickness { get; set; } = 1.2;

    /// <summary>
    /// How far edges bow away from a straight line, as a fraction of their length.
    /// A little curvature separates edges that would otherwise coincide.
    /// </summary>
    public double Curvature { get; set; } = 0.14;

    /// <summary>Gap left between a node's boundary and the end of its edges.</summary>
    public double NodeMargin { get; set; } = 6;

    /// <summary>Opacity of the edge that currently has focus.</summary>
    public double FocusedOpacity { get; set; } = 0.85;

    /// <summary>Opacity of edges when nothing has focus.</summary>
    public double RestingOpacity { get; set; } = 0.34;

    /// <summary>Opacity of edges pushed back because something else has focus.</summary>
    public double DimmedOpacity { get; set; } = 0.10;

    /// <summary>Font used for edge labels.</summary>
    public Typeface Typeface { get; set; } = Typeface.Default;

    /// <summary>Point size of edge labels.</summary>
    public double FontSize { get; set; } = 9.5;

    /// <summary>Resolves the text drawn on an edge. Return null or empty to draw none.</summary>
    public Func<GraphLink, string?>? LabelSelector { get; set; }

    /// <summary>Resolves per-edge appearance overrides.</summary>
    public Func<GraphLink, GraphEdgeStyle?>? StyleSelector { get; set; }

    /// <summary>Replaces the geometry to draw and requests a repaint.</summary>
    /// <param name="links">Edges to draw.</param>
    /// <param name="visuals">Where each node was arranged, keyed by node.</param>
    /// <param name="highlighted">
    /// Edges to draw with focus. Null means nothing has focus and every edge rests; an empty set
    /// means something has focus but no edge qualifies, so all of them fade.
    /// </param>
    public void Update(
        IReadOnlyList<GraphLink> links,
        IReadOnlyDictionary<GraphNode, GraphNodeVisual> visuals,
        IReadOnlySet<GraphLink>? highlighted)
    {
        _links = links;
        _visuals = visuals;
        _highlighted = highlighted;
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (_links.Count == 0) return;

        var anyFocus = _highlighted is not null;

        foreach (var link in _links)
        {
            if (!_visuals.TryGetValue(link.Source, out var source)) continue;
            if (!_visuals.TryGetValue(link.Target, out var target)) continue;

            var deltaX = target.Center.X - source.Center.X;
            var deltaY = target.Center.Y - source.Center.Y;
            var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (length < 1) continue;

            var focused = anyFocus && _highlighted!.Contains(link);
            var faded = anyFocus && !focused;

            var style = StyleSelector?.Invoke(link);
            var opacity = focused ? FocusedOpacity : faded ? DimmedOpacity : RestingOpacity;
            var width = (style?.Thickness ?? Thickness) * (focused ? 2.2 : faded ? 0.7 : 1);
            var brush = style?.Brush ?? Brush;

            var start = source.BoundaryPoint(deltaX, deltaY, NodeMargin);
            var end = target.BoundaryPoint(-deltaX, -deltaY, NodeMargin);

            // Bow the edge perpendicular to its own direction.
            var bow = length * Curvature;
            var control = new Point(
                (start.X + end.X) / 2 - deltaY / length * bow,
                (start.Y + end.Y) / 2 + deltaX / length * bow);

            var pen = CreatePen(brush, opacity, width, style?.IsDashed ?? false);
            var geometry = new StreamGeometry();
            using (var sink = geometry.Open())
            {
                sink.BeginFigure(start, false);
                sink.QuadraticBezierTo(control, end);
                sink.EndFigure(false);
            }

            context.DrawGeometry(null, pen, geometry);

            if (style?.ShowArrowhead ?? true)
            {
                DrawArrowhead(context, control, end, brush, opacity, focused ? 7 : 5);
            }

            // A faded edge drops its label so the focused one is the only thing left to read.
            if (faded) continue;

            var label = style?.Label ?? LabelSelector?.Invoke(link);
            if (string.IsNullOrWhiteSpace(label)) continue;

            DrawLabel(context, label, start, control, end, focused);
        }
    }

    private static IPen CreatePen(IBrush brush, double opacity, double width, bool dashed)
    {
        var stroke = brush is ISolidColorBrush solid
            ? new ImmutableSolidColorBrush(solid.Color, opacity * solid.Opacity)
            : brush;

        return dashed
            ? new Pen(stroke, width, new DashStyle([3, 3], 0))
            : new Pen(stroke, width);
    }

    private static void DrawArrowhead(
        DrawingContext context, Point from, Point to, IBrush brush, double opacity, double size)
    {
        var deltaX = to.X - from.X;
        var deltaY = to.Y - from.Y;
        var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (length < 0.5) return;

        var unitX = deltaX / length;
        var unitY = deltaY / length;
        var back = new Point(to.X - unitX * size, to.Y - unitY * size);
        var half = size * 0.45;

        var geometry = new StreamGeometry();
        using (var sink = geometry.Open())
        {
            sink.BeginFigure(to, true);
            sink.LineTo(new Point(back.X - unitY * half, back.Y + unitX * half));
            sink.LineTo(new Point(back.X + unitY * half, back.Y - unitX * half));
            sink.EndFigure(true);
        }

        var fill = brush is ISolidColorBrush solid
            ? new ImmutableSolidColorBrush(solid.Color, opacity * solid.Opacity)
            : brush;

        context.DrawGeometry(fill, null, geometry);
    }

    private void DrawLabel(DrawingContext context, string label, Point start, Point control, Point end, bool focused)
    {
        var text = new FormattedText(
            label,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface,
            focused ? FontSize + 1 : FontSize,
            focused ? Brush : LabelBrush);

        // Midpoint of the quadratic curve, not of the chord, so the label sits on the line.
        var anchor = new Point(
            (start.X + 2 * control.X + end.X) / 4,
            (start.Y + 2 * control.Y + end.Y) / 4);

        var box = new Rect(
            anchor.X - text.Width / 2 - 4,
            anchor.Y - text.Height / 2 - 1,
            text.Width + 8,
            text.Height + 2);

        if (LabelBackground is not null)
        {
            context.DrawRectangle(LabelBackground, null, new RoundedRect(box, 4));
        }

        context.DrawText(text, new Point(box.X + 4, box.Y + 1));
    }
}
