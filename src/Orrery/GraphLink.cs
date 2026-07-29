namespace Orrery;

/// <summary>An edge between two <see cref="GraphNode"/>s.</summary>
/// <remarks>
/// Links are undirected as far as the physics is concerned — <see cref="LinkForce"/> pulls both
/// endpoints toward each other. Direction is presentation: a renderer may draw an arrowhead at
/// <see cref="Target"/>.
/// </remarks>
public sealed class GraphLink
{
    /// <summary>Creates a link between two nodes.</summary>
    /// <param name="source">One endpoint.</param>
    /// <param name="target">The other endpoint; the arrow end, by convention.</param>
    /// <param name="data">Arbitrary payload — typically the relationship this edge represents.</param>
    public GraphLink(GraphNode source, GraphNode target, object? data = null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Data = data;
    }

    /// <summary>One endpoint.</summary>
    public GraphNode Source { get; }

    /// <summary>The other endpoint; the arrow end, by convention.</summary>
    public GraphNode Target { get; }

    /// <summary>Arbitrary payload — typically the relationship this edge represents.</summary>
    public object? Data { get; set; }

    /// <summary>Position within the link set, assigned by <see cref="Graph"/>. -1 when unattached.</summary>
    public int Index { get; internal set; } = -1;

    /// <summary>Returns the endpoint that is not <paramref name="node"/>, or <c>null</c> if it is neither.</summary>
    public GraphNode? Other(GraphNode node) =>
        ReferenceEquals(node, Source) ? Target : ReferenceEquals(node, Target) ? Source : null;

    /// <summary>Returns <see cref="Data"/> as <typeparamref name="T"/>, or <c>null</c> if it is something else.</summary>
    public T? DataAs<T>() where T : class => Data as T;

    /// <inheritdoc />
    public override string ToString() => $"{Source.Id} -> {Target.Id}";
}
