using System.ComponentModel;

namespace Orrery.Avalonia;

/// <summary>
/// The viewport onto the simulation: where the view is looking and how far in.
/// </summary>
/// <remarks>
/// The simulation works in unbounded world units centred on the origin; the camera is what turns
/// those into pixels. Keeping it separate from the view means a host can drive it (buttons, a
/// minimap, an animation) or share one camera between two views of the same graph.
/// </remarks>
public sealed class GraphCamera : INotifyPropertyChanged
{
    private double _zoom = 1;
    private double _offsetX;
    private double _offsetY;
    private double _minimumZoom = 0.2;
    private double _maximumZoom = 4;

    /// <summary>Raised whenever the viewport changes, so views can redraw.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Pixels per world unit, clamped to <see cref="MinimumZoom"/>..<see cref="MaximumZoom"/>.</summary>
    public double Zoom
    {
        get => _zoom;
        set => Set(ref _zoom, Math.Clamp(value, _minimumZoom, _maximumZoom));
    }

    /// <summary>The world coordinate shown at the centre of the viewport.</summary>
    public double OffsetX
    {
        get => _offsetX;
        set => Set(ref _offsetX, value);
    }

    /// <summary>The world coordinate shown at the centre of the viewport.</summary>
    public double OffsetY
    {
        get => _offsetY;
        set => Set(ref _offsetY, value);
    }

    /// <summary>Closest the camera may pull back.</summary>
    public double MinimumZoom
    {
        get => _minimumZoom;
        set
        {
            if (!Set(ref _minimumZoom, value)) return;
            Zoom = _zoom;
        }
    }

    /// <summary>Closest the camera may push in.</summary>
    public double MaximumZoom
    {
        get => _maximumZoom;
        set
        {
            if (!Set(ref _maximumZoom, value)) return;
            Zoom = _zoom;
        }
    }

    /// <summary>Moves the viewport by a world-space delta.</summary>
    public void Pan(double deltaX, double deltaY)
    {
        _offsetX += deltaX;
        _offsetY += deltaY;
        Raise(nameof(OffsetX));
        Raise(nameof(OffsetY));
    }

    /// <summary>Returns the camera to the origin at 1:1.</summary>
    public void Reset()
    {
        _offsetX = 0;
        _offsetY = 0;
        _zoom = Math.Clamp(1, _minimumZoom, _maximumZoom);
        Raise(nameof(OffsetX));
        Raise(nameof(OffsetY));
        Raise(nameof(Zoom));
    }

    private bool Set(ref double field, double value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (field.Equals(value)) return false;

        field = value;
        Raise(name);
        return true;
    }

    private void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
