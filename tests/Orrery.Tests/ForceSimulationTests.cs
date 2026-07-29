using Xunit;

namespace Orrery.Tests;

/// <summary>
/// Physics tests. These run with no UI at all, which is the reason the simulation lives in its own
/// package: layout behaviour is asserted directly instead of being eyeballed on screen.
/// </summary>
public class ForceSimulationTests
{
    [Fact]
    public void Seeding_GivesEveryNodeADistinctPosition()
    {
        var graph = Star(8);
        var simulation = new ForceSimulation(graph);

        var positions = simulation.Nodes.Select(n => (n.X, n.Y)).ToList();

        Assert.All(positions, p => Assert.False(double.IsNaN(p.X) || double.IsNaN(p.Y)));
        Assert.Equal(positions.Count, positions.Distinct().Count());
    }

    [Fact]
    public void Simulation_SettlesWithinTheDefaultSchedule()
    {
        var simulation = Settled(Star(10), out var ticks);

        Assert.True(simulation.IsSettled);
        Assert.InRange(ticks, 1, 400);
    }

    [Fact]
    public void PinnedNode_HoldsItsExactPosition()
    {
        var graph = Star(6);
        var simulation = Standard(graph);
        var hub = graph.GetNode("hub")!;
        hub.Pin(120, -45);

        simulation.Tick(50);

        Assert.Equal(120, hub.X);
        Assert.Equal(-45, hub.Y);
        Assert.Equal(0, hub.Vx);
        Assert.Equal(0, hub.Vy);
    }

    [Fact]
    public void DraggingAPinnedHub_PullsTheWholeWebAlong()
    {
        var graph = Star(6);
        // No centering force here: it translates every node each tick to hold the centroid, which a
        // pinned node then fights, dragging the rest of the graph out from under it. See CenterForce.
        var simulation = new ForceSimulation(graph)
            .AddForce("link", new LinkForce(graph))
            .AddForce("charge", new ManyBodyForce());
        simulation.RunToSettle();

        var hub = graph.GetNode("hub")!;
        hub.Pin(600, 600);
        simulation.Reheat();
        simulation.Tick(400);

        // Every leaf should have followed the hub to its new home rather than been left behind.
        foreach (var leaf in graph.Nodes.Where(n => n != hub))
        {
            var distance = Math.Sqrt(Math.Pow(leaf.X - hub.X, 2) + Math.Pow(leaf.Y - hub.Y, 2));
            Assert.InRange(distance, 0, 200);
        }
    }

    [Fact]
    public void CenterForce_DragsTheLayoutOutFromUnderAPinnedNode()
    {
        // Documents a trap worth knowing: centering translates every node to hold the centroid, but a
        // pinned node is restored to its pin immediately afterward. The two never reconcile, so the
        // free nodes are walked away from the pin a little further every tick. Use XForce/YForce
        // instead when anything can be dragged.
        var graph = Star(6);
        var simulation = new ForceSimulation(graph)
            .AddForce("link", new LinkForce(graph))
            .AddForce("center", new CenterForce());
        var hub = graph.GetNode("hub")!;
        hub.Pin(600, 600);

        simulation.Tick(400);

        var leaf = graph.GetNode("leaf0")!;
        var distance = Math.Sqrt(Math.Pow(leaf.X - hub.X, 2) + Math.Pow(leaf.Y - hub.Y, 2));
        Assert.True(distance > 300, $"expected the leaf to be stranded, but it was {distance:F0} away");
    }

    [Fact]
    public void LinkForce_SplitsTheCorrectionEvenlyBetweenEqualDegrees()
    {
        var graph = new Graph();
        var a = graph.AddNode("a");
        var b = graph.AddNode("b");
        graph.AddLink(a, b);
        a.X = 0;
        a.Y = 0;
        b.X = 200;
        b.Y = 0;

        Apply(new LinkForce(graph), graph);

        Assert.True(Math.Abs(a.Vx) > 0);
        Assert.Equal(Math.Abs(a.Vx), Math.Abs(b.Vx), 9);
    }

