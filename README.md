# rkwjournalreader

A .NET console application that processes scanned images, splits each scan into left/right pages, runs Azure AI Vision OCR/caption analysis, and logs results per page. Optionally annotates and saves word‐bounding JPEGs.

---

## Features

- **Input scanning**: Reads all `.jpg`/`.jpeg` files from a specified folder (default `.\images`).  
- **EXIF correction**: Auto-rotates images based on their EXIF orientation tag.  
- **Page splitting**: Splits each full-scan into left/right halves around a configurable binder gap (`BinderWidth`) :contentReference[oaicite:17]{index=17}.  
- **Azure AI Vision**: Extracts OCR text, captions, and dense captions using `VisualFeatures.Read | Caption | DenseCaptions` :contentReference[oaicite:18]{index=18}.  
- **Per‐page logging**: Writes a `<scanname>_L.txt` and `<scanname>_R.txt` log into `image_out/`, capturing all console output (including word-by-word confidences in verbose mode) :contentReference[oaicite:19]{index=19}.  
- **Optional image output**: With `--save-images`, saves annotated JPEGs showing word bounding boxes (`*_words.jpg`) into `image_out/` :contentReference[oaicite:20]{index=20}.  
- **Verbose mode**: With `--verbose`, prints extra details: page dimensions, dense captions, and individual word confidences :contentReference[oaicite:21]{index=21}.  
- **Metrics**: Reports total pages with captions, average caption confidence, and total processing time at completion :contentReference[oaicite:22]{index=22}.

---

## Prerequisites

1. **.NET 6.0 SDK** (or later)  
2. **Azure Cognitive Services Vision** endpoint & key.  
3. **`appsettings.json`** in the working directory:
   ```json
   {
     "AIServicesEndpoint": "https://<your-resource>.cognitiveservices.azure.com/",
     "AIServicesKey": "<your-vision-key>"
   }
