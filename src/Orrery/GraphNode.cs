namespace Orrery;

/// <summary>
/// A particle in a <see cref="ForceSimulation"/> and a vertex in a <see cref="Graph"/>.
/// </summary>
/// <remarks>
/// Position and velocity are mutated in place by the forces every tick, so this type is
/// deliberately a mutable class rather than a record or struct.
/// <para>
/// <see cref="X"/> and <see cref="Y"/> default to <see cref="double.NaN"/>, which means
/// "not yet placed". The simulation seeds any such node onto a phyllotaxis spiral when the
/// node set is attached. This mirrors d3-force, where an unset coordinate is
/// <c>undefined</c> and fails its <c>isNaN</c> check — a plain <c>0</c> default would instead
/// stack every node on the origin, where the repulsion force degenerates.
/// </para>
/// </remarks>
public sealed class GraphNode
{
    /// <summary>Creates a node.</summary>
    /// <param name="id">Identity, unique within a <see cref="Graph"/>.</param>
    /// <param name="data">Arbitrary payload — typically the view model this node represents.</param>
    public GraphNode(string id, object? data = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Data = data;
    }

    /// <summary>Identity, unique within a <see cref="Graph"/>.</summary>
    public string Id { get; }

    /// <summary>Arbitrary payload — typically the view model this node represents.</summary>
    public object? Data { get; set; }

    /// <summary>Position within the node set, assigned by the simulation. -1 when unattached.</summary>
    public int Index { get; internal set; } = -1;

    /// <summary>Horizontal position. <see cref="double.NaN"/> until seeded.</summary>
    public double X { get; set; } = double.NaN;

    /// <summary>Vertical position. <see cref="double.NaN"/> until seeded.</summary>
    public double Y { get; set; } = double.NaN;

    /// <summary>Horizontal velocity, in units per tick.</summary>
    public double Vx { get; set; }

    /// <summary>Vertical velocity, in units per tick.</summary>
    public double Vy { get; set; }

    /// <summary>
    /// When set, <see cref="X"/> is forced to this value every tick and <see cref="Vx"/> is zeroed.
    /// This is how dragging works: pin while the pointer is down, release on pointer up.
    /// </summary>
    public double? Fx { get; set; }

    /// <summary>Vertical counterpart of <see cref="Fx"/>.</summary>
    public double? Fy { get; set; }

    /// <summary>True when the node is held in place and ignores the forces acting on it.</summary>
    public bool IsPinned => Fx.HasValue || Fy.HasValue;

    /// <summary>Holds the node at a fixed point until <see cref="Unpin"/> is called.</summary>
    public void Pin(double x, double y)
    {
        Fx = x;
        Fy = y;
    }

    /// <summary>Releases a pinned node back to the simulation, keeping its current position.</summary>
    public void Unpin()
    {
        Fx = null;
        Fy = null;
    }

    /// <summary>Returns <see cref="Data"/> as <typeparamref name="T"/>, or <c>null</c> if it is something else.</summary>
    public T? DataAs<T>() where T : class => Data as T;

    /// <inheritdoc />
    public override string ToString() => $"{Id} ({X:F1}, {Y:F1})";
}
