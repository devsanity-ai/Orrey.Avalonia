using Avalonia.Controls;
using Orrery;
using Orrery.Avalonia;

namespace OrrerySample;

/// <summary>A person node.</summary>
public sealed record Person(string Name, string Role);

/// <summary>A team node — the hub its members attach to.</summary>
public sealed record Team(string Name);

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // The edge carries the relationship TYPE; the node carries the value. That split is what makes an
        // edge assert something specific rather than "these two are related".
        GraphHost.EdgeLabelSelector = link => link.DataAs<string>();

        // Anything speculative is drawn dashed.
        GraphHost.EdgeStyleSelector = link =>
            link.DataAs<string>() == "maybe" ? new GraphEdgeStyle { IsDashed = true } : null;

        GraphHost.Graph = BuildGraph();
    }

    private static Graph BuildGraph()
    {
        var graph = new Graph();

        var core = graph.AddNode("core", new Team("Core"));
        var platform = graph.AddNode("platform", new Team("Platform"));

        var ada = graph.AddNode("ada", new Person("Ada", "compilers"));
        var grace = graph.AddNode("grace", new Person("Grace", "runtime"));
        var alan = graph.AddNode("alan", new Person("Alan", "analysis"));
        var edsger = graph.AddNode("edsger", new Person("Edsger", "scheduling"));
        var barbara = graph.AddNode("barbara", new Person("Barbara", "storage"));

        graph.AddLink(core, ada, "member");
        graph.AddLink(core, grace, "member");
        graph.AddLink(core, alan, "member");
        graph.AddLink(platform, edsger, "member");
        graph.AddLink(platform, barbara, "member");
        graph.AddLink(core, platform, "depends on");
        graph.AddLink(ada, grace, "pairs with");
        graph.AddLink(alan, edsger, "maybe");

        return graph;
    }
}
