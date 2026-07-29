using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Orrery.Avalonia;

/// <summary>Carries the node a graph event concerns.</summary>
/// <param name="node">The node involved.</param>
public sealed class GraphNodeEventArgs(GraphNode node) : EventArgs
{
    /// <summary>The node involved.</summary>
    public GraphNode Node { get; } = node;
}

/// <summary>
/// An interactive force-directed graph: templated node content over curved, labelled edges, with
/// dragging, panning, zooming and hover focus.
/// </summary>
/// <remarks>
/// The view owns a <see cref="ForceSimulation"/> and drives it from a frame timer, stopping once the
/// layout settles so an idle graph costs nothing. Mutating the <see cref="Graph"/> does not notify
/// the view — call <see cref="Refresh"/> afterwards.
/// <para>
/// Node visuals are ordinary controls built from <see cref="NodeTemplate"/>, so anything Avalonia can
/// render can be a node, including interactive content. Pointer input a node's own controls handle
/// (a button, a text box) is left alone; a drag only begins on parts of the node that ignore it.
/// </para>
/// </remarks>
public class GraphView : Panel
{
    /// <summary>Defines the <see cref="Graph"/> property.</summary>
    public static readonly StyledProperty<Graph?> GraphProperty =
        AvaloniaProperty.Register<GraphView, Graph?>(nameof(Graph));

    /// <summary>Defines the <see cref="NodeTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> NodeTemplateProperty =
        AvaloniaProperty.Register<GraphView, IDataTemplate?>(nameof(NodeTemplate));

    /// <summary>Defines the <see cref="FocusedNode"/> property.</summary>
    public static readonly StyledProperty<GraphNode?> FocusedNodeProperty =
        AvaloniaProperty.Register<GraphView, GraphNode?>(nameof(FocusedNode));

    /// <summary>Defines the <see cref="FocusDepth"/> property.</summary>
    public static readonly StyledProperty<int> FocusDepthProperty =
        AvaloniaProperty.Register<GraphView, int>(nameof(FocusDepth), 1);

    /// <summary>Defines the <see cref="IsHoverFocusEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsHoverFocusEnabledProperty =
        AvaloniaProperty.Register<GraphView, bool>(nameof(IsHoverFocusEnabled), true);

    /// <summary>Defines the <see cref="IsNodeDragEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsNodeDragEnabledProperty =
        AvaloniaProperty.Register<GraphView, bool>(nameof(IsNodeDragEnabled), true);

    /// <summary>Defines the <see cref="IsPanEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsPanEnabledProperty =
        AvaloniaProperty.Register<GraphView, bool>(nameof(IsPanEnabled), true);

    /// <summary>Defines the <see cref="IsZoomEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsZoomEnabledProperty =
        AvaloniaProperty.Register<GraphView, bool>(nameof(IsZoomEnabled), true);

    /// <summary>Defines the <see cref="PinNodesAfterDrag"/> property.</summary>
    public static readonly StyledProperty<bool> PinNodesAfterDragProperty =
        AvaloniaProperty.Register<GraphView, bool>(nameof(PinNodesAfterDrag), true);

    /// <summary>Defines the <see cref="ScaleNodesWithZoom"/> property.</summary>
    public static readonly StyledProperty<bool> ScaleNodesWithZoomProperty =
        AvaloniaProperty.Register<GraphView, bool>(nameof(ScaleNodesWithZoom), true);

    /// <summary>Defines the <see cref="AutoFit"/> property.</summary>
    public static readonly StyledProperty<bool> AutoFitProperty =
        AvaloniaProperty.Register<GraphView, bool>(nameof(AutoFit), true);

    /// <summary>Defines the <see cref="FitPadding"/> property.</summary>
    public static readonly StyledProperty<double> FitPaddingProperty =
        AvaloniaProperty.Register<GraphView, double>(nameof(FitPadding), 48);

    /// <summary>Defines the <see cref="DimmedNodeOpacity"/> property.</summary>
    public static readonly StyledProperty<double> DimmedNodeOpacityProperty =
        AvaloniaProperty.Register<GraphView, double>(nameof(DimmedNodeOpacity), 0.35);

    /// <summary>Defines the <see cref="LinkDistance"/> property.</summary>
    public static readonly StyledProperty<double> LinkDistanceProperty =
        AvaloniaProperty.Register<GraphView, double>(nameof(LinkDistance), 140);

