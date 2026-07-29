namespace Orrery;

/// <summary>
/// A velocity Verlet integrator that settles a set of nodes under a named collection of forces.
/// </summary>
/// <remarks>
/// Ported from d3-force (ISC, © 2010-2021 Mike Bostock); see THIRD-PARTY-NOTICES.txt.
/// <para>
/// The simulation owns no timer and raises no events — call <see cref="Tick"/> from whatever clock
/// the host already has (a render loop, a dispatcher timer, or a plain <c>for</c> loop in a test).
/// That is what keeps the physics headless and unit-testable.
/// </para>
/// </remarks>
public sealed class ForceSimulation
{
    /// <summary>The temperature below which the layout is considered settled.</summary>
    public const double DefaultAlphaMin = 0.001;

    /// <summary>Cooling rate that reaches <see cref="DefaultAlphaMin"/> in roughly 300 ticks.</summary>
    public static readonly double DefaultAlphaDecay = 1 - Math.Pow(DefaultAlphaMin, 1.0 / 300);

    private const double InitialRadius = 10;
    private static readonly double InitialAngle = Math.PI * (3 - Math.Sqrt(5));

    private readonly List<GraphNode> _nodes = [];
    private readonly Dictionary<string, IForce> _forces = new(StringComparer.Ordinal);
    private Func<double> _random = Lcg.Create();

    /// <summary>Creates an empty simulation.</summary>
    public ForceSimulation()
    {
    }

    /// <summary>Creates a simulation over the given nodes.</summary>
    public ForceSimulation(IEnumerable<GraphNode> nodes) => SetNodes(nodes);

    /// <summary>Creates a simulation over a graph's nodes.</summary>
    public ForceSimulation(Graph graph) => SetNodes((graph ?? throw new ArgumentNullException(nameof(graph))).Nodes);

    /// <summary>
    /// Current temperature. Starts at 1 and decays toward <see cref="AlphaTarget"/>; forces scale
    /// their effect by it, so the layout moves boldly at first and finishes with fine adjustments.
    /// </summary>
    public double Alpha { get; set; } = 1;

    /// <summary>The temperature below which <see cref="IsSettled"/> reports true.</summary>
    public double AlphaMin { get; set; } = DefaultAlphaMin;

    /// <summary>How quickly <see cref="Alpha"/> approaches <see cref="AlphaTarget"/> each tick.</summary>
    public double AlphaDecay { get; set; } = DefaultAlphaDecay;

    /// <summary>
    /// The temperature <see cref="Alpha"/> decays toward — 0 by default, so the layout comes to rest.
    /// </summary>
    /// <remarks>
    /// Raising this to around 0.3 while a drag is in progress, then dropping it back to 0 on
    /// release, is what makes dragging feel elastic: the graph warms up smoothly instead of
    /// snapping to a new solution.
    /// </remarks>
    public double AlphaTarget { get; set; }

    /// <summary>
    /// Friction, in [0, 1]. Velocity retains <c>1 - VelocityDecay</c> of its magnitude per tick.
    /// Lower values make the layout springier and slower to settle.
    /// </summary>
    public double VelocityDecay { get; set; } = 0.4;

    /// <summary>The live node list, in index order.</summary>
    public IReadOnlyList<GraphNode> Nodes => _nodes;

    /// <summary>The forces currently attached, keyed by name.</summary>
    public IReadOnlyDictionary<string, IForce> Forces => _forces;

    /// <summary>True once <see cref="Alpha"/> has cooled below <see cref="AlphaMin"/>.</summary>
    public bool IsSettled => Alpha < AlphaMin;

    /// <summary>
    /// The seeded generator handed to every force. Replacing it re-initializes them.
    /// </summary>
    public Func<double> Random
    {
        get => _random;
        set
        {
            _random = value ?? throw new ArgumentNullException(nameof(value));
            foreach (var force in _forces.Values) force.Initialize(_nodes, _random);
        }
    }

    /// <summary>Replaces the node set, seeding any unplaced node and re-initializing every force.</summary>
    public ForceSimulation SetNodes(IEnumerable<GraphNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        _nodes.Clear();
        _nodes.AddRange(nodes);
        InitializeNodes();
        foreach (var force in _forces.Values) force.Initialize(_nodes, _random);
        return this;
    }

