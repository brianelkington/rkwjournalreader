using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace read_journal
{
    class Program
    {
        private const int BinderWidth = 0;  // pixels to skip for the spiral binder
        private const int JpegQuality = 50; // output JPEG quality (0-100)
        private const string DefaultFolder = @".\images";
        private const string OutputFolder = "image_out";
        private static readonly VisualFeatures VisionFeatures =
            VisualFeatures.Read | VisualFeatures.Caption | VisualFeatures.DenseCaptions;

        static void Main(string[] args)
        {
            // --- Parse args ---
            bool saveImages = args.Any(a => a.Equals("--save-images", StringComparison.OrdinalIgnoreCase));
            bool verbose = args.Any(a => a.Equals("--verbose", StringComparison.OrdinalIgnoreCase));
            string inputFolder = args
                .FirstOrDefault(a => !a.StartsWith("--"))
                ?? DefaultFolder;

            if (!Directory.Exists(inputFolder))
            {
                Console.Error.WriteLine($"Image folder not found: {inputFolder}");
                return;
            }

            var cfg = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();
            var endpoint = cfg["AIServicesEndpoint"];
            var key = cfg["AIServicesKey"];
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
            {
                Console.Error.WriteLine("Missing AIServicesEndpoint or AIServicesKey in appsettings.json");
                return;
            }

            // Init Vision client
            var client = new ImageAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));

            // Enumerate JPEG files
            var images = Directory
                .EnumerateFiles(inputFolder, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => new[] { ".jpg", ".jpeg" }
                    .Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();
            if (!images.Any())
            {
                Console.WriteLine("No JPEG images found; exiting.");
                return;
            }

            // Prepare output directory
            var outputBase = Path.Combine(inputFolder, OutputFolder);
            Directory.CreateDirectory(outputBase);

            // Overall metrics
            double captionSum = 0;
            int captionCount = 0;
            var overallSw = Stopwatch.StartNew();

            // Process each image file
            foreach (var imagePath in images)
                ProcessImageFile(imagePath, client, outputBase, saveImages, verbose, ref captionSum, ref captionCount);

            overallSw.Stop();
            Console.WriteLine($"\nProcessed {captionCount} pages with captions. " +
                              $"Avg confidence: {(captionCount > 0 ? captionSum / captionCount : 0):P2}");
            Console.WriteLine($"Total processing time: {overallSw.Elapsed:c}");
        }

        static void ProcessImageFile(
            string imagePath,
            ImageAnalysisClient client,
            string outputBase,
            bool saveImages,
            bool verbose,
            ref double captionSum,
            ref int captionCount)
        {
            // Load and auto-rotate according to EXIF
            using var full = LoadAndOrientImage(imagePath);
            int W = full.Width, H = full.Height;
            int half = (W - BinderWidth) / 2;

            // Define left/right crop regions
            var regions = new[]
            {
                (Suffix: "_L", Rect: new SKRectI(0,              0, half,           H)),
                (Suffix: "_R", Rect: new SKRectI(half + BinderWidth, 0, W,           H))
            };

            var baseName = Path.GetFileNameWithoutExtension(imagePath);
            foreach (var reg in regions)
            {
                using var bmp = new SKBitmap(reg.Rect.Width, reg.Rect.Height);
                full.ExtractSubset(bmp, reg.Rect);

                var pageName = baseName + reg.Suffix;
                ProcessRegion(bmp, client, outputBase, baseName + reg.Suffix, saveImages, verbose, ref captionSum, ref captionCount);
            }
        }

        static void ProcessRegion(
            SKBitmap bitmap,
            ImageAnalysisClient client,
            string outputBase,
            string pageName,
            bool saveImages,
            bool verbose,
            ref double captionSum,
            ref int captionCount)
        {
            // Setup per-page logging (console ➔ .txt file)
            var origOut = Console.Out;
            var origErr = Console.Error;
            var logPath = Path.Combine(outputBase, $"{pageName}.txt");
            using var logWriter = new StreamWriter(logPath, false) { AutoFlush = true };
            var tee = new LogWriter(origOut, logWriter);
            Console.SetOut(tee);
            Console.SetError(tee);

            if (verbose)
            {
                Console.WriteLine($"\n--- Processing {pageName} ({bitmap.Width}x{bitmap.Height}) ---");
            }
            var sw = Stopwatch.StartNew();

            try
            {
                // Encode to JPEG in memory and call Vision
                using var imgData = SKImage
                    .FromBitmap(bitmap)
                    .Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
                using var ms = new MemoryStream(imgData.ToArray());
                ImageAnalysisResult result = client.Analyze(BinaryData.FromStream(ms), VisionFeatures);

                // Caption & stats
                if (!string.IsNullOrEmpty(result.Caption?.Text))
                {
                    if (verbose)
                    {
                        Console.WriteLine($"Caption: \"{result.Caption.Text}\" (Conf: {result.Caption.Confidence:P2})");
                    }
                    captionSum += result.Caption.Confidence;
                    captionCount++;
                }

                // Dense captions
                if (verbose)
                {
                    Console.WriteLine("Dense Captions:");
                    foreach (var dc in result.DenseCaptions.Values)
                        Console.WriteLine($"  {dc.Text} (Conf: {dc.Confidence:P2})");
                }

                // Full text (if there is any)
                if (result.Read != null)
                {
                    Console.WriteLine("Recognized Text:");
                    foreach (var line in result.Read.Blocks.SelectMany(b => b.Lines))
                        Console.WriteLine($"  {line.Text}");
                    if (verbose)
                    {
                        // Word-by-word confidences
                        Console.WriteLine("\nWord confidences:");
                        foreach (var word in result.Read.Blocks
                                                    .SelectMany(b => b.Lines)
                                                    .SelectMany(l => l.Words))
                        {
                            Console.WriteLine($"  {word.Text} (Confidence: {word.Confidence:P2})");
                        }
                    }
                }
                else
                {
                    if (verbose)
                        Console.WriteLine("No text recognized.");
                }

                // Annotate & save two images per region
                if (saveImages)
                {
                    // var linesOut = Path.Combine(outputBase, $"{pageName}_lines.jpg");
                    // AnnotateAndSave(bitmap, result.Read, linesOut, drawLines: true, drawWords: false, verbose);

                    var wordsOut = Path.Combine(outputBase, $"{pageName}_words.jpg");
                    AnnotateAndSave(bitmap, result.Read, wordsOut, drawLines: false, drawWords: true, verbose);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in {pageName}: {ex.Message}");
            }
            finally
            {
                sw.Stop();
                if (verbose)
                    Console.WriteLine($"Completed {pageName} in {sw.Elapsed:c}");
                // Restore console
                Console.SetOut(origOut);
                Console.SetError(origErr);
            }
        }

        static SKBitmap LoadAndOrientImage(string path)
        {
            // Load the JPEG & read EXIF
            using var codec = SKCodec.Create(path)
                ?? throw new InvalidOperationException($"Cannot open {path}");

            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height);

            // Allocate raw bitmap (do NOT dispose it here)
            var raw = new SKBitmap(info);
            codec.GetPixels(info, raw.GetPixels());

            // Apply orientation – this may return 'raw' or a new bitmap
            var oriented = raw.ApplyExifOrientation(codec.EncodedOrigin);

            // If we got back a *different* SKBitmap, dispose the old one
            if (!ReferenceEquals(oriented, raw))
            {
                raw.Dispose();
            }

            // Return the correctly-oriented bitmap (still alive)
            return oriented;
        }

        static void AnnotateAndSave(
            SKBitmap src,
            ReadResult readResult,
            string outputPath,
            bool drawLines,
            bool drawWords,
            bool verbose)
        {
            // Create a fresh bitmap and draw the source
            using var bmp = new SKBitmap(src.Info);
            using var canvas = new SKCanvas(bmp);
            canvas.DrawBitmap(src, 0, 0);

            // Outline paint
            using var paint = new SKPaint
            {
                Color = SKColors.Cyan,
                StrokeWidth = 3,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            // Draw lines
            if (drawLines && readResult != null)
            {
                foreach (var ln in readResult.Blocks.SelectMany(b => b.Lines))
                {
                    var skPoints = ln.BoundingPolygon
                        .Select(p => new SKPoint((float)p.X, (float)p.Y))
                        .ToArray();
                    DrawPolygon(canvas, skPoints, paint);
                }
            }

            // Draw words
            if (drawWords && readResult != null)
            {
                foreach (var wd in readResult.Blocks.SelectMany(b => b.Lines).SelectMany(l => l.Words))
                {
                    var skPoints = wd.BoundingPolygon
                        .Select(p => new SKPoint((float)p.X, (float)p.Y))
                        .ToArray();
                    DrawPolygon(canvas, skPoints, paint);
                }
            }

            // Save JPEG
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var stream = new SKFileWStream(outputPath);
            bmp.Encode(stream, SKEncodedImageFormat.Jpeg, JpegQuality);
            if (verbose)
                Console.WriteLine($"Saved {Path.GetFileName(outputPath)}");
        }

        static void DrawPolygon(SKCanvas canvas, SKPoint[] pts, SKPaint paint)
        {
            using var path = new SKPath();
            path.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) path.LineTo(pts[i]);
            path.Close();
            canvas.DrawPath(path, paint);
        }
    }
}
