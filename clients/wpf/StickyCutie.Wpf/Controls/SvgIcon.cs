using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace StickyCutie.Wpf.Controls;

/// <summary>
/// Extremely small SVG renderer that supports the subset of tags used by our Lucide icons.
/// </summary>
public sealed class SvgIcon : Viewbox
{
    private static readonly BrushConverter BrushConverter = new();

    public static readonly DependencyProperty SvgPathProperty =
        DependencyProperty.Register(
            nameof(SvgPath),
            typeof(string),
            typeof(SvgIcon),
            new PropertyMetadata(null, OnSvgChanged));

    public static readonly DependencyProperty IconBrushProperty =
        DependencyProperty.Register(
            nameof(IconBrush),
            typeof(Brush),
            typeof(SvgIcon),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5A)), OnSvgChanged));

    public string? SvgPath
    {
        get => (string?)GetValue(SvgPathProperty);
        set => SetValue(SvgPathProperty, value);
    }

    public Brush IconBrush
    {
        get => (Brush)GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public SvgIcon()
    {
        Stretch = Stretch.Uniform;
        SnapsToDevicePixels = true;
    }

    private static void OnSvgChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
    {
        if (d is SvgIcon icon)
        {
            icon.RenderSvg();
        }
    }

    private void RenderSvg()
    {
        var path = SvgPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            Child = null;
            return;
        }

        try
        {
            var svgMarkup = ReadSvgContent(path);
            if (svgMarkup is null)
            {
                Child = null;
                return;
            }

            var document = XDocument.Parse(svgMarkup);
            if (document.Root is null)
            {
                Child = null;
                return;
            }

            var width = ParseDouble(document.Root.Attribute("width"), 24);
            var height = ParseDouble(document.Root.Attribute("height"), 24);

            var canvas = new Canvas
            {
                Width = width,
                Height = height
            };

            foreach (var element in document.Root.Elements())
            {
                if (CreateShape(element, document.Root) is Shape shape)
                {
                    canvas.Children.Add(shape);
                }
            }

            Child = canvas;
        }
        catch
        {
            Child = null;
        }
    }

    private Shape? CreateShape(XElement element, XElement root)
    {
        return element.Name.LocalName switch
        {
            "path" => CreatePath(element, root),
            "rect" => CreateRectangle(element, root),
            "circle" => CreateCircle(element, root),
            "line" => CreateLine(element, root),
            _ => null
        };
    }

    private Shape? CreatePath(XElement element, XElement root)
    {
        var data = element.Attribute("d")?.Value;
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(data)
        };

        ApplyStrokeSettings(path, element, root);
        ApplyFill(path, element, root);

        return path;
    }

    private Shape CreateRectangle(XElement element, XElement root)
    {
        var rect = new Rectangle
        {
            Width = ParseDouble(element.Attribute("width"), 0),
            Height = ParseDouble(element.Attribute("height"), 0),
            RadiusX = ParseDouble(element.Attribute("rx"), 0),
            RadiusY = ParseDouble(element.Attribute("ry"), 0)
        };

        Canvas.SetLeft(rect, ParseDouble(element.Attribute("x"), 0));
        Canvas.SetTop(rect, ParseDouble(element.Attribute("y"), 0));

        ApplyStrokeSettings(rect, element, root);
        ApplyFill(rect, element, root);

        return rect;
    }

    private Shape CreateCircle(XElement element, XElement root)
    {
        var radius = ParseDouble(element.Attribute("r"), 0);
        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2
        };

        var cx = ParseDouble(element.Attribute("cx"), radius);
        var cy = ParseDouble(element.Attribute("cy"), radius);
        Canvas.SetLeft(ellipse, cx - radius);
        Canvas.SetTop(ellipse, cy - radius);

        ApplyStrokeSettings(ellipse, element, root);
        ApplyFill(ellipse, element, root);

        return ellipse;
    }

    private Shape CreateLine(XElement element, XElement root)
    {
        var line = new Line
        {
            X1 = ParseDouble(element.Attribute("x1"), 0),
            Y1 = ParseDouble(element.Attribute("y1"), 0),
            X2 = ParseDouble(element.Attribute("x2"), 0),
            Y2 = ParseDouble(element.Attribute("y2"), 0)
        };

        ApplyStrokeSettings(line, element, root);
        return line;
    }

    private void ApplyStrokeSettings(Shape shape, XElement element, XElement root)
    {
        shape.Stroke = ResolveBrush(element, root, "stroke") ?? IconBrush;
        shape.StrokeThickness = ParseDouble(element.Attribute("stroke-width"), ParseDouble(root.Attribute("stroke-width"), 2));

        if (Enum.TryParse(element.Attribute("stroke-linecap")?.Value ?? root.Attribute("stroke-linecap")?.Value, true, out PenLineCap lineCap))
        {
            shape.StrokeStartLineCap = shape.StrokeEndLineCap = lineCap;
        }
        else
        {
            shape.StrokeStartLineCap = shape.StrokeEndLineCap = PenLineCap.Round;
        }

        if (Enum.TryParse(element.Attribute("stroke-linejoin")?.Value ?? root.Attribute("stroke-linejoin")?.Value, true, out PenLineJoin lineJoin))
        {
            shape.StrokeLineJoin = lineJoin;
        }
        else
        {
            shape.StrokeLineJoin = PenLineJoin.Round;
        }
    }

    private void ApplyFill(Shape shape, XElement element, XElement root)
    {
        var fillBrush = ResolveBrush(element, root, "fill") ?? Brushes.Transparent;
        shape.Fill = fillBrush;
    }

    private Brush? ResolveBrush(XElement element, XElement root, string attributeName)
    {
        var raw = element.Attribute(attributeName)?.Value ?? root.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (string.Equals(raw, "none", StringComparison.OrdinalIgnoreCase))
        {
            return Brushes.Transparent;
        }

        if (string.Equals(raw, "currentColor", StringComparison.OrdinalIgnoreCase))
        {
            return IconBrush;
        }

        if (BrushConverter.ConvertFromString(raw) is Brush brush)
        {
            return brush;
        }

        return null;
    }

    private static double ParseDouble(XAttribute? attribute, double fallback)
    {
        return attribute != null &&
               double.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static string? ReadSvgContent(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var absolute = System.IO.Path.Combine(AppContext.BaseDirectory, relativePath);
        if (File.Exists(absolute))
        {
            return File.ReadAllText(absolute, Encoding.UTF8);
        }

        var resourceUri = new Uri($"pack://application:,,,/{normalized}", UriKind.Absolute);
        var resource = Application.GetResourceStream(resourceUri);
        if (resource is null)
        {
            return null;
        }

        using var reader = new StreamReader(resource.Stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
