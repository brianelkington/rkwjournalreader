# Image Text Processor CLI

A .NET console application that automates reading and annotating text from scanned documents (JPEGs), splitting two-page images around a spiral binder into left and right pages, and logging results per page.

---

## Features

- **Batch processing**: Enumerates all `.jpg`/`.jpeg` files in a specified folder.
- **Spiral binder split**: Detects a central binding coil and splits each scan into left/right halves.
- **EXIF orientation correction**: Reads and applies orientation metadata so images always appear right-side-up.
- **Azure AI Vision integration**: Uses the Azure AI Vision ImageAnalysisClient to:
  - Extract full text and individual words
  - Generate image captions and dense captions
- **Annotation output**: Draws bounding boxes around recognized lines and words on output images (saved as `<name>_L_lines.jpg`, `<name>_L_words.jpg`, etc.).
- **Per-page logging**: Captures console output for each side in `image_out/<name>_L.out` and `image_out/<name>_R.out`.
- **Timing and confidence metrics**: Logs the time taken per page and overall, plus per-page and aggregate caption confidence.

---

## Prerequisites

1. **.NET 6.0 SDK** (or later)  
2. **appsettings.json** in the working directory, containing:
   ```json
   {
     "AIServicesEndpoint": "<your-vision-endpoint-uri>",
     "AIServicesKey": "<your-vision-api-key>"
   }
---

## Installation

```bash
# Clone the repository
git clone <repo-url>
cd <repo-folder>

# Restore NuGet packages
dotnet restore

# Build the app
dotnet build -c Release
```

---

## Configuration

Create an **appsettings.json** alongside `Program.cs` with the following structure:

```json
{
  "AIServicesEndpoint": "https://<your-resource>.cognitiveservices.azure.com/",
  "AIServicesKey": "<your-access-key>"
}
```

---

## Usage

Run the application against a folder of scans:

```bash
dotnet run -- [<image-folder>] [--save-images]
```

- If no folder is provided, it defaults to the `images` folder included in the repository.
- All JPEGs in the folder will be processed.

---

## Output

After running, you'll find an **image_out** folder next to each original image containing:

- `<name>_L.jpg` and `<name>_R.jpg`: the left/right halves of the original scan
- `<name>_L_lines.jpg`, `<name>_R_lines.jpg`: annotated line-bounding images (if --save-images)
- `<name>_L_words.jpg`, `<name>_R_words.jpg`: annotated word-bounding images (if --save-images)
- `<name>_L.out`, `<name>_R.out`: console logs with timing and caption details

An overall summary (total time, average caption confidence) is printed to the console at the end.

---

## Example

```bash
# Process default 'images/' folder, generate only .out logs
dotnet run

# Process 'scans/' folder, generate only .out logs
dotnet run -- scans

# Process default folder AND save annotated JPEGs
dotnet run -- --save-images

# Process 'scans/' AND save annotated JPEGs
dotnet run -- scans --save-images

---

## License

MIT © Brian Elkington
