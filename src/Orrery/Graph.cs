namespace Orrery;

/// <summary>
/// A mutable graph of <see cref="GraphNode"/>s and <see cref="GraphLink"/>s, with adjacency and
/// traversal queries.
/// </summary>
/// <remarks>
/// Shaped after graphology: the container owns identity, adjacency and traversal, and knows nothing
/// about layout. Feed <see cref="Nodes"/> and <see cref="Links"/> to a <see cref="ForceSimulation"/>
/// to position them.
/// <para>
/// Traversal is the reason this exists rather than two bare lists. Highlighting "everything within
/// two hops of X", or the path between two nodes, is a graph query — a renderer holding only a flat
/// node list cannot answer it.
/// </para>
/// </remarks>
public sealed class Graph
{
    private readonly List<GraphNode> _nodes = [];
    private readonly List<GraphLink> _links = [];
    private readonly Dictionary<string, GraphNode> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<GraphLink>> _incident = new(StringComparer.Ordinal);

    /// <summary>Every node, in insertion order. Index matches <see cref="GraphNode.Index"/>.</summary>
    public IReadOnlyList<GraphNode> Nodes => _nodes;

    /// <summary>Every link, in insertion order. Index matches <see cref="GraphLink.Index"/>.</summary>
    public IReadOnlyList<GraphLink> Links => _links;

    /// <summary>Adds a node, or returns the existing node if <paramref name="id"/> is already present.</summary>
    public GraphNode AddNode(string id, object? data = null)
    {
        if (_byId.TryGetValue(id, out var existing))
        {
            if (data is not null) existing.Data = data;
            return existing;
        }

        var node = new GraphNode(id, data) { Index = _nodes.Count };
        _nodes.Add(node);
        _byId[id] = node;
        _incident[id] = [];
        return node;
    }

    /// <summary>Adds an already-constructed node. Throws if its id is taken.</summary>
    public GraphNode AddNode(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_byId.ContainsKey(node.Id)) throw new ArgumentException($"A node with id '{node.Id}' already exists.", nameof(node));

