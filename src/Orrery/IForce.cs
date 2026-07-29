namespace Orrery;

/// <summary>A rule that nudges node velocities (or positions) once per simulation tick.</summary>
public interface IForce
{
    /// <summary>
    /// Called when the force is added to a simulation, and again whenever the node set changes.
    /// Precompute per-node state here rather than per tick.
    /// </summary>
    /// <param name="nodes">The live node list; it is mutated in place by the simulation.</param>
    /// <param name="random">The simulation's seeded generator. Use it for any random decision.</param>
    void Initialize(IReadOnlyList<GraphNode> nodes, Func<double> random);

    /// <summary>Applies the force for one tick.</summary>
    /// <param name="alpha">
    /// The simulation temperature, decaying from 1 toward <see cref="ForceSimulation.AlphaTarget"/>.
    /// Most forces scale their effect by it so the layout cools into place.
    /// </param>
    void Apply(double alpha);
}

/// <summary>Convenience base holding the node list and the seeded generator.</summary>
public abstract class ForceBase : IForce
{
    /// <summary>The live node list. Mutated in place by the simulation every tick.</summary>
    protected IReadOnlyList<GraphNode> Nodes { get; private set; } = [];

    /// <summary>The simulation's seeded generator.</summary>
    protected Func<double> Random { get; private set; } = Lcg.Create();

    /// <summary>Set when cached per-node state must be recomputed before the next <see cref="Apply"/>.</summary>
    protected bool IsDirty { get; set; } = true;

    /// <inheritdoc />
    public void Initialize(IReadOnlyList<GraphNode> nodes, Func<double> random)
    {
        Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        Random = random ?? throw new ArgumentNullException(nameof(random));
        IsDirty = true;
        OnInitialize();
    }

    /// <inheritdoc />
    public abstract void Apply(double alpha);

    /// <summary>Called after the node set is attached or replaced.</summary>
    protected virtual void OnInitialize()
    {
    }

    /// <summary>
    /// A vanishingly small random offset, used to break exact coincidence.
    /// </summary>
    /// <remarks>
    /// Two nodes at identical coordinates give a zero separation vector, which divides to infinity
    /// and poisons the whole layout with NaN. Displacing by 1e-6 is invisible but keeps the maths
    /// finite.
    /// </remarks>
    protected double Jiggle() => (Random() - 0.5) * 1e-6;
}