    /// <summary>Attaches a force under a name, replacing any force already using that name.</summary>
    public ForceSimulation AddForce(string name, IForce force)
    {
        ArgumentNullException.ThrowIfNull(force);

        force.Initialize(_nodes, _random);
        _forces[name] = force;
        return this;
    }

    /// <summary>The force attached under this name, or <c>null</c>.</summary>
    public IForce? GetForce(string name) => _forces.GetValueOrDefault(name);

    /// <summary>The force attached under this name as <typeparamref name="T"/>, or <c>null</c>.</summary>
    public T? GetForce<T>(string name) where T : class, IForce => _forces.GetValueOrDefault(name) as T;

    /// <summary>Detaches a force. Returns false if the name was not attached.</summary>
    public bool RemoveForce(string name) => _forces.Remove(name);

    /// <summary>
    /// Advances the simulation. Each iteration cools <see cref="Alpha"/>, applies every force, then
    /// integrates velocity into position.
    /// </summary>
    /// <param name="iterations">How many steps to run. More than one settles faster but skips frames.</param>
    public ForceSimulation Tick(int iterations = 1)
    {
        var decay = 1 - VelocityDecay;

        for (var k = 0; k < iterations; k++)
        {
            Alpha += (AlphaTarget - Alpha) * AlphaDecay;

            foreach (var force in _forces.Values) force.Apply(Alpha);

            foreach (var node in _nodes)
            {
                if (node.Fx is null)
                {
                    node.Vx *= decay;
                    node.X += node.Vx;
                }
                else
                {
                    node.X = node.Fx.Value;
                    node.Vx = 0;
                }

                if (node.Fy is null)
                {
                    node.Vy *= decay;
                    node.Y += node.Vy;
                }
                else
                {
                    node.Y = node.Fy.Value;
                    node.Vy = 0;
                }
            }
        }

        return this;
    }

    /// <summary>Runs the simulation until it settles, or until <paramref name="maxTicks"/> is reached.</summary>
    /// <returns>How many ticks were run.</returns>
    public int RunToSettle(int maxTicks = 1000)
    {
        var ticks = 0;
        while (!IsSettled && ticks < maxTicks)
        {
            Tick();
            ticks++;
        }

        return ticks;
    }

    /// <summary>
    /// Reheats the layout so it responds to a change. Call this whenever the graph is edited or a
    /// drag begins, otherwise a settled simulation will simply ignore the new state.
    /// </summary>
    public ForceSimulation Reheat(double alpha = 0.3)
    {
        Alpha = Math.Max(Alpha, alpha);
        return this;
    }

    /// <summary>The node nearest to a point, within an optional radius, or <c>null</c>.</summary>
    public GraphNode? Find(double x, double y, double radius = double.PositiveInfinity)
    {
        GraphNode? closest = null;
        var best = double.IsPositiveInfinity(radius) ? double.PositiveInfinity : radius * radius;

        foreach (var node in _nodes)
        {
            var dx = x - node.X;
            var dy = y - node.Y;
            var distance = dx * dx + dy * dy;
            if (distance >= best) continue;

            closest = node;
            best = distance;
        }

        return closest;
    }

    /// <summary>
    /// Assigns indices and places any node that has no coordinates yet onto a phyllotaxis spiral.
    /// </summary>
    /// <remarks>
    /// The golden-angle spiral matters more than it looks: it is deterministic, and it guarantees no
    /// two starting positions coincide and no three are collinear. Seeding on a grid or at the
    /// origin instead produces symmetric configurations that the forces cannot break out of.
    /// </remarks>
    private void InitializeNodes()
    {
        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            node.Index = i;

            if (node.Fx.HasValue) node.X = node.Fx.Value;
            if (node.Fy.HasValue) node.Y = node.Fy.Value;

            if (double.IsNaN(node.X) || double.IsNaN(node.Y))
            {
                var radius = InitialRadius * Math.Sqrt(0.5 + i);
                var angle = i * InitialAngle;
                node.X = radius * Math.Cos(angle);
                node.Y = radius * Math.Sin(angle);
            }

            if (double.IsNaN(node.Vx) || double.IsNaN(node.Vy))
            {
                node.Vx = 0;
                node.Vy = 0;
            }
        }
    }
}
