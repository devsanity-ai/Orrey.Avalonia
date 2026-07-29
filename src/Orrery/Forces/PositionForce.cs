namespace Orrery;

/// <summary>
/// Draws each node toward a target coordinate on one axis. See <see cref="XForce"/> and <see cref="YForce"/>.
/// </summary>
/// <remarks>
/// Ported from d3-force (ISC, © 2010-2021 Mike Bostock); see THIRD-PARTY-NOTICES.txt.
/// <para>
/// Returning <see cref="double.NaN"/> from <see cref="Target"/> excludes that node from the force
/// entirely. That is how you anchor part of a graph — a timeline column, a fixed root — while
/// leaving the rest free.
/// </para>
/// </remarks>
public abstract class PositionForce : ForceBase
{
    private Func<GraphNode, int, double> _target = static (_, _) => 0;
    private Func<GraphNode, int, double> _strength = static (_, _) => 0.1;
    private double[] _targets = [];
    private double[] _strengths = [];

    /// <summary>The coordinate each node is drawn toward. <see cref="double.NaN"/> exempts a node.</summary>
    public Func<GraphNode, int, double> Target
    {
        get => _target;
        set
        {
            _target = value ?? throw new ArgumentNullException(nameof(value));
            IsDirty = true;
        }
    }

    /// <summary>How strongly each node is drawn toward its target. Defaults to 0.1.</summary>
    public Func<GraphNode, int, double> Strength
    {
        get => _strength;
        set
        {
            _strength = value ?? throw new ArgumentNullException(nameof(value));
            IsDirty = true;
        }
    }

    /// <inheritdoc />
    protected override void OnInitialize() => IsDirty = true;

    /// <inheritdoc />
    public override void Apply(double alpha)
    {
        if (Nodes.Count == 0) return;
        if (IsDirty) Recompute();

        for (var i = 0; i < Nodes.Count; i++)
        {
            Push(Nodes[i], _targets[i], _strengths[i], alpha);
        }
    }

    /// <summary>Applies the accumulated pull to whichever axis the concrete force governs.</summary>
    protected abstract void Push(GraphNode node, double target, double strength, double alpha);

    private void Recompute()
    {
        _targets = new double[Nodes.Count];
        _strengths = new double[Nodes.Count];

        for (var i = 0; i < Nodes.Count; i++)
        {
            var target = _target(Nodes[i], i);
            _targets[i] = target;
            _strengths[i] = double.IsNaN(target) ? 0 : _strength(Nodes[i], i);
        }

        IsDirty = false;
    }
}

/// <summary>Draws each node toward a target horizontal position.</summary>
public sealed class XForce : PositionForce
{
    /// <summary>Creates the force targeting a single coordinate for every node.</summary>
    public XForce(double target = 0) => Target = (_, _) => target;

    /// <summary>Creates the force with a per-node target.</summary>
    public XForce(Func<GraphNode, int, double> target) => Target = target;

    /// <inheritdoc />
    protected override void Push(GraphNode node, double target, double strength, double alpha)
    {
        if (strength == 0) return;
        node.Vx += (target - node.X) * strength * alpha;
    }
}

/// <summary>Draws each node toward a target vertical position.</summary>
public sealed class YForce : PositionForce
{
    /// <summary>Creates the force targeting a single coordinate for every node.</summary>
    public YForce(double target = 0) => Target = (_, _) => target;

    /// <summary>Creates the force with a per-node target.</summary>
    public YForce(Func<GraphNode, int, double> target) => Target = target;

    /// <inheritdoc />
    protected override void Push(GraphNode node, double target, double strength, double alpha)
    {
        if (strength == 0) return;
        node.Vy += (target - node.Y) * strength * alpha;
    }
}
