#!/usr/bin/env bash
set -euo pipefail

# Usage: ./collect_jpgs.sh [folder] [output_json]
#   folder:       directory to scan (non‐recursive; default: .)
#   output_json:  file to write JSON to (default: images.json)

folder="${1:-.}"
output_file="${2:-images.json}"

# Start JSON array
printf '[\n' > "$output_file"

first=true
# Find only *.jpg files in the specified folder (no recursion)
while IFS= read -r img; do
  # Compute the absolute path
  linuxpath=$(realpath "$img")

  # Convert to Windows‐style path, e.g. /c/Users/... → C:\Users\...
  winpath=$(echo "$linuxpath" | sed -E 's|^/([A-Za-z])/(.*)|\1:\\\2|; s|/|\\|g')

  # Add comma between entries
  if $first; then
    first=false
  else
    printf ',\n' >> "$output_file"
  fi

  # Escape backslashes and quotes for JSON
  esc=$(printf '%s' "$winpath" | sed 's/\\/\\\\/g; s/"/\\"/g')

  # Append JSON object
  printf '  {\n    "path": "%s",\n    "split": true\n  }' "$esc" >> "$output_file"
done < <(find "$folder" -maxdepth 1 -type f -iname '*.jpg')

# Close JSON array
printf '\n]\n' >> "$output_file"

count=$(grep -c '"path"' "$output_file" || echo 0)
echo "Generated JSON with $count entries in '$output_file'."
