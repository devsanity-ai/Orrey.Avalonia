# Orrery

Force-directed graph layout and an interactive graph control for .NET and Avalonia.

There is no maintained open-source force-directed graph control for Avalonia. The one library that
exists is layout-only and explicitly feature-frozen; the alternatives with real physics are
commercial, and are charting controls that draw labelled circles rather than hosting arbitrary
interactive content. Orrery exists to fill that gap.

| Package | Depends on | What it is |
|---|---|---|
| `Orrery` | nothing | Graph model and force simulation. No UI. |
| `Orrery.Avalonia` | `Orrery`, Avalonia | The `GraphView` control. |

The physics is a C# port of [d3-force](https://github.com/d3/d3-force) (ISC, © Mike Bostock); the
graph container is shaped after [graphology](https://github.com/graphology/graphology). See
`THIRD-PARTY-NOTICES.txt`.

## Why the split

The simulation has no UI dependency, so layout behaviour is asserted in unit tests rather than
eyeballed on screen:

```csharp
var graph = new Graph();
graph.AddNode("hub");
for (var i = 0; i < 6; i++) graph.AddLink("hub", graph.AddNode($"leaf{i}").Id);

var simulation = new ForceSimulation(graph)
    .AddForce("link", new LinkForce(graph))
    .AddForce("charge", new ManyBodyForce());

simulation.RunToSettle();

graph.GetNode("hub")!.Pin(600, 600);   // this is what dragging does
simulation.Reheat();
simulation.Tick(400);
// every leaf has followed the hub
```

It also means the graph model is usable on its own — for traversal queries a flat node list cannot
answer:

```csharp
graph.BreadthFirst("elera", maxDepth: 2);   // everything within two hops
graph.ShortestPath("elera", "kadan");       // the links along the route
graph.Degree("elera");
```

## The control

```xml
<orrery:GraphView Graph="{Binding CharacterGraph}"
                  LinkDistance="150"
                  EdgeBrush="{DynamicResource Accent}"
                  FocusDepth="1">
  <orrery:GraphView.NodeTemplate>
    <DataTemplate>
      <Border Classes="node">
        <TextBlock Text="{Binding Label}" />
      </Border>
    </DataTemplate>
  </orrery:GraphView.NodeTemplate>
</orrery:GraphView>
```

Nodes are ordinary Avalonia controls built from `NodeTemplate`, so they can contain buttons, text
boxes, images — anything. Pointer input that a node's own controls handle is left alone, so a drag
only begins on the parts of a node that ignore it.

What you get out of the box:

- **Physics** with alpha cooling; the frame timer stops once the layout settles, so an idle graph
  costs nothing
- **Node dragging** via pin/release, with the layout reheating smoothly rather than snapping
- **Pan and zoom**, zoom anchored under the pointer
- **Hover focus with multi-hop de-emphasis** — the focused neighbourhood stays lit while everything
  else fades. This is the single largest legibility lever in a dense graph, more than layout is.
- **Curved edges** with arrowheads and on-edge labels, trimmed to each node's real arranged bounds
- **`ZoomToFit`**, applied automatically until the user takes control of the view

## Forces

| Force | Effect |
|---|---|
| `LinkForce` | Springs along edges. Splits displacement by degree, so hubs anchor and leaves swing. |
| `ManyBodyForce` | Repulsion (or attraction) between every pair. |
| `CollideForce` | Keeps nodes from overlapping. The fix for colliding labels. |
| `XForce` / `YForce` | Soft per-node pull toward a coordinate. |
| `CenterForce` | Rigid re-centring. Do not combine with pinned nodes — see its docs. |

Implement `IForce` to add your own.

## Two things worth knowing

**Layouts are deterministic.** Node seeding uses a golden-angle spiral and every random decision
comes from a seeded generator, so the same graph lays out the same way every time. That is what makes
persisted node positions meaningful and physics regressions testable.

**`CenterForce` and dragging do not mix.** Centring translates every node to hold the centroid, and
the simulation then restores pinned nodes to their pins — the two never reconcile, and free nodes get
walked away from the pin a little more each tick. Since dragging is implemented as pinning, use
`XForce`/`YForce` for centring in any interactive view. `GraphView` already does.

## License

MIT.