        node.Index = _nodes.Count;
        _nodes.Add(node);
        _byId[node.Id] = node;
        _incident[node.Id] = [];
        return node;
    }

    /// <summary>The node with this id, or <c>null</c>.</summary>
    public GraphNode? GetNode(string id) => _byId.GetValueOrDefault(id);

    /// <summary>True when a node with this id exists.</summary>
    public bool ContainsNode(string id) => _byId.ContainsKey(id);

    /// <summary>Links two existing nodes. Throws if either id is unknown.</summary>
    public GraphLink AddLink(string sourceId, string targetId, object? data = null)
    {
        var source = _byId.TryGetValue(sourceId, out var s) ? s : throw new ArgumentException($"Unknown node '{sourceId}'.", nameof(sourceId));
        var target = _byId.TryGetValue(targetId, out var t) ? t : throw new ArgumentException($"Unknown node '{targetId}'.", nameof(targetId));
        return AddLink(source, target, data);
    }

    /// <summary>Links two nodes, adding either to the graph if it is not already a member.</summary>
    public GraphLink AddLink(GraphNode source, GraphNode target, object? data = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (!_byId.ContainsKey(source.Id)) AddNode(source);
        if (!_byId.ContainsKey(target.Id)) AddNode(target);

        var link = new GraphLink(source, target, data) { Index = _links.Count };
        _links.Add(link);
        _incident[source.Id].Add(link);
        if (!ReferenceEquals(source, target)) _incident[target.Id].Add(link);
        return link;
    }

    /// <summary>Removes a node and every link incident to it. Returns false if the id is unknown.</summary>
    public bool RemoveNode(string id)
    {
        if (!_byId.TryGetValue(id, out var node)) return false;

        foreach (var link in _incident[id].ToList()) RemoveLink(link);
        _incident.Remove(id);
        _byId.Remove(id);
        _nodes.Remove(node);
        node.Index = -1;
        Reindex();
        return true;
    }

    /// <summary>Removes a link. Returns false if it is not part of this graph.</summary>
    public bool RemoveLink(GraphLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        if (!_links.Remove(link)) return false;

        if (_incident.TryGetValue(link.Source.Id, out var fromSource)) fromSource.Remove(link);
        if (_incident.TryGetValue(link.Target.Id, out var fromTarget)) fromTarget.Remove(link);
        link.Index = -1;
        for (var i = 0; i < _links.Count; i++) _links[i].Index = i;
        return true;
    }

    /// <summary>Every link touching this node. Empty when the id is unknown.</summary>
    public IReadOnlyList<GraphLink> IncidentLinks(string id) =>
        _incident.TryGetValue(id, out var links) ? links : [];

    /// <summary>How many links touch this node.</summary>
    public int Degree(string id) => _incident.TryGetValue(id, out var links) ? links.Count : 0;

    /// <summary>The nodes one hop away. Self-links are skipped.</summary>
    public IEnumerable<GraphNode> Neighbors(string id)
    {
        if (!_byId.TryGetValue(id, out var node)) yield break;

        foreach (var link in _incident[id])
        {
            var other = link.Other(node);
            if (other is not null && !ReferenceEquals(other, node)) yield return other;
        }
    }

    /// <summary>
    /// Walks outward from a node breadth-first, yielding each reachable node with its hop count.
    /// The start node is yielded first at depth 0.
    /// </summary>
    /// <param name="startId">Where to start. Yields nothing if unknown.</param>
    /// <param name="maxDepth">Stop after this many hops.</param>
    public IEnumerable<(GraphNode Node, int Depth)> BreadthFirst(string startId, int maxDepth = int.MaxValue)
    {
        if (!_byId.TryGetValue(startId, out var start)) yield break;

        var seen = new HashSet<string>(StringComparer.Ordinal) { startId };
        var queue = new Queue<(GraphNode Node, int Depth)>();
        queue.Enqueue((start, 0));

        while (queue.Count > 0)
        {
            var (node, depth) = queue.Dequeue();
            yield return (node, depth);
            if (depth >= maxDepth) continue;

            foreach (var neighbor in Neighbors(node.Id))
            {
                if (seen.Add(neighbor.Id)) queue.Enqueue((neighbor, depth + 1));
            }
        }
    }

    /// <summary>
    /// The links along a shortest path between two nodes, or an empty list when none exists.
    /// Use it to highlight a traversal — the path saturates while the rest of the graph fades.
    /// </summary>
    public IReadOnlyList<GraphLink> ShortestPath(string fromId, string toId)
    {
        if (!_byId.TryGetValue(fromId, out var from) || !_byId.ContainsKey(toId)) return [];
        if (string.Equals(fromId, toId, StringComparison.Ordinal)) return [];

        var cameFrom = new Dictionary<string, GraphLink>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal) { fromId };
        var queue = new Queue<GraphNode>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var link in _incident[node.Id])
            {
                var next = link.Other(node);
                if (next is null || !seen.Add(next.Id)) continue;

                cameFrom[next.Id] = link;
                if (string.Equals(next.Id, toId, StringComparison.Ordinal)) return Rebuild(cameFrom, next);
                queue.Enqueue(next);
            }
        }

        return [];
    }

    /// <summary>Removes every node and link.</summary>
    public void Clear()
    {
        foreach (var node in _nodes) node.Index = -1;
        _nodes.Clear();
        _links.Clear();
        _byId.Clear();
        _incident.Clear();
    }

    private static List<GraphLink> Rebuild(Dictionary<string, GraphLink> cameFrom, GraphNode end)
    {
        var path = new List<GraphLink>();
        var cursor = end;
        while (cameFrom.TryGetValue(cursor.Id, out var link))
        {
            path.Add(link);
            var previous = link.Other(cursor);
            if (previous is null) break;
            cursor = previous;
        }

        path.Reverse();
        return path;
    }

    private void Reindex()
    {
        for (var i = 0; i < _nodes.Count; i++) _nodes[i].Index = i;
    }
}