    /// <summary>Defines the <see cref="ChargeStrength"/> property.</summary>
    public static readonly StyledProperty<double> ChargeStrengthProperty =
        AvaloniaProperty.Register<GraphView, double>(nameof(ChargeStrength), -900);

    /// <summary>Defines the <see cref="CenteringStrength"/> property.</summary>
    public static readonly StyledProperty<double> CenteringStrengthProperty =
        AvaloniaProperty.Register<GraphView, double>(nameof(CenteringStrength), 0.05);

    /// <summary>Defines the <see cref="EdgeBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> EdgeBrushProperty =
        AvaloniaProperty.Register<GraphView, IBrush>(nameof(EdgeBrush), Brushes.SlateGray);

    /// <summary>Defines the <see cref="EdgeLabelBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> EdgeLabelBrushProperty =
        AvaloniaProperty.Register<GraphView, IBrush>(nameof(EdgeLabelBrush), Brushes.Gray);

    /// <summary>Defines the <see cref="EdgeLabelBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> EdgeLabelBackgroundProperty =
        AvaloniaProperty.Register<GraphView, IBrush?>(nameof(EdgeLabelBackground));

    /// <summary>Defines the <see cref="EdgeThickness"/> property.</summary>
    public static readonly StyledProperty<double> EdgeThicknessProperty =
        AvaloniaProperty.Register<GraphView, double>(nameof(EdgeThickness), 1.2);

    /// <summary>Defines the <see cref="EdgeCurvature"/> property.</summary>
    public static readonly StyledProperty<double> EdgeCurvatureProperty =
        AvaloniaProperty.Register<GraphView, double>(nameof(EdgeCurvature), 0.14);

    private readonly GraphEdgeLayer _edgeLayer = new();
    private readonly Dictionary<GraphNode, ContentPresenter> _presenters = [];
    private readonly Dictionary<GraphNode, GraphNodeVisual> _visuals = [];
    private readonly DispatcherTimer _timer;

    private GraphCamera _camera = new();
    private HashSet<GraphNode>? _focusNodes;
    private HashSet<GraphLink>? _focusLinks;
    private GraphNode? _dragNode;
    private Point _lastPointer;
    private bool _isPanning;
    private bool _userAdjustedView;

