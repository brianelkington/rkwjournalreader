using SkiaSharp;

/// <summary>
/// Extension to apply EXIF orientation to an SKBitmap.
/// </summary>
public static class SKBitmapExtensions
{
    public static SKBitmap ApplyExifOrientation(this SKBitmap src, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft) return src;
        int w = src.Width, h = src.Height;
        bool swap = origin == SKEncodedOrigin.RightTop
                  || origin == SKEncodedOrigin.LeftBottom
                  || origin == SKEncodedOrigin.RightBottom
                  || origin == SKEncodedOrigin.LeftTop;
        var dst = new SKBitmap(swap ? h : w, swap ? w : h);
        using var canvas = new SKCanvas(dst);
        switch (origin)
        {
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(w, h); canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(h, 0); canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, w); canvas.RotateDegrees(270);
                break;
            case SKEncodedOrigin.TopRight:
                canvas.Scale(-1, 1); canvas.Translate(-w, 0);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Scale(1, -1); canvas.Translate(0, -h);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.Scale(-1, 1); canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Scale(1, -1); canvas.RotateDegrees(90);
                break;
        }
        canvas.DrawBitmap(src, 0, 0);
        return dst;
    }
}
