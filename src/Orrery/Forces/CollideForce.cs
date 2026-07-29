namespace Orrery;

/// <summary>
/// Pushes overlapping nodes apart, treating each as a circle of a given radius.
/// </summary>
/// <remarks>
/// Ported from d3-force (ISC, © 2010-2021 Mike Bostock); see THIRD-PARTY-NOTICES.txt.
/// <para>
/// This is the force that makes a labelled graph legible. Link and repulsion forces balance
/// distances between node <i>centres</i> and know nothing about how much room a node's label
/// actually occupies, so text overlaps long before the layout looks crowded. Give each node a radius
/// covering its rendered bounds and the overlap resolves itself.
/// </para>
/// <para>
/// Unlike most forces this one ignores <c>alpha</c> and acts at full strength for the whole run:
/// an overlap is equally wrong when the layout is hot and when it has settled.
/// </para>
/// </remarks>
public sealed class CollideForce : ForceBase
{
    private Func<GraphNode, int, double> _radius;
    private double[] _radii = [];

    /// <summary>Creates the force with a uniform radius.</summary>
    public CollideForce(double radius = 1) => _radius = (_, _) => radius;

    /// <summary>Creates the force with a per-node radius.</summary>
    public CollideForce(Func<GraphNode, int, double> radius)
        => _radius = radius ?? throw new ArgumentNullException(nameof(radius));

    /// <summary>The exclusion radius of each node.</summary>
    public Func<GraphNode, int, double> Radius
    {
        get => _radius;
        set
        {
            _radius = value ?? throw new ArgumentNullException(nameof(value));
            IsDirty = true;
        }
    }

    /// <summary>How hard overlaps are resolved, in [0, 1]. Lower values let nodes interpenetrate.</summary>
    public double Strength { get; set; } = 1;

    /// <summary>How many times to apply the force per tick. Raise it for densely packed graphs.</summary>
    public int Iterations { get; set; } = 1;

    /// <inheritdoc />
    protected override void OnInitialize() => IsDirty = true;

    /// <inheritdoc />
    public override void Apply(double alpha)
    {
        var count = Nodes.Count;
        if (count < 2) return;
        if (IsDirty) Recompute();

        for (var k = 0; k < Iterations; k++)
        {
            for (var i = 0; i < count; i++)
            {
                var node = Nodes[i];
                var radius = _radii[i];
                var radiusSquared = radius * radius;
                var nodeX = node.X + node.Vx;
                var nodeY = node.Y + node.Vy;

                // Only the higher-indexed half of each pair is visited, so every overlap is resolved
                // once with a symmetric push rather than twice with a double correction.
                for (var j = i + 1; j < count; j++)
                {
                    var other = Nodes[j];
                    var otherRadius = _radii[j];
                    var combined = radius + otherRadius;

                    var x = nodeX - other.X - other.Vx;
                    var y = nodeY - other.Y - other.Vy;
                    var distance = x * x + y * y;
                    if (distance >= combined * combined) continue;

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

                    distance = Math.Sqrt(distance);
                    var scale = (combined - distance) / distance * Strength;
                    x *= scale;
                    y *= scale;

                    // Split the separation by area, so a small node yields to a large one.
                    var otherRadiusSquared = otherRadius * otherRadius;
                    var share = otherRadiusSquared / (radiusSquared + otherRadiusSquared);
                    node.Vx += x * share;
                    node.Vy += y * share;
                    share = 1 - share;
                    other.Vx -= x * share;
                    other.Vy -= y * share;
                }
            }
        }
    }

    private void Recompute()
    {
        _radii = new double[Nodes.Count];
        for (var i = 0; i < Nodes.Count; i++) _radii[i] = _radius(Nodes[i], i);
        IsDirty = false;
    }
}
