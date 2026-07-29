using Avalonia;
using Avalonia.Media;

namespace Orrery.Avalonia;

/// <summary>The arranged geometry of one node, in pixels. Produced by <see cref="GraphView"/>.</summary>
/// <param name="Node">The simulation node.</param>
/// <param name="Center">Centre of the node's visual, in view pixels.</param>
/// <param name="HalfWidth">Half the arranged width, used to trim edges to the node's boundary.</param>
/// <param name="HalfHeight">Half the arranged height.</param>
/// <param name="AnchorRadius">Optional radius, in pixels, that edges meet instead of the arranged bounds.</param>
public readonly record struct GraphNodeVisual(
    GraphNode Node, Point Center, double HalfWidth, double HalfHeight, double? AnchorRadius = null)
{
    /// <summary>
    /// The point on this node's boundary in the given direction, treating the node as an ellipse
    /// inscribed in its arranged bounds — or a circle of <see cref="AnchorRadius"/> when one is given.
    /// </summary>
    /// <remarks>
    /// Trimming to the node's real arranged size is what lets any template be used. A fixed radius
    /// would leave a gap under a small node and bury the arrowhead under a large one.
    /// <para>
    /// Those bounds are the wrong target when a template puts chrome around its visible shape, though: a
    /// caption or a row of buttons stacked under a circle inflates the box, and edges then stop well short
    /// of the thing they appear to connect. <see cref="AnchorRadius"/> lets the host name the shape edges
    /// should actually meet.
    /// </para>
    /// </remarks>
    public Point BoundaryPoint(double directionX, double directionY, double margin = 0)
    {
        var halfWidth = Math.Max(1, AnchorRadius ?? HalfWidth) + margin;
        var halfHeight = Math.Max(1, AnchorRadius ?? HalfHeight) + margin;
        var scaledX = directionX / halfWidth;
        var scaledY = directionY / halfHeight;
        var denominator = Math.Sqrt(scaledX * scaledX + scaledY * scaledY);
        if (denominator < 1e-9) return Center;

        var distance = 1 / denominator;
        return new Point(Center.X + directionX * distance, Center.Y + directionY * distance);
    }
}

/// <summary>Per-edge appearance overrides, returned from <see cref="GraphView.EdgeStyleSelector"/>.</summary>
public sealed class GraphEdgeStyle
{
    /// <summary>Line colour. Falls back to <see cref="GraphView.EdgeBrush"/>.</summary>
    public IBrush? Brush { get; set; }

    /// <summary>Line width. Falls back to <see cref="GraphView.EdgeThickness"/>.</summary>
    public double? Thickness { get; set; }

    /// <summary>Draw the edge dashed — conventionally, a proposed or uncertain relationship.</summary>
    public bool IsDashed { get; set; }

    /// <summary>Draw an arrowhead at the target end.</summary>
    public bool ShowArrowhead { get; set; } = true;

    /// <summary>Text drawn on the edge. Overrides <see cref="GraphView.EdgeLabelSelector"/>.</summary>
    public string? Label { get; set; }
}
