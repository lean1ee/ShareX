using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace ShareX.Linux.Capture.Overlay;

public class InteractiveOverlayCanvas : Control
{
    public Bitmap? BackgroundImage { get; set; }
    public SKBitmap? SkiaBitmap { get; set; }

    public Rect SelectedRect { get; set; }
    public bool IsSelecting { get; set; }
    public bool HasSelection { get; set; }
    public Point CurrentPointerPos { get; set; }
    public bool ShowMagnifier { get; set; } = true;

    private static readonly IBrush MaskBrush = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
    private static readonly IPen BorderPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 164, 239)), 2);
    private static readonly IPen OuterBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), 4);
    private static readonly IPen CrosshairPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), 1, DashStyle.Dash);
    private static readonly IBrush TooltipBackground = new SolidColorBrush(Color.FromArgb(220, 24, 24, 37));
    private static readonly IBrush TextWhite = new SolidColorBrush(Colors.White);

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (BackgroundImage == null) return;

        var bounds = Bounds;

        // 1. Draw Background Image
        context.DrawImage(BackgroundImage, new Rect(0, 0, bounds.Width, bounds.Height));

        // 2. Draw Selection or Dark Mask
        if (HasSelection || IsSelecting)
        {
            var sel = NormalizeRect(SelectedRect);

            // Draw 4 mask rectangles around the selection
            // Top
            context.FillRectangle(MaskBrush, new Rect(0, 0, bounds.Width, sel.Top));
            // Bottom
            context.FillRectangle(MaskBrush, new Rect(0, sel.Bottom, bounds.Width, bounds.Height - sel.Bottom));
            // Left
            context.FillRectangle(MaskBrush, new Rect(0, sel.Top, sel.Left, sel.Height));
            // Right
            context.FillRectangle(MaskBrush, new Rect(sel.Right, sel.Top, bounds.Width - sel.Right, sel.Height));

            // Selection borders
            context.DrawRectangle(OuterBorderPen, sel);
            context.DrawRectangle(BorderPen, sel);

            // Dimension label
            var dimText = new FormattedText(
                $"{Math.Round(sel.Width)} × {Math.Round(sel.Height)} px",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                12,
                TextWhite
            );

            var labelY = sel.Top > 28 ? sel.Top - 24 : sel.Bottom + 6;
            var labelRect = new Rect(sel.Left, labelY, dimText.Width + 12, dimText.Height + 6);
            context.FillRectangle(TooltipBackground, labelRect, 4);
            context.DrawText(dimText, new Point(labelRect.Left + 6, labelRect.Top + 3));
        }
        else
        {
            // Dim whole screen slightly
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), bounds);
        }

        // 3. Draw Crosshairs & Magnifier if not finalized selection
        if (!HasSelection)
        {
            DrawCrosshairs(context, bounds, CurrentPointerPos);

            if (ShowMagnifier && SkiaBitmap != null)
            {
                DrawMagnifier(context, bounds, CurrentPointerPos);
            }
        }
    }

    private void DrawCrosshairs(DrawingContext context, Rect bounds, Point p)
    {
        // Horizontal line
        context.DrawLine(CrosshairPen, new Point(0, p.Y), new Point(bounds.Width, p.Y));
        // Vertical line
        context.DrawLine(CrosshairPen, new Point(p.X, 0), new Point(p.X, bounds.Height));

        // Coordinate tag
        var coordText = new FormattedText(
            $"X: {Math.Round(p.X)}, Y: {Math.Round(p.Y)}",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            11,
            TextWhite
        );
        var tagRect = new Rect(p.X + 8, p.Y + 8, coordText.Width + 10, coordText.Height + 4);
        context.FillRectangle(TooltipBackground, tagRect, 3);
        context.DrawText(coordText, new Point(tagRect.Left + 5, tagRect.Top + 2));
    }

    private void DrawMagnifier(DrawingContext context, Rect bounds, Point p)
    {
        double scaleX = bounds.Width > 0 ? (double)SkiaBitmap!.Width / bounds.Width : 1.0;
        double scaleY = bounds.Height > 0 ? (double)SkiaBitmap!.Height / bounds.Height : 1.0;

        int px = (int)Math.Clamp(Math.Round(p.X * scaleX), 0, SkiaBitmap!.Width - 1);
        int py = (int)Math.Clamp(Math.Round(p.Y * scaleY), 0, SkiaBitmap!.Height - 1);

        var pixelColor = SkiaBitmap.GetPixel(px, py);
        var hexColor = $"#{pixelColor.Red:X2}{pixelColor.Green:X2}{pixelColor.Blue:X2}";
        var rgbColor = $"RGB: {pixelColor.Red}, {pixelColor.Green}, {pixelColor.Blue}";

        // Position magnifier box (flip if close to right/bottom border)
        double boxSize = 130;
        double offsetX = 25;
        double offsetY = 25;

        double mx = p.X + offsetX;
        double my = p.Y + offsetY;

        if (mx + boxSize > bounds.Width) mx = p.X - boxSize - offsetX;
        if (my + boxSize + 60 > bounds.Height) my = p.Y - boxSize - offsetY - 60;

        var magRect = new Rect(mx, my, boxSize, boxSize);
        var infoRect = new Rect(mx, my + boxSize, boxSize, 56);

        // Magnifier background
        context.FillRectangle(TooltipBackground, new Rect(mx, my, boxSize, boxSize + 56), 6);
        context.DrawRectangle(BorderPen, new Rect(mx, my, boxSize, boxSize + 56), 6);

        // Zoom grid (9x9 pixels around cursor, 8x magnification)
        int sampleRadius = 4; // 9x9 pixels total
        double cellSize = boxSize / (sampleRadius * 2 + 1);

        for (int dx = -sampleRadius; dx <= sampleRadius; dx++)
        {
            for (int dy = -sampleRadius; dy <= sampleRadius; dy++)
            {
                int sx = Math.Clamp(px + dx, 0, SkiaBitmap.Width - 1);
                int sy = Math.Clamp(py + dy, 0, SkiaBitmap.Height - 1);
                var c = SkiaBitmap.GetPixel(sx, sy);

                var cellRect = new Rect(
                    mx + (dx + sampleRadius) * cellSize,
                    my + (dy + sampleRadius) * cellSize,
                    cellSize,
                    cellSize
                );

                context.FillRectangle(new SolidColorBrush(Color.FromRgb(c.Red, c.Green, c.Blue)), cellRect);
                context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), 0.5), cellRect);
            }
        }

        // Highlight center pixel
        var centerCell = new Rect(
            mx + sampleRadius * cellSize,
            my + sampleRadius * cellSize,
            cellSize,
            cellSize
        );
        context.DrawRectangle(new Pen(new SolidColorBrush(Colors.Red), 1.5), centerCell);

        // Color badge & text
        var colorBadgeRect = new Rect(mx + 8, my + boxSize + 8, 14, 14);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(pixelColor.Red, pixelColor.Green, pixelColor.Blue)), colorBadgeRect, 3);
        context.DrawRectangle(new Pen(new SolidColorBrush(Colors.White), 1), colorBadgeRect, 3);

        var hexText = new FormattedText(hexColor, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, TextWhite);
        context.DrawText(hexText, new Point(mx + 28, my + boxSize + 7));

        var rgbText = new FormattedText(rgbColor, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 9, new SolidColorBrush(Color.FromRgb(180, 180, 200)));
        context.DrawText(rgbText, new Point(mx + 8, my + boxSize + 26));

        var hintText = new FormattedText("C: copy color", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 9, new SolidColorBrush(Color.FromRgb(120, 160, 255)));
        context.DrawText(hintText, new Point(mx + 8, my + boxSize + 40));
    }

    private static Rect NormalizeRect(Rect r)
    {
        double x = Math.Min(r.Left, r.Right);
        double y = Math.Min(r.Top, r.Bottom);
        double w = Math.Abs(r.Width);
        double h = Math.Abs(r.Height);
        return new Rect(x, y, w, h);
    }
}
