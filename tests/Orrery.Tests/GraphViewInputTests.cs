using Avalonia.Controls;
using Avalonia.Media;
using Orrery.Avalonia;
using Xunit;

namespace Orrery.Tests;

/// <summary>
/// Guards the preconditions that make GraphView's POINTER input reach it at all — as opposed to the
/// behaviour of that input, which needs a headless app and is covered by eye in the sample.
/// </summary>
public class GraphViewInputTests
{
    /// <summary>
    /// The background must default to something non-null or the control is invisible to hit testing.
    /// </summary>
    /// <remarks>
    /// This shipped broken: a <see cref="Panel"/> with a null Background does not participate in hit
    /// testing, so a press on empty canvas passed THROUGH the view and OnPointerPressed never ran —
    /// background-drag panning was dead for every host that didn't set a background of its own, including
    /// this repo's sample. Nodes are real child visuals and stayed draggable, which is what disguised it
    /// as a broken pan feature rather than as discarded events.
    /// </remarks>
    [Fact]
    public void Background_defaults_to_non_null_so_empty_canvas_is_hit_testable()
    {
        var fallback = Panel.BackgroundProperty.GetDefaultValue(typeof(GraphView));

        Assert.NotNull(fallback);
        Assert.Equal(Brushes.Transparent, fallback);
    }

    /// <summary>A host's own background must still win — the default is an override, not an assignment,
    /// so setting one in XAML is not clobbered by the constructor.</summary>
    [Fact]
    public void Host_can_still_set_its_own_background()
    {
        var view = new GraphView { Background = Brushes.Red };

        Assert.Equal(Brushes.Red, view.Background);
    }
}
