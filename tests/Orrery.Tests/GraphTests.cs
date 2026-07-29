using Xunit;

namespace Orrery.Tests;

/// <summary>
/// Graph container tests. Traversal is what a renderer holding two flat lists cannot do, and it is
/// what drives focus effects like "highlight everything within two hops" or "light up the path".
/// </summary>
public class GraphTests
{
    [Fact]
    public void AddNode_IsIdempotentOnIdAndAssignsSequentialIndices()
    {
        var graph = new Graph();
        var first = graph.AddNode("a");
        var again = graph.AddNode("a");
        var second = graph.AddNode("b");

        Assert.Same(first, again);
        Assert.Equal(2, graph.Nodes.Count);
        Assert.Equal(0, first.Index);
        Assert.Equal(1, second.Index);
    }

    [Fact]
    public void Degree_CountsIncidentLinksRegardlessOfDirection()
    {
        var graph = Chain("a", "b", "c");

        Assert.Equal(1, graph.Degree("a"));
        Assert.Equal(2, graph.Degree("b"));
        Assert.Equal(0, graph.Degree("missing"));
    }

    [Fact]
    public void Neighbors_WalksBothDirections()
    {
        var graph = Chain("a", "b", "c");

        Assert.Equal(new[] { "a", "c" }, graph.Neighbors("b").Select(n => n.Id).Order());
    }

    [Fact]
    public void BreadthFirst_ReportsHopCount()
    {
        var graph = Chain("a", "b", "c", "d");

        var depths = graph.BreadthFirst("a").ToDictionary(x => x.Node.Id, x => x.Depth);

        Assert.Equal(0, depths["a"]);
        Assert.Equal(1, depths["b"]);
        Assert.Equal(2, depths["c"]);
        Assert.Equal(3, depths["d"]);
    }

    [Fact]
    public void BreadthFirst_StopsAtMaxDepth()
    {
        var graph = Chain("a", "b", "c", "d");

        var reached = graph.BreadthFirst("a", maxDepth: 2).Select(x => x.Node.Id).ToList();

        Assert.Equal(new[] { "a", "b", "c" }, reached);
    }

    [Fact]
    public void ShortestPath_ReturnsTheLinksAlongTheRoute()
    {
        var graph = Chain("a", "b", "c", "d");
        graph.AddLink("a", "d"); // a shortcut, so the shortest route is one hop

        var path = graph.ShortestPath("a", "d");

        Assert.Single(path);
        Assert.Equal("a", path[0].Source.Id);
        Assert.Equal("d", path[0].Target.Id);
    }

    [Fact]
    public void ShortestPath_ReturnsEmptyWhenDisconnected()
    {
        var graph = Chain("a", "b");
        graph.AddNode("island");

        Assert.Empty(graph.ShortestPath("a", "island"));
    }

    [Fact]
    public void RemoveNode_AlsoRemovesIncidentLinksAndReindexes()
    {
        var graph = Chain("a", "b", "c");

        Assert.True(graph.RemoveNode("b"));

        Assert.Empty(graph.Links);
        Assert.Equal(0, graph.Degree("a"));
        Assert.Equal(new[] { 0, 1 }, graph.Nodes.Select(n => n.Index));
    }

    [Fact]
    public void SelfLink_DoesNotAppearAsItsOwnNeighbor()
    {
        var graph = new Graph();
        graph.AddNode("a");
        graph.AddLink("a", "a");

        Assert.Empty(graph.Neighbors("a"));
        Assert.Equal(1, graph.Degree("a"));
    }

    [Fact]
    public void AddLink_RejectsUnknownEndpoints()
    {
        var graph = new Graph();
        graph.AddNode("a");

        Assert.Throws<ArgumentException>(() => graph.AddLink("a", "ghost"));
    }

    private static Graph Chain(params string[] ids)
    {
        var graph = new Graph();
        foreach (var id in ids) graph.AddNode(id);
        for (var i = 1; i < ids.Length; i++) graph.AddLink(ids[i - 1], ids[i]);
        return graph;
    }
}
