namespace Orrery;

/// <summary>
/// An n-body force acting between every pair of nodes: negative strength repels (the usual case,
/// spreading a graph out), positive strength attracts.
/// </summary>
/// <remarks>
/// Ported from d3-force (ISC, © 2010-2021 Mike Bostock); see THIRD-PARTY-NOTICES.txt.
/// <para>
/// This implementation evaluates every pair directly, which is O(n²) per tick. d3 uses a Barnes-Hut
/// quadtree to approximate distant clusters; for the graph sizes this library targets the exact
/// version is both faster in practice and far simpler, since the quadtree's build cost dominates
/// until roughly two hundred nodes. Swap in an approximation only when a real graph proves it
/// necessary.
/// </para>
/// </remarks>
public sealed class ManyBodyForce : ForceBase
{
    private Func<GraphNode, int, double> _strength = static (_, _) => -30;
    private double[] _strengths = [];

    /// <summary>Per-node strength. Negative repels, positive attracts. Defaults to -30.</summary>
    public Func<GraphNode, int, double> Strength
    {
        get => _strength;
        set
        {
            _strength = value ?? throw new ArgumentNullException(nameof(value));
            IsDirty = true;
        }
    }

    /// <summary>
    /// Separations below this are clamped. Without it, two nearly coincident nodes would fling each
    /// other across the canvas as the inverse-square term blows up.
    /// </summary>
    public double DistanceMin { get; set; } = 1;

    /// <summary>Pairs further apart than this ignore each other. Infinite by default.</summary>
    public double DistanceMax { get; set; } = double.PositiveInfinity;

    /// <inheritdoc />
    protected override void OnInitialize() => IsDirty = true;

    /// <inheritdoc />
    public override void Apply(double alpha)
    {
        var count = Nodes.Count;
        if (count < 2) return;
        if (IsDirty) Recompute();

        var minimum = DistanceMin * DistanceMin;
        var maximum = double.IsPositiveInfinity(DistanceMax) ? double.PositiveInfinity : DistanceMax * DistanceMax;

        for (var i = 0; i < count; i++)
        {
            var node = Nodes[i];

            for (var j = 0; j < count; j++)
            {
                if (i == j) continue;
                var other = Nodes[j];

                var x = other.X - node.X;
                var y = other.Y - node.Y;
                var distance = x * x + y * y;

                if (x == 0)
                {
                    x = Jiggle();
                    distance += x * x;
                }

                if (y == 0)
                {
                    y = Jiggle();
                    distance += y * y;
                }

                if (distance >= maximum) continue;
                if (distance < minimum) distance = Math.Sqrt(minimum * distance);

                var weight = _strengths[j] * alpha / distance;
                node.Vx += x * weight;
                node.Vy += y * weight;
            }
        }
    }

    private void Recompute()
    {
        _strengths = new double[Nodes.Count];
        for (var i = 0; i < Nodes.Count; i++) _strengths[i] = _strength(Nodes[i], i);
        IsDirty = false;
    }
}
