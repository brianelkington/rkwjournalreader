using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace read_journal
{
    // POCO for JSON input
    public class ImageEntry
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        [JsonPropertyName("split")]
        public bool Split { get; set; }
    }

    class Program
    {
        // Config constants
        private const int BinderWidth = 0;
        private const int JpegQuality = 50;
        private const string DefaultFolder = "images";
        private const string OutputFolder = "image_out";
        private static readonly VisualFeatures VisionFeatures =
            VisualFeatures.Read | VisualFeatures.Caption | VisualFeatures.DenseCaptions;

        static void Main(string[] args)
        {
            // Parse flags
            bool saveImages = args.Any(a => a.Equals("--save-images", StringComparison.OrdinalIgnoreCase));
            bool verbose = args.Any(a => a.Equals("--verbose", StringComparison.OrdinalIgnoreCase));

            // Determine input: folder or JSON
            string inputArg = args.FirstOrDefault(a => !a.StartsWith("--")) ?? DefaultFolder;
            bool isJson = inputArg.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                          && File.Exists(inputArg);

            // Build list of images to process
            List<ImageEntry> entries;
            string baseInputFolder;
            if (isJson)
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                string jsonText = File.ReadAllText(inputArg);
                entries = JsonSerializer.Deserialize<List<ImageEntry>>(jsonText, options)
                          ?? new List<ImageEntry>();

                // Normalize relative paths
                string jsonFolder = Path.GetDirectoryName(Path.GetFullPath(inputArg))
                                     ?? Directory.GetCurrentDirectory();
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (!Path.IsPathRooted(e.Path))
                        e.Path = Path.Combine(jsonFolder, e.Path);
                    entries[i] = e;
                }

                baseInputFolder = jsonFolder;
            }
            else if (Directory.Exists(inputArg))
            {
                entries = Directory
                    .EnumerateFiles(inputArg, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => new[] { ".jpg", ".jpeg" }
                        .Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Select(f => new ImageEntry { Path = f, Split = true })
                    .ToList();
                baseInputFolder = inputArg;
            }
            else
            {
                Console.Error.WriteLine($"Input not found: {inputArg}");
                return;
            }

            if (!entries.Any())
            {
                Console.WriteLine("No images to process; exiting.");
                return;
            }

            // Load config & create Vision client
            var cfg = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();
            string endpoint = cfg["AIServicesEndpoint"];
            string key = cfg["AIServicesKey"];
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
            {
                Console.Error.WriteLine("Missing AIServicesEndpoint or AIServicesKey in appsettings.json");
                return;
            }
            var client = new ImageAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));

            // Prepare output & aggregator
            string outputBase = Path.Combine(baseInputFolder, OutputFolder);
            Directory.CreateDirectory(outputBase);
            string aggPath = Path.Combine(outputBase, "aggregator.txt");
            using var aggregatorWriter = new StreamWriter(aggPath, append: false) { AutoFlush = true };

            // Process entries
            double captionSum = 0;
            int captionCount = 0;
            var overallSw = Stopwatch.StartNew();

            foreach (var entry in entries)
            {
                ProcessImageFile(
                    imagePath: entry.Path,
                    split: entry.Split,
                    client: client,
                    outputBase: outputBase,
                    saveImages: saveImages,
                    verbose: verbose,
                    captionSum: ref captionSum,
                    captionCount: ref captionCount,
                    aggregatorWriter: aggregatorWriter);
            }

            overallSw.Stop();
            Console.WriteLine($"\nProcessed {captionCount} pages with captions. " +
                              $"Avg confidence: {(captionCount > 0 ? captionSum / captionCount : 0):P2}");
            Console.WriteLine($"Total time: {overallSw.Elapsed:c}");
        }

        static void ProcessImageFile(
            string imagePath,
            bool split,
            ImageAnalysisClient client,
            string outputBase,
            bool saveImages,
            bool verbose,
            ref double captionSum,
            ref int captionCount,
            StreamWriter aggregatorWriter)
        {
            using var full = LoadAndOrientImage(imagePath);
            string baseName = Path.GetFileNameWithoutExtension(imagePath);

            if (split)
            {
                int W = full.Width, H = full.Height;
                int half = (W - BinderWidth) / 2;
                var regions = new[]
                {
                    (Suffix: "_L", Rect: new SKRectI(0, 0, half, H)),
                    (Suffix: "_R", Rect: new SKRectI(half + BinderWidth, 0, W, H))
                };
                foreach (var reg in regions)
                {
                    using var bmp = new SKBitmap(reg.Rect.Width, reg.Rect.Height);
                    full.ExtractSubset(bmp, reg.Rect);

                    ProcessRegion(
                        bmp,
                        client,
                        outputBase,
                        baseName + reg.Suffix,
                        saveImages,
                        verbose,
                        ref captionSum,
                        ref captionCount,
                        aggregatorWriter);
                }
            }
            else
            {
                ProcessRegion(
                    full,
                    client,
                    outputBase,
                    baseName,
                    saveImages,
                    verbose,
                    ref captionSum,
                    ref captionCount,
                    aggregatorWriter);
            }
        }

        static void ProcessRegion(
            SKBitmap bmp,
            ImageAnalysisClient client,
            string outputBase,
            string pageName,
            bool saveImages,
            bool verbose,
            ref double captionSum,
            ref int captionCount,
            StreamWriter aggregatorWriter)
        {
            // Chain console + per-page log + aggregator
            var origOut = Console.Out;
            var origErr = Console.Error;
            string logPath = Path.Combine(outputBase, pageName + ".out");
            using var logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
            var consolePlusAgg = new TeeTextWriter(origOut, aggregatorWriter);
            var allOut = new TeeTextWriter(consolePlusAgg, logWriter);
            Console.SetOut(allOut);
            var errorPlusAgg = new TeeTextWriter(origErr, aggregatorWriter);
            var allErr = new TeeTextWriter(errorPlusAgg, logWriter);
            Console.SetError(allErr);

            var sw = Stopwatch.StartNew();
            Console.WriteLine($"--- {pageName} ---");

            try
            {
                // Encode and analyze
                using var imgData = SKImage.FromBitmap(bmp)
                                          .Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
                using var ms = new MemoryStream(imgData.ToArray());
                ImageAnalysisResult result = client.Analyze(BinaryData.FromStream(ms), VisionFeatures);

                // Caption
                if (!string.IsNullOrEmpty(result.Caption?.Text))
                {
                    Console.WriteLine($"Caption: \"{result.Caption.Text}\" (Conf:{result.Caption.Confidence:P2})");
                    captionSum += result.Caption.Confidence;
                    captionCount++;
                }

                // Dense captions
                Console.WriteLine("Dense Captions:");
                foreach (var dc in result.DenseCaptions.Values)
                    Console.WriteLine($"  {dc.Text} (Conf:{dc.Confidence:P2})");

                // OCR text
                if (result.Read != null)
                {
                    Console.WriteLine("Recognized Text:");
                    foreach (var ln in result.Read.Blocks.SelectMany(b => b.Lines))
                        Console.WriteLine($"  {ln.Text}");

                    Console.WriteLine("\nWord confidences:");
                    foreach (var word in result.Read.Blocks.SelectMany(b => b.Lines).SelectMany(l => l.Words))
                        Console.WriteLine($"  {word.Text} (Conf:{word.Confidence:P2})");
                }

                // Optional image output
                if (saveImages)
                {
                    // var linesOut = Path.Combine(outputBase, $"{pageName}_lines.jpg");
                    // AnnotateAndSave(bmp, result.Read, linesOut, drawLines: true, drawWords: false);

                    var wordsOut = Path.Combine(outputBase, $"{pageName}_words.jpg");
                    AnnotateAndSave(bmp, result.Read, wordsOut, drawLines: false, drawWords: true);
                }
                else
                {
                    Console.WriteLine("Skipping JPEG output (no --save-images flag).");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in {pageName}: {ex.Message}");
            }
            finally
            {
                sw.Stop();
                Console.WriteLine($"Done in {sw.Elapsed:c}\n");
                Console.SetOut(origOut);
                Console.SetError(origErr);
            }
        }

        static SKBitmap LoadAndOrientImage(string path)
        {
            using var codec = SKCodec.Create(path)
                ?? throw new InvalidOperationException($"Cannot open {path}");
            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height);
            var raw = new SKBitmap(info);
            codec.GetPixels(info, raw.GetPixels());
            var oriented = raw.ApplyExifOrientation(codec.EncodedOrigin);
            if (!ReferenceEquals(oriented, raw))
                raw.Dispose();
            return oriented;
        }

        static void AnnotateAndSave(
            SKBitmap src,
            ReadResult readResult,
            string outputPath,
            bool drawLines,
            bool drawWords)
        {
            using var bmp = new SKBitmap(src.Info);
            using var canvas = new SKCanvas(bmp);
            canvas.DrawBitmap(src, 0, 0);

            using var paint = new SKPaint
            {
                Color = SKColors.Cyan,
                StrokeWidth = 3,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            if (drawLines && readResult != null)
            {
                foreach (var line in readResult.Blocks.SelectMany(b => b.Lines))
                {
                    var polygon = line.BoundingPolygon
                                      .Select(p => new SKPoint(p.X, p.Y))
                                      .ToArray();

                    DrawPolygon(canvas, polygon, paint);
                }
            }

            if (drawWords && readResult != null)
            {
                foreach (var wd in readResult.Blocks.SelectMany(b => b.Lines).SelectMany(l => l.Words))
                {
                    var polygon = wd.BoundingPolygon
                                    .Select(p => new SKPoint(p.X, p.Y))
                                    .ToArray();

                    DrawPolygon(canvas, polygon, paint);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var outStream = new SKFileWStream(outputPath);
            bmp.Encode(outStream, SKEncodedImageFormat.Jpeg, JpegQuality);
            Console.WriteLine($"Saved {Path.GetFileName(outputPath)}");
        }

        static void DrawPolygon(SKCanvas canvas, SKPoint[] pts, SKPaint paint)
        {
            using var path = new SKPath();
            path.MoveTo(pts[0]);
            foreach (var pt in pts.Skip(1))
                path.LineTo(pt);
            path.Close();
            canvas.DrawPath(path, paint);
        }
    }

    // Extension for EXIF orientation
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
                    canvas.Translate(w, h);
                    canvas.RotateDegrees(180);
                    break;
                case SKEncodedOrigin.RightTop:
                    canvas.Translate(h, 0);
                    canvas.RotateDegrees(90);
                    break;
                case SKEncodedOrigin.LeftBottom:
                    canvas.Translate(0, w);
                    canvas.RotateDegrees(270);
                    break;
                case SKEncodedOrigin.TopRight:
                    canvas.Scale(-1, 1);
                    canvas.Translate(-w, 0);
                    break;
                case SKEncodedOrigin.BottomLeft:
                    canvas.Scale(1, -1);
                    canvas.Translate(0, -h);
                    break;
                case SKEncodedOrigin.LeftTop:
                    canvas.Scale(-1, 1);
                    canvas.RotateDegrees(90);
                    break;
                case SKEncodedOrigin.RightBottom:
                    canvas.Scale(1, -1);
                    canvas.RotateDegrees(90);
                    break;
            }
            canvas.DrawBitmap(src, 0, 0);
            return dst;
        }
    }

    // TextWriter that tees output
    public class TeeTextWriter : TextWriter
    {
        private readonly TextWriter _primary, _secondary;
        public TeeTextWriter(TextWriter primary, TextWriter secondary)
        {
            _primary = primary;
            _secondary = secondary;
        }
        public override Encoding Encoding => _primary.Encoding;
        public override void Write(char value)
        {
            _primary.Write(value);
            _secondary.Write(value);
        }
        public override void Write(string value)
        {
            _primary.Write(value);
            _secondary.Write(value);
        }
        public override void WriteLine(string value)
        {
            _primary.WriteLine(value);
            _secondary.WriteLine(value);
        }
        public override void Flush()
        {
            _primary.Flush();
            _secondary.Flush();
        }
    }
}
