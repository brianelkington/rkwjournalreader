using SkiaSharp;

// === Extension to correct EXIF orientation ===
namespace read_journal
{
  public static class SKBitmapExtensions
  {
    /// <summary>
    /// Returns a new SKBitmap that has been rotated/flipped
    /// according to the EXIF orientation flag.
    /// </summary>
    public static SKBitmap ApplyExifOrientation(this SKBitmap src, SKEncodedOrigin origin)
    {
      // No-op if already correct.
      if (origin == SKEncodedOrigin.TopLeft)
        return src;

      int w = src.Width;
      int h = src.Height;
      SKBitmap dst;

      // Decide target dimensions
      bool swap = origin == SKEncodedOrigin.RightTop   // 90° CW
              || origin == SKEncodedOrigin.LeftBottom // 90° CCW
              || origin == SKEncodedOrigin.RightBottom  // transverse
              || origin == SKEncodedOrigin.LeftTop;    // transverse
      dst = new SKBitmap(swap ? h : w, swap ? w : h);

      using var canvas = new SKCanvas(dst);
      // Build the transform
      switch (origin)
      {
        case SKEncodedOrigin.BottomRight: // 180°
          canvas.Translate(w, h);
          canvas.RotateDegrees(180);
          break;

        case SKEncodedOrigin.RightTop: // 90° CW
          canvas.Translate(h, 0);
          canvas.RotateDegrees(90);
          break;

        case SKEncodedOrigin.LeftBottom: // 90° CCW
          canvas.Translate(0, w);
          canvas.RotateDegrees(270);
          break;

        case SKEncodedOrigin.TopRight: // Flip horizontal
          canvas.Scale(-1, 1);
          canvas.Translate(-w, 0);
          break;

        case SKEncodedOrigin.BottomLeft: // Flip vertical
          canvas.Scale(1, -1);
          canvas.Translate(0, -h);
          break;

        case SKEncodedOrigin.LeftTop: // Transpose (flip across TL↔BR diagonal)
          canvas.Scale(-1, 1);
          canvas.RotateDegrees(90);
          break;

        case SKEncodedOrigin.RightBottom: // Transverse (flip across TR↔BL diagonal)
          canvas.Scale(1, -1);
          canvas.RotateDegrees(90);
          break;
      }

      // Draw the source into the transformed canvas
      canvas.DrawBitmap(src, 0, 0);
      return dst;
    }
  }
}