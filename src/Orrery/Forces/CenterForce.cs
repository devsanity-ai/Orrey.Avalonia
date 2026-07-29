namespace Orrery;

/// <summary>
/// Keeps the mean of all node positions at a fixed point by translating the whole layout.
/// </summary>
/// <remarks>
/// Ported from d3-force (ISC, © 2010-2021 Mike Bostock); see THIRD-PARTY-NOTICES.txt.
/// <para>
/// This force moves positions directly rather than accumulating velocity, and ignores <c>alpha</c>.
/// It adds no energy — it only stops the layout drifting off-canvas, which repulsion alone would
/// otherwise allow. Because it is a rigid translation it cannot fight the other forces, so it is
/// safe to combine with anything.
/// </para>
/// <para>
/// <b>It is not safe to combine with pinned nodes, however.</b> Centering translates every node to
/// hold the centroid, and the simulation then restores each pinned node to its pin immediately
/// afterward. The two never reconcile, so every tick walks the free nodes a little further away
/// while the pinned ones stay put — a dragged node ends up stranded as its graph slides off. Since
/// dragging is implemented as pinning, any interactive layout hits this. Use <see cref="XForce"/> and
/// <see cref="YForce"/> instead: they are soft and per-node, so a dragged node pulls its neighbours
/// along rather than being abandoned by them.
/// </para>
/// </remarks>
public sealed class CenterForce : ForceBase
{
    /// <summary>Creates the force targeting a point, the origin by default.</summary>
    public CenterForce(double x = 0, double y = 0)
    {
        X = x;
        Y = y;
    }

    /// <summary>Horizontal position the centre of mass is held at.</summary>
    public double X { get; set; }

    /// <summary>Vertical position the centre of mass is held at.</summary>
    public double Y { get; set; }

    /// <summary>How much of the offset is corrected each tick, in [0, 1].</summary>
    public double Strength { get; set; } = 1;

    /// <inheritdoc />
    public override void Apply(double alpha)
    {
        var count = Nodes.Count;
        if (count == 0) return;

        double sumX = 0;
        double sumY = 0;
        foreach (var node in Nodes)
        {
            sumX += node.X;
            sumY += node.Y;
        }

        var shiftX = (sumX / count - X) * Strength;
        var shiftY = (sumY / count - Y) * Strength;

        foreach (var node in Nodes)
        {
            node.X -= shiftX;
            node.Y -= shiftY;
        }
    }
}