    /// <summary>Creates the view.</summary>
    public GraphView()
    {
        Children.Add(_edgeLayer);
        ClipToBounds = true;

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnFrame);
        AttachCamera(_camera);
    }

    /// <summary>Raised when a node drag finishes — the moment to persist its position.</summary>
    public event EventHandler<GraphNodeEventArgs>? NodeDragCompleted;

    /// <summary>The graph to display. Call <see cref="Refresh"/> after mutating it in place.</summary>
    public Graph? Graph
    {
        get => GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    /// <summary>Builds the visual for each node. Its data context is <see cref="GraphNode.Data"/>.</summary>
    public IDataTemplate? NodeTemplate
    {
        get => GetValue(NodeTemplateProperty);
        set => SetValue(NodeTemplateProperty, value);
    }

    /// <summary>
    /// The node currently under focus. Everything more than <see cref="FocusDepth"/> hops away is
    /// pushed back.
    /// </summary>
    public GraphNode? FocusedNode
    {
        get => GetValue(FocusedNodeProperty);
        set => SetValue(FocusedNodeProperty, value);
    }

    /// <summary>
    /// How many hops from <see cref="FocusedNode"/> stay lit. 1 shows a node and its neighbours;
    /// raise it to trace a neighbourhood outward.
    /// </summary>
    public int FocusDepth
    {
        get => GetValue(FocusDepthProperty);
        set => SetValue(FocusDepthProperty, value);
    }

    /// <summary>Whether hovering a node focuses it. Turn off to drive focus from code.</summary>
    public bool IsHoverFocusEnabled
    {
        get => GetValue(IsHoverFocusEnabledProperty);
        set => SetValue(IsHoverFocusEnabledProperty, value);
    }

    /// <summary>Whether nodes can be dragged.</summary>
    public bool IsNodeDragEnabled
    {
        get => GetValue(IsNodeDragEnabledProperty);
        set => SetValue(IsNodeDragEnabledProperty, value);
    }

    /// <summary>Whether dragging the background pans the view.</summary>
    public bool IsPanEnabled
    {
        get => GetValue(IsPanEnabledProperty);
        set => SetValue(IsPanEnabledProperty, value);
    }

    /// <summary>Whether the wheel zooms.</summary>
    public bool IsZoomEnabled
    {
        get => GetValue(IsZoomEnabledProperty);
        set => SetValue(IsZoomEnabledProperty, value);
    }

    /// <summary>
    /// Whether a dragged node stays where it was dropped. Leave on for hand-arranged layouts worth
    /// persisting; turn off to let the physics reclaim the node on release.
    /// </summary>
    public bool PinNodesAfterDrag
    {
        get => GetValue(PinNodesAfterDragProperty);
        set => SetValue(PinNodesAfterDragProperty, value);
    }

    /// <summary>
    /// Whether node visuals grow and shrink with the zoom level. While on, the view owns each node's
    /// <c>RenderTransform</c>; turn it off if the host wants to animate node transforms itself.
    /// </summary>
    public bool ScaleNodesWithZoom
    {
        get => GetValue(ScaleNodesWithZoomProperty);
        set => SetValue(ScaleNodesWithZoomProperty, value);
    }

    /// <summary>
    /// Whether the view frames the whole graph automatically. It stops as soon as the user pans,
    /// zooms or drags, so an arrangement made by hand is never yanked away from them.
    /// </summary>
    public bool AutoFit
    {
        get => GetValue(AutoFitProperty);
        set => SetValue(AutoFitProperty, value);
    }

    /// <summary>
    /// Breathing room, in pixels, left around the graph by <see cref="AutoFit"/>. Worth lowering in a
    /// narrow panel, where a generous margin costs enough zoom to make node labels unreadable.
    /// </summary>
    public double FitPadding
    {
        get => GetValue(FitPaddingProperty);
        set => SetValue(FitPaddingProperty, value);
    }

    /// <summary>Opacity of nodes pushed back because something else has focus.</summary>
    public double DimmedNodeOpacity
    {
        get => GetValue(DimmedNodeOpacityProperty);
        set => SetValue(DimmedNodeOpacityProperty, value);
    }

    /// <summary>Separation each edge pulls toward, in world units.</summary>
    public double LinkDistance
    {
        get => GetValue(LinkDistanceProperty);
        set => SetValue(LinkDistanceProperty, value);
    }

    /// <summary>Node repulsion. More negative spreads the graph further apart.</summary>
    public double ChargeStrength
    {
        get => GetValue(ChargeStrengthProperty);
        set => SetValue(ChargeStrengthProperty, value);
    }

    /// <summary>How strongly nodes drift toward the origin, keeping the graph from wandering off.</summary>
    public double CenteringStrength
    {
        get => GetValue(CenteringStrengthProperty);
        set => SetValue(CenteringStrengthProperty, value);
    }

    /// <summary>Default edge colour.</summary>
    public IBrush EdgeBrush
    {
        get => GetValue(EdgeBrushProperty);
        set => SetValue(EdgeBrushProperty, value);
    }

    /// <summary>Edge label colour.</summary>
    public IBrush EdgeLabelBrush
    {
        get => GetValue(EdgeLabelBrushProperty);
        set => SetValue(EdgeLabelBrushProperty, value);
    }

    /// <summary>Fill drawn behind edge labels.</summary>
    public IBrush? EdgeLabelBackground
    {
        get => GetValue(EdgeLabelBackgroundProperty);
        set => SetValue(EdgeLabelBackgroundProperty, value);
    }

    /// <summary>Default edge width.</summary>
    public double EdgeThickness
    {
        get => GetValue(EdgeThicknessProperty);
        set => SetValue(EdgeThicknessProperty, value);
    }

    /// <summary>How far edges bow, as a fraction of their length. Zero draws straight lines.</summary>
    public double EdgeCurvature
    {
        get => GetValue(EdgeCurvatureProperty);
        set => SetValue(EdgeCurvatureProperty, value);
    }

    /// <summary>The layout engine. Add or replace forces here to change how the graph settles.</summary>
    public ForceSimulation Simulation { get; } = new();

    /// <summary>The viewport. Replaceable, so two views can share one.</summary>
    public GraphCamera Camera
    {
        get => _camera;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_camera, value)) return;

            _camera.PropertyChanged -= OnCameraChanged;
            _camera = value;
            AttachCamera(value);
            InvalidateArrange();
        }
    }

    /// <summary>Resolves the text drawn on an edge.</summary>
    public Func<GraphLink, string?>? EdgeLabelSelector
    {
        get => _edgeLayer.LabelSelector;
        set => _edgeLayer.LabelSelector = value;
    }

    /// <summary>Resolves per-edge appearance overrides.</summary>
    public Func<GraphLink, GraphEdgeStyle?>? EdgeStyleSelector
    {
        get => _edgeLayer.StyleSelector;
        set => _edgeLayer.StyleSelector = value;
    }

    /// <summary>
    /// Rebuilds node visuals and forces from the current <see cref="Graph"/>, then reheats.
    /// Call this after adding or removing nodes or links.
    /// </summary>
    public void Refresh()
    {
        SyncPresenters();
        ConfigureForces();
        Simulation.Reheat();
        Start();
        InvalidateArrange();
    }

    /// <summary>Reheats a settled layout so it responds to a change.</summary>
    public void Reheat(double alpha = 0.3)
    {
        Simulation.Reheat(alpha);
        Start();
    }

    /// <summary>Frames the whole graph in the viewport.</summary>
    /// <param name="padding">Pixels of breathing room left around the outermost nodes.</param>
    public void ZoomToFit(double padding = 48)
    {
        var graph = Graph;
        if (graph is null || graph.Nodes.Count == 0) return;
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        double marginX = 0, marginY = 0;

        foreach (var node in graph.Nodes)
        {
            if (double.IsNaN(node.X) || double.IsNaN(node.Y)) continue;

            minX = Math.Min(minX, node.X);
            maxX = Math.Max(maxX, node.X);
            minY = Math.Min(minY, node.Y);
            maxY = Math.Max(maxY, node.Y);

            var size = _presenters.TryGetValue(node, out var presenter)
                ? presenter.DesiredSize
                : new Size(80, 48);
            marginX = Math.Max(marginX, size.Width / 2);
            marginY = Math.Max(marginY, size.Height / 2);
        }

        if (minX > maxX || minY > maxY) return;

        // Node footprints have to be reserved in whichever space they are CONSTANT in, or the fit feeds
        // back on itself. Measuring a fixed-pixel node in world units means a smaller zoom implies a
        // bigger world footprint, which implies a smaller zoom again — every frame, until the graph
        // collapses to a dot.
        var spanX = maxX - minX;
        var spanY = maxY - minY;
        double availableWidth, availableHeight;

        if (ScaleNodesWithZoom)
        {
            // The node grows with the zoom, so its world footprint is fixed: fold it into the span.
            spanX += 2 * marginX;
            spanY += 2 * marginY;
            availableWidth = Bounds.Width - 2 * padding;
            availableHeight = Bounds.Height - 2 * padding;
        }
        else
        {
            // The node is a fixed pixel size, so reserve the pixels instead.
            availableWidth = Bounds.Width - 2 * padding - 2 * marginX;
            availableHeight = Bounds.Height - 2 * padding - 2 * marginY;
        }

        _camera.Zoom = Math.Min(
            Math.Max(1, availableWidth) / Math.Max(1, spanX),
            Math.Max(1, availableHeight) / Math.Max(1, spanY));
        _camera.OffsetX = (minX + maxX) / 2;
        _camera.OffsetY = (minY + maxY) / 2;
    }

    /// <summary>Converts a point in view pixels to simulation world coordinates.</summary>
    public Point ToWorld(Point pixel) => new(
        (pixel.X - Bounds.Width / 2) / _camera.Zoom + _camera.OffsetX,
        (pixel.Y - Bounds.Height / 2) / _camera.Zoom + _camera.OffsetY);

    /// <summary>Converts simulation world coordinates to view pixels.</summary>
    public Point ToPixels(double worldX, double worldY) => new(
        Bounds.Width / 2 + (worldX - _camera.OffsetX) * _camera.Zoom,
        Bounds.Height / 2 + (worldY - _camera.OffsetY) * _camera.Zoom);

    /// <summary>The node whose visual contains this point, or null.</summary>
    public GraphNode? NodeAt(Point pixel)
    {
        // Reverse order so the topmost node wins where visuals overlap.
        for (var i = Children.Count - 1; i >= 1; i--)
        {
            if (Children[i] is not ContentPresenter presenter) continue;
            if (presenter.Tag is not GraphNode node) continue;
            if (!_visuals.TryGetValue(node, out var visual)) continue;

            if (Math.Abs(pixel.X - visual.Center.X) <= visual.HalfWidth &&
                Math.Abs(pixel.Y - visual.Center.Y) <= visual.HalfHeight)
            {
                return node;
            }
        }

        return null;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var child in Children) child.Measure(unbounded);

        return new Size(
            double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 300 : availableSize.Height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        _edgeLayer.Arrange(new Rect(finalSize));

        var graph = Graph;
        if (graph is null)
        {
            _visuals.Clear();
            _edgeLayer.Update([], _visuals, null);
            return finalSize;
        }

        if (AutoFit && !_userAdjustedView) ZoomToFit(FitPadding);

        var zoom = ScaleNodesWithZoom ? _camera.Zoom : 1;
        _visuals.Clear();

        foreach (var (node, presenter) in _presenters)
        {
            if (double.IsNaN(node.X) || double.IsNaN(node.Y)) continue;

            var size = presenter.DesiredSize;
            var center = ToPixels(node.X, node.Y);
            presenter.Arrange(new Rect(
                center.X - size.Width / 2,
                center.Y - size.Height / 2,
                size.Width,
                size.Height));

            // Only claim RenderTransform when we actually own it, so a host can animate node entrances.
            if (ScaleNodesWithZoom)
            {
                presenter.RenderTransformOrigin = RelativePoint.Center;
                presenter.RenderTransform = new ScaleTransform(zoom, zoom);
            }

            var dimmed = _focusNodes is not null && !_focusNodes.Contains(node);
            presenter.Opacity = dimmed ? DimmedNodeOpacity : 1;
            presenter.ZIndex = ReferenceEquals(node, FocusedNode) ? 100 : 1;

            _visuals[node] = new GraphNodeVisual(
                node, center, size.Width / 2 * zoom, size.Height / 2 * zoom);
        }

        ApplyEdgeAppearance();
        _edgeLayer.Update(graph.Links, _visuals, _focusLinks);
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == GraphProperty)
        {
            _userAdjustedView = false;
            FocusedNode = null;
            Simulation.SetNodes(Graph?.Nodes ?? []);
            Simulation.Alpha = 1;
            Refresh();
        }
        else if (change.Property == NodeTemplateProperty)
        {
            foreach (var presenter in _presenters.Values) presenter.ContentTemplate = NodeTemplate;
        }
        else if (change.Property == FocusedNodeProperty || change.Property == FocusDepthProperty)
        {
            UpdateFocusSets();
            InvalidateArrange();
        }
        else if (change.Property == LinkDistanceProperty ||
                 change.Property == ChargeStrengthProperty ||
                 change.Property == CenteringStrengthProperty)
        {
            ConfigureForces();
            Reheat();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // Content inside a node that wants the pointer (a button, a text box) has already claimed it.
        if (e.Handled) return;

        var position = e.GetPosition(this);
        var node = NodeAt(position);

        if (node is not null && IsNodeDragEnabled)
        {
            _dragNode = node;
            _userAdjustedView = true;
            var world = ToWorld(position);
            node.Pin(world.X, world.Y);
            Simulation.AlphaTarget = 0.3;
            Start();
            e.Pointer.Capture(this);
            e.Handled = true;
        }
        else if (IsPanEnabled)
        {
            _isPanning = true;
            _userAdjustedView = true;
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        _lastPointer = position;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);

        if (_dragNode is not null)
        {
            var world = ToWorld(position);
            _dragNode.Pin(world.X, world.Y);
            InvalidateArrange();
        }
        else if (_isPanning)
        {
            _camera.Pan(
                -(position.X - _lastPointer.X) / _camera.Zoom,
                -(position.Y - _lastPointer.Y) / _camera.Zoom);
        }
        else if (IsHoverFocusEnabled)
        {
            FocusedNode = NodeAt(position);
        }

        _lastPointer = position;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragNode is not null)
        {
            var node = _dragNode;
            _dragNode = null;
            if (!PinNodesAfterDrag) node.Unpin();
            Simulation.AlphaTarget = 0;
            NodeDragCompleted?.Invoke(this, new GraphNodeEventArgs(node));
        }

        _isPanning = false;
        e.Pointer.Capture(null);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_dragNode is null && !_isPanning && IsHoverFocusEnabled) FocusedNode = null;
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!IsZoomEnabled) return;

        var position = e.GetPosition(this);
        var before = ToWorld(position);

        _userAdjustedView = true;
        _camera.Zoom *= Math.Pow(1.12, e.Delta.Y);

        // Keep whatever was under the pointer under the pointer.
        _camera.OffsetX = before.X - (position.X - Bounds.Width / 2) / _camera.Zoom;
        _camera.OffsetY = before.Y - (position.Y - Bounds.Height / 2) / _camera.Zoom;
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Graph is { Nodes.Count: > 0 }) Start();
    }

    private void AttachCamera(GraphCamera camera) => camera.PropertyChanged += OnCameraChanged;

    private void OnCameraChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        InvalidateArrange();

    private void OnFrame(object? sender, EventArgs e)
    {
        Simulation.Tick();
        InvalidateArrange();

        // Idle graphs cost nothing; a drag holds AlphaTarget above the floor so this stays running.
        if (Simulation.IsSettled && _dragNode is null) _timer.Stop();
    }

    private void Start()
    {
        if (!_timer.IsEnabled) _timer.Start();
    }

    private void SyncPresenters()
    {
        var graph = Graph;
        var live = graph?.Nodes ?? [];

        foreach (var node in _presenters.Keys.Where(n => !live.Contains(n)).ToList())
        {
            Children.Remove(_presenters[node]);
            _presenters.Remove(node);
        }

        foreach (var node in live)
        {
            if (_presenters.ContainsKey(node)) continue;

            var presenter = new ContentPresenter
            {
                Content = node.Data ?? node,
                ContentTemplate = NodeTemplate,
                Tag = node,
            };

            _presenters[node] = presenter;
            Children.Add(presenter);
        }
    }

    private void ConfigureForces()
    {
        var graph = Graph;
        if (graph is null)
        {
            Simulation.RemoveForce("link");
            Simulation.RemoveForce("charge");
            Simulation.RemoveForce("collide");
            Simulation.RemoveForce("x");
            Simulation.RemoveForce("y");
            return;
        }

        var distance = LinkDistance;
        var charge = ChargeStrength;
        var centering = CenteringStrength;

        Simulation.AddForce("link", new LinkForce(graph) { Distance = (_, _) => distance });
        Simulation.AddForce("charge", new ManyBodyForce { Strength = (_, _) => charge });

        // Radius follows the node's real arranged size, so collision tracks whatever the template
        // renders instead of a guess that goes stale the moment a label grows.
        Simulation.AddForce("collide", new CollideForce(NodeRadius) { Strength = 0.8 });

        // Soft per-node centring rather than CenterForce: a rigid re-centre would walk the graph out
        // from under a pinned node, and dragging is implemented as pinning.
        Simulation.AddForce("x", new XForce(0) { Strength = (_, _) => centering });
        Simulation.AddForce("y", new YForce(0) { Strength = (_, _) => centering });
    }

    private double NodeRadius(GraphNode node, int index)
    {
        if (!_presenters.TryGetValue(node, out var presenter)) return 40;

        var size = presenter.DesiredSize;
        if (size.Width <= 0 || size.Height <= 0) return 40;

        return Math.Max(size.Width, size.Height) / 2 + 4;
    }

    private void UpdateFocusSets()
    {
        var graph = Graph;
        var focused = FocusedNode;

        if (graph is null || focused is null || !graph.ContainsNode(focused.Id))
        {
            _focusNodes = null;
            _focusLinks = null;
            return;
        }

        _focusNodes = graph.BreadthFirst(focused.Id, Math.Max(0, FocusDepth))
            .Select(hop => hop.Node)
            .ToHashSet();

        _focusLinks = graph.Links
            .Where(link => _focusNodes.Contains(link.Source) && _focusNodes.Contains(link.Target))
            .ToHashSet();
    }

    private void ApplyEdgeAppearance()
    {
        _edgeLayer.Brush = EdgeBrush;
        _edgeLayer.LabelBrush = EdgeLabelBrush;
        _edgeLayer.LabelBackground = EdgeLabelBackground;
        _edgeLayer.Thickness = EdgeThickness;
        _edgeLayer.Curvature = EdgeCurvature;
        _edgeLayer.Typeface = new Typeface(TextElement.GetFontFamily(this));
        _edgeLayer.FontSize = Math.Max(7, TextElement.GetFontSize(this) - 3);
    }
}
