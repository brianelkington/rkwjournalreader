# read_journal

A .NET console app that splits spiral-bound scanned images (or processes a JSON list), runs Azure AI Vision OCR, logs word-by-word confidences, computes average word confidence per page and overall, and optionally emits annotated JPEGs.

---

## Features

- **Folder or JSON input**  
  - Pass a path to a folder of `.jpg`/`.jpeg` files (default: `images/`) → each image is split into left/right pages.  
  - Or pass a path to a JSON file containing an array of `{ "path": "...", "split": true|false }` entries → honor each entry’s split flag.  
- **Vertical, midpoint splitting**  
  - Splitting is always a vertical cut halfway across the image width (you can enable or disable per entry).  
- **EXIF orientation**  
  - Detects and corrects camera rotation metadata before processing.  
- **OCR & Word Confidences**  
  - Uses Azure AI Vision to extract text and per-word confidence scores.  
  - Logs each word’s confidence and computes an **average word confidence** per page.  
- **Per-page logging & aggregation**  
  - Writes one `<imagename>_L.out` and/or `<imagename>_R.out` file per page under `image_out/`.  
  - All console output is also appended to a single `aggregator.txt`.  
- **Optional annotated images** (`--save-images`)  
  - Saves annotated JPEGs under `image_out/`.  
  - **Word bounding boxes** are drawn **only if** the `--verbose` flag is also set.  
- **Verbose mode** (`--verbose`)  
  - Enables extra console/log detail (e.g. dense captions, skip messages) and causes word bounding boxes to appear in the saved JPEGs.

---

## Prerequisites

1. **.NET 6.0 SDK** (or later)  
2. **Azure Cognitive Services Vision** endpoint & key  
3. A valid `appsettings.json` in the working directory:
   ```json
   {
     "AIServicesEndpoint": "https://<your-resource>.cognitiveservices.azure.com/",
     "AIServicesKey":      "<your-vision-key>"
   }