    [Fact]
    public void LinkForce_MovesTheLowDegreeEndpointMore()
    {
        // a has one edge, b has five. The correction is weighted by the *other* endpoint's degree,
        // so the leaf yields and the hub holds its ground — which is what lets a dragged hub carry
        // its web instead of being knocked around by it.
        var graph = new Graph();
        var a = graph.AddNode("a");
        var b = graph.AddNode("b");
        graph.AddLink(a, b);
        foreach (var id in new[] { "c", "d", "e", "f" }) graph.AddLink(b, graph.AddNode(id));

        b.X = 0;
        b.Y = 0;
        var angle = 0.0;
        foreach (var id in new[] { "c", "d", "e", "f" })
        {
            var node = graph.GetNode(id)!;
            node.X = 30 * Math.Cos(angle);
            node.Y = 30 * Math.Sin(angle);
            angle += Math.PI / 2;
        }

        a.X = 200;
        a.Y = 0;

        Apply(new LinkForce(graph), graph);

        Assert.True(Math.Abs(a.Vx) > 3 * Math.Abs(b.Vx),
            $"leaf moved {Math.Abs(a.Vx):F2}, hub moved {Math.Abs(b.Vx):F2}");
    }

    [Fact]
    public void CollideForce_KeepsNodesApartEvenWhenTheLinkWantsThemCloser()
    {
        // The realistic case: the link asks for 10 units of separation, but each node needs 20 units
        // of room for its label. Collide ignores alpha, so it still wins once the layout has cooled.
        var graph = new Graph();
        var a = graph.AddNode("a");
        var b = graph.AddNode("b");
        graph.AddLink(a, b);
        a.X = 0;
        a.Y = 0;
        b.X = 4;
        b.Y = 0;

        var simulation = new ForceSimulation(graph);
        simulation.AddForce("link", new LinkForce(graph) { Distance = (_, _) => 10 });
        simulation.AddForce("collide", new CollideForce(20));
        simulation.RunToSettle();

        var distance = Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        Assert.True(distance >= 40, $"nodes settled {distance:F1} apart, inside their 40-unit exclusion");
    }

    [Fact]
    public void ManyBodyForce_PushesNodesApart()
    {
        var graph = new Graph();
        for (var i = 0; i < 5; i++) graph.AddNode($"n{i}");

        var simulation = new ForceSimulation(graph);
        simulation.AddForce("charge", new ManyBodyForce());
        var before = Spread(graph);
        simulation.RunToSettle();

        Assert.True(Spread(graph) > before);
    }

    [Fact]
    public void Layout_IsDeterministic()
    {
        var first = Settled(Star(12), out _).Nodes.Select(n => (n.X, n.Y)).ToList();
        var second = Settled(Star(12), out _).Nodes.Select(n => (n.X, n.Y)).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Reheat_WakesASettledSimulation()
    {
        var simulation = Settled(Star(5), out _);
        Assert.True(simulation.IsSettled);

        simulation.Reheat();

        Assert.False(simulation.IsSettled);
    }

    [Fact]
    public void Find_ReturnsTheNearestNodeWithinRadius()
    {
        var graph = new Graph();
        var near = graph.AddNode("near");
        var far = graph.AddNode("far");
        near.X = 10;
        near.Y = 10;
        far.X = 500;
        far.Y = 500;

        var simulation = new ForceSimulation(graph);

        Assert.Same(near, simulation.Find(12, 12));
        Assert.Null(simulation.Find(900, 900, 50));
    }

    private static Graph Star(int leaves)
    {
        var graph = new Graph();
        graph.AddNode("hub");
        for (var i = 0; i < leaves; i++) graph.AddLink("hub", graph.AddNode($"leaf{i}").Id);
        return graph;
    }

    private static void Apply(IForce force, Graph graph)
    {
        var simulation = new ForceSimulation(graph);
        force.Initialize(simulation.Nodes, simulation.Random);
        force.Apply(1);
    }

    private static ForceSimulation Standard(Graph graph) =>
        new ForceSimulation(graph)
            .AddForce("link", new LinkForce(graph))
            .AddForce("charge", new ManyBodyForce())
            .AddForce("center", new CenterForce());

    private static ForceSimulation Settled(Graph graph, out int ticks)
    {
        var simulation = Standard(graph);
        ticks = simulation.RunToSettle();
        return simulation;
    }

    private static double Spread(Graph graph) =>
        graph.Nodes.Sum(n => Math.Sqrt(n.X * n.X + n.Y * n.Y));
}
