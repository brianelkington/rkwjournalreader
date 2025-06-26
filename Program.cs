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
        static void Main(string[] args)
        {
            // Accumulators for caption confidence
            double totalCaptionConfidence = 0;
            int captionCount = 0;

            // Preserve original console output
            var originalOut = Console.Out;
            var originalErr = Console.Error;

            try
            {
                Console.WriteLine($"Run started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

                // Load configuration
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .Build();
                string aiSvcEndpoint = configuration["AIServicesEndpoint"];
                string aiSvcKey = configuration["AIServicesKey"];

                // Determine base path for images
                string basePath = args.Length > 0
                    ? args[0]
                    : @"..\..\..\images";

                if (!Directory.Exists(basePath))
                {
                    Console.Error.WriteLine($"ERROR: folder not found: {basePath}");
                    return;
                }

                // Create Vision client
                var client = new ImageAnalysisClient(
                    new Uri(aiSvcEndpoint),
                    new AzureKeyCredential(aiSvcKey));

                Console.WriteLine("Initialized Azure AI Vision client.\n");

                // Enumerate JPEG files
                var imageFiles = Directory
                    .EnumerateFiles(basePath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f);
                        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();

                if (!imageFiles.Any())
                {
                    Console.WriteLine("No JPEG images found in folder.");
                    return;
                }

                var totalSw = Stopwatch.StartNew();

                foreach (var imageFile in imageFiles)
                {
                    string name = Path.GetFileNameWithoutExtension(imageFile);
                    var sw = Stopwatch.StartNew();
                    Console.WriteLine($"\n--- Processing {name} ---\n");

                    try
                    {
                        // Load and correct EXIF orientation
                        using var codec = SKCodec.Create(imageFile)
                            ?? throw new InvalidOperationException($"Couldn't open {imageFile}");
                        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height);
                        using var raw = new SKBitmap(info);
                        codec.GetPixels(info, raw.GetPixels());
                        using var fullBitmap = raw.ApplyExifOrientation(codec.EncodedOrigin);

                        int W = fullBitmap.Width;
                        int H = fullBitmap.Height;
                        int binderWidth = 0;
                        int halfWidth = (W - binderWidth) / 2;

                        // Split into left/right
                        var leftRect = new SKRectI(0, 0, halfWidth, H);
                        var rightRect = new SKRectI(halfWidth + binderWidth, 0, W, H);
                        var leftBmp = new SKBitmap(halfWidth, H);
                        var rightBmp = new SKBitmap(halfWidth, H);
                        fullBitmap.ExtractSubset(leftBmp, leftRect);
                        fullBitmap.ExtractSubset(rightBmp, rightRect);

                        // Process each side
                        ProcessPage(leftBmp, imageFile, "L", client, ref totalCaptionConfidence, ref captionCount, originalOut, originalErr);
                        ProcessPage(rightBmp, imageFile, "R", client, ref totalCaptionConfidence, ref captionCount, originalOut, originalErr);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error processing {imageFile}: {ex.Message}");
                    }
                    finally
                    {
                        sw.Stop();
                        Console.WriteLine($"\nTime for {name}: {sw.Elapsed:c}\n");

                        // Restore console output
                        Console.SetOut(originalOut);
                        Console.SetError(originalErr);
                    }
                }

                totalSw.Stop();

                // Aggregate caption confidence
                if (captionCount > 0)
                {
                    double avg = totalCaptionConfidence / captionCount;
                    Console.WriteLine($"Processed {captionCount} pages with captions. Average caption confidence: {avg:P2}");
                }
                else
                {
                    Console.WriteLine("No captions found on any page.");
                }

                Console.WriteLine($"\nAll pages done. Total time: {totalSw.Elapsed:c}");
                Console.WriteLine($"Run finished at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
            }
        }

    static void ProcessPage(
        SKBitmap pageBitmap,
        string originalPath,
        string side,
        ImageAnalysisClient client,
        ref double totalCaptionConfidence,
        ref int captionCount,
        TextWriter originalOut,
        TextWriter originalErr)
    {
        string dir    = Path.GetDirectoryName(originalPath)!;
        string name   = Path.GetFileNameWithoutExtension(originalPath);
        string suffix = $"_{side}";

        // Setup per-page logging
        string outDir   = Path.Combine(dir, "image_out");
        string logFile  = Path.Combine(outDir, $"{name}{suffix}.out");
        using var logWriter = new StreamWriter(logFile, append: false) { AutoFlush = true };
        var tee = new LogWriter(originalOut, logWriter);
        Console.SetOut(tee);
        Console.SetError(tee);

        try
        {
            // Encode the page into a MemoryStream
            using var img  = SKImage.FromBitmap(pageBitmap);
            using var data = img.Encode(SKEncodedImageFormat.Jpeg, 100);
            using var ms   = new MemoryStream(data.ToArray());

            Console.WriteLine($"\n--- Analysis for {name}{suffix} ---");
            ImageAnalysisResult result = client.Analyze(
                BinaryData.FromStream(ms),
                VisualFeatures.Read | VisualFeatures.Caption | VisualFeatures.DenseCaptions
            );
            
            // Caption
            if (!string.IsNullOrEmpty(result.Caption?.Text))
            {
                Console.WriteLine("Caption:");
                Console.WriteLine($"  \"{result.Caption.Text}\" (Confidence: {result.Caption.Confidence:0.00})\n");

                totalCaptionConfidence += result.Caption.Confidence;
                captionCount++;
            }

            // Dense captions
            Console.WriteLine("Dense Captions:");
            foreach (var dc in result.DenseCaptions.Values)
            {
                Console.WriteLine($"  \"{dc.Text}\" (Confidence: {dc.Confidence:0.00})");
            }
            Console.WriteLine();

            // Full text
            if (result.Read != null)
            {
                Console.WriteLine("Full Text:");
                foreach (var line in result.Read.Blocks.SelectMany(b => b.Lines))
                {
                    Console.WriteLine($"  {line.Text}");
                }
                Console.WriteLine();

                Console.WriteLine("Individual Words:");
                foreach (var word in result.Read.Blocks.SelectMany(b => b.Lines).SelectMany(l => l.Words))
                {
                    Console.WriteLine($"  {word.Text} (Confidence: {word.Confidence:P2})");
                }
                Console.WriteLine();
            }

            Console.WriteLine("Analysis complete.\n");

            // Annotate results
            string baseOut = Path.Combine(outDir, $"{name}{suffix}.jpg");
            AnnotateLines(pageBitmap, result.Read, baseOut);
            AnnotateWords(pageBitmap, result.Read, baseOut);

            // Track caption confidence
            if (!string.IsNullOrEmpty(result.Caption?.Text))
            {
                Console.WriteLine($"Caption Confidence for {name}{suffix}: {result.Caption.Confidence:0.00}");
                totalCaptionConfidence += result.Caption.Confidence;
                captionCount++;
            }

            Console.WriteLine(
                $"Saved annotated images: {Path.Combine(outDir, name + suffix + "_lines.jpg")} and {Path.Combine(outDir, name + suffix + "_words.jpg")}"
            );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error analyzing {name}{suffix}: {ex.Message}");
        }
        finally
        {
            // Restore original console output
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
        static void AnnotateLines(SKBitmap bitmap, ReadResult readResult, string baseOutPath)
        {
            using var canvas = new SKCanvas(bitmap);
            using var paint = new SKPaint
            {
                Color = SKColors.Cyan,
                StrokeWidth = 3,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            foreach (var line in readResult.Blocks.SelectMany(b => b.Lines))
            {
                var pts = line.BoundingPolygon;
                var poly = new[]
                {
                    new SKPoint(pts[0].X, pts[0].Y),
                    new SKPoint(pts[1].X, pts[1].Y),
                    new SKPoint(pts[2].X, pts[2].Y),
                    new SKPoint(pts[3].X, pts[3].Y)
                };
                DrawPolygon(canvas, poly, paint);
            }

            string linesPath = baseOutPath.Replace(".jpg", "_lines.jpg");
            using var w = new SKFileWStream(linesPath);
            bitmap.Encode(w, SKEncodedImageFormat.Jpeg, 50);
            Console.WriteLine($"  Lines image saved to {linesPath}");
        }

        static void AnnotateWords(SKBitmap bitmap, ReadResult readResult, string baseOutPath)
        {
            using var canvas = new SKCanvas(bitmap);
            using var paint = new SKPaint
            {
                Color = SKColors.Cyan,
                StrokeWidth = 3,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            foreach (var word in readResult.Blocks.SelectMany(b => b.Lines).SelectMany(l => l.Words))
            {
                var pts = word.BoundingPolygon;
                var poly = new[]
                {
                    new SKPoint(pts[0].X, pts[0].Y),
                    new SKPoint(pts[1].X, pts[1].Y),
                    new SKPoint(pts[2].X, pts[2].Y),
                    new SKPoint(pts[3].X, pts[3].Y)
                };
                DrawPolygon(canvas, poly, paint);
            }

            string wordsPath = baseOutPath.Replace(".jpg", "_words.jpg");
            using var w = new SKFileWStream(wordsPath);
            bitmap.Encode(w, SKEncodedImageFormat.Jpeg, 50);
            Console.WriteLine($"  Words image saved to {wordsPath}");
        }

        static void DrawPolygon(SKCanvas canvas, SKPoint[] poly, SKPaint paint)
        {
            for (int i = 0; i < poly.Length; i++)
            {
                var start = poly[i];
                var end = poly[(i + 1) % poly.Length];
                canvas.DrawLine(start, end, paint);
            }
        }
    }
}


