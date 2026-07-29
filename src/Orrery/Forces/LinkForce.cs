namespace Orrery;

/// <summary>
/// Pulls linked nodes toward a target separation, like a spring along every edge.
/// </summary>
/// <remarks>
/// Ported from d3-force (ISC, © 2010-2021 Mike Bostock); see THIRD-PARTY-NOTICES.txt.
/// <para>
/// Two details make this behave far better than a naive Hooke spring, and both are worth knowing
/// because they remove work you would otherwise do by hand:
/// </para>
/// <para>
/// <b>Displacement is split by degree.</b> Each endpoint absorbs a share of the correction weighted
/// by the other's degree, so a hub with many edges barely moves while a leaf swings freely. Dragging
/// a hub therefore drags its whole web without the hub being knocked around by it — no special-casing
/// or manual pinning required.
/// </para>
/// <para>
/// <b>Default strength falls off with degree</b> (<c>1 / min(deg(source), deg(target))</c>), so a node
/// with twenty edges is not crushed by twenty springs all pulling at full force.
/// </para>
/// </remarks>
public sealed class LinkForce : ForceBase
{
    private readonly List<GraphLink> _links;
    private Func<GraphLink, int, double> _distance = static (_, _) => 30;
    private Func<GraphLink, int, double>? _strength;
    private double[] _strengths = [];
    private double[] _distances = [];
    private double[] _bias = [];
    private int[] _degrees = [];

    /// <summary>Creates the force over a set of links.</summary>
    public LinkForce(IEnumerable<GraphLink> links)
    {
        ArgumentNullException.ThrowIfNull(links);
        _links = [.. links];
    }

    /// <summary>Creates the force over a graph's links.</summary>
    public LinkForce(Graph graph)
        : this((graph ?? throw new ArgumentNullException(nameof(graph))).Links)
    {
    }

    /// <summary>The links this force acts on.</summary>
    public IReadOnlyList<GraphLink> Links => _links;

    /// <summary>
    /// How many times to apply the force per tick. Raising it makes the target distances hold more
    /// rigidly, at the cost of a springier, less organic feel.
    /// </summary>
    public int Iterations { get; set; } = 1;

    /// <summary>The separation each link pulls toward, given the link and its index. Defaults to 30.</summary>
    public Func<GraphLink, int, double> Distance
    {
        get => _distance;
        set
        {
            _distance = value ?? throw new ArgumentNullException(nameof(value));
            IsDirty = true;
        }
    }

    /// <summary>
    /// Spring stiffness, given the link and its index. Leave <c>null</c> for the degree-aware default.
    /// </summary>
    public Func<GraphLink, int, double>? Strength
    {
        get => _strength;
        set
        {
            _strength = value;
            IsDirty = true;
        }
    }

    /// <summary>Replaces the link set and recomputes degrees.</summary>
    public void SetLinks(IEnumerable<GraphLink> links)
    {
        ArgumentNullException.ThrowIfNull(links);
        _links.Clear();
        _links.AddRange(links);
        OnInitialize();
    }

    /// <inheritdoc />
    protected override void OnInitialize()
    {
        var nodeCount = Nodes.Count;
        _degrees = new int[nodeCount];
        _bias = new double[_links.Count];

        for (var i = 0; i < _links.Count; i++)
        {
            var link = _links[i];
            link.Index = i;
            if (IsAttached(link.Source)) _degrees[link.Source.Index]++;
            if (IsAttached(link.Target)) _degrees[link.Target.Index]++;
        }

        for (var i = 0; i < _links.Count; i++)
        {
            var link = _links[i];
            double source = IsAttached(link.Source) ? _degrees[link.Source.Index] : 0;
            double target = IsAttached(link.Target) ? _degrees[link.Target.Index] : 0;
            var total = source + target;
            _bias[i] = total > 0 ? source / total : 0.5;
        }

        IsDirty = true;
    }

    /// <inheritdoc />
    public override void Apply(double alpha)
    {
        if (_links.Count == 0) return;
        if (IsDirty) Recompute();

        for (var k = 0; k < Iterations; k++)
        {
            for (var i = 0; i < _links.Count; i++)
            {
                var link = _links[i];
                var source = link.Source;
                var target = link.Target;

                var x = target.X + target.Vx - source.X - source.Vx;
                if (x == 0 || double.IsNaN(x)) x = Jiggle();
                var y = target.Y + target.Vy - source.Y - source.Vy;
                if (y == 0 || double.IsNaN(y)) y = Jiggle();

                var length = Math.Sqrt(x * x + y * y);
                var scale = (length - _distances[i]) / length * alpha * _strengths[i];
                x *= scale;
                y *= scale;

                var bias = _bias[i];
                target.Vx -= x * bias;
                target.Vy -= y * bias;
                bias = 1 - bias;
                source.Vx += x * bias;
                source.Vy += y * bias;
            }
        }
    }

    private void Recompute()
    {
        _strengths = new double[_links.Count];
        _distances = new double[_links.Count];

        for (var i = 0; i < _links.Count; i++)
        {
            var link = _links[i];
            _strengths[i] = _strength is not null ? _strength(link, i) : DefaultStrength(link);
            _distances[i] = _distance(link, i);
        }

        IsDirty = false;
    }

    private double DefaultStrength(GraphLink link)
    {
        var source = IsAttached(link.Source) ? _degrees[link.Source.Index] : 1;
        var target = IsAttached(link.Target) ? _degrees[link.Target.Index] : 1;
        var lowest = Math.Min(source, target);
        return lowest > 0 ? 1.0 / lowest : 1;
    }

    private bool IsAttached(GraphNode node) => node.Index >= 0 && node.Index < _degrees.Length;
}
