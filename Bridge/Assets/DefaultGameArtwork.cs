using System.Windows;
using System.Windows.Media;

namespace Bridge.Assets;

/// <summary>Fallback artwork for games missing icon, cover, or background images.</summary>
public static class DefaultGameArtwork
{
    private static ImageSource? _icon;
    private static ImageSource? _cover;
    private static ImageSource? _background;

    public static ImageSource Icon => _icon ??= CreateIcon();
    public static ImageSource Cover => _cover ??= CreateCover();
    public static ImageSource Background => _background ??= CreateBackground();

    public static ImageSource? Get(GameArtworkFallback fallback) => fallback switch
    {
        GameArtworkFallback.Icon => Icon,
        GameArtworkFallback.Cover => Cover,
        GameArtworkFallback.Background => Background,
        _ => null
    };

    private static ImageSource CreateIcon()
    {
        const int size = 64;
        const double radius = 10;

        var background = Freeze(Color.FromRgb(0x27, 0x32, 0x48));
        var foreground = Freeze(Color.FromRgb(0x93, 0x9E, 0xB2));

        var icon = new GeometryGroup();
        icon.Children.Add(new RectangleGeometry(new Rect(14, 24, 36, 20), 6, 6));
        icon.Children.Add(new EllipseGeometry(new Point(22, 44), 5, 5));
        icon.Children.Add(new EllipseGeometry(new Point(42, 44), 5, 5));
        icon.Children.Add(new RectangleGeometry(new Rect(18, 30, 8, 8), 2, 2));
        icon.Children.Add(new EllipseGeometry(new Point(46, 34), 4, 4));

        return FreezeDrawing(background, foreground, icon, size, size, radius);
    }

    private static ImageSource CreateCover()
    {
        const int width = 120;
        const int height = 170;
        const double radius = 8;

        var background = Freeze(Color.FromRgb(0x22, 0x2B, 0x3D));
        var frame = Freeze(Color.FromRgb(0x35, 0x41, 0x58));
        var foreground = Freeze(Color.FromRgb(0x8D, 0x98, 0xAD));

        var art = new GeometryGroup();
        art.Children.Add(new RectangleGeometry(new Rect(28, 36, 64, 78), 4, 4));
        art.Children.Add(new EllipseGeometry(new Point(78, 58), 10, 10));
        art.Children.Add(new PathGeometry
        {
            Figures =
            {
                new PathFigure(new Point(34, 108), [new LineSegment(new Point(52, 82), true), new LineSegment(new Point(70, 96), true), new LineSegment(new Point(86, 76), true), new LineSegment(new Point(86, 108), true)], true)
            }
        });

        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(background, null, new RectangleGeometry(new Rect(0, 0, width, height), radius, radius)));
        drawing.Children.Add(new GeometryDrawing(null, new Pen(frame, 2), new RectangleGeometry(new Rect(16, 22, 88, 126), 6, 6)));
        drawing.Children.Add(new GeometryDrawing(foreground, null, art));

        return FreezeDrawing(drawing, width, height);
    }

    private static ImageSource CreateBackground()
    {
        const int width = 320;
        const int height = 180;

        var top = Freeze(Color.FromRgb(0x18, 0x20, 0x30));
        var bottom = Freeze(Color.FromRgb(0x2C, 0x3A, 0x52));
        var hill = Freeze(Color.FromRgb(0x3A, 0x4A, 0x66));
        var accent = Freeze(Color.FromRgb(0x56, 0x6D, 0x92));

        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops =
            {
                new GradientStop(top.Color, 0),
                new GradientStop(bottom.Color, 1)
            }
        };
        gradient.Freeze();

        var hills = new GeometryGroup();
        hills.Children.Add(new PathGeometry
        {
            Figures =
            {
                new PathFigure(new Point(0, 132), [new LineSegment(new Point(88, 84), true), new LineSegment(new Point(176, 118), true), new LineSegment(new Point(260, 72), true), new LineSegment(new Point(320, 104), true), new LineSegment(new Point(320, 180), true), new LineSegment(new Point(0, 180), true)], true)
            }
        });
        hills.Children.Add(new PathGeometry
        {
            Figures =
            {
                new PathFigure(new Point(0, 152), [new LineSegment(new Point(120, 108), true), new LineSegment(new Point(220, 138), true), new LineSegment(new Point(320, 118), true), new LineSegment(new Point(320, 180), true), new LineSegment(new Point(0, 180), true)], true)
            }
        });

        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(gradient, null, new RectangleGeometry(new Rect(0, 0, width, height))));
        drawing.Children.Add(new GeometryDrawing(hill, null, hills));
        drawing.Children.Add(new GeometryDrawing(null, new Pen(accent, 2), new EllipseGeometry(new Point(248, 52), 18, 18)));

        return FreezeDrawing(drawing, width, height);
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static ImageSource FreezeDrawing(SolidColorBrush background, SolidColorBrush foreground, Geometry geometry, double width, double height, double radius)
    {
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(background, null, new RectangleGeometry(new Rect(0, 0, width, height), radius, radius)));
        drawing.Children.Add(new GeometryDrawing(foreground, null, geometry));
        return FreezeDrawing(drawing, width, height);
    }

    private static ImageSource FreezeDrawing(DrawingGroup drawing, double width, double height)
    {
        var image = new DrawingImage(drawing);
        image.Freeze();
        _ = width;
        _ = height;
        return image;
    }
}
