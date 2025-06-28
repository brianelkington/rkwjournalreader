#!/usr/bin/env bash
set -euo pipefail

# Usage: ./collect_jpgs.sh [root_dir] [output_json]
#   root_dir:      directory to scan (default: .)
#   output_json:   file to write JSON to (default: images.json)

root_dir="${1:-.}"
output_file="${2:-images.json}"

# Start JSON array
printf '[\n' > "$output_file"

first=true
# Find all .jpg/.JPG files under root_dir
while IFS= read -r img; do
  # Add comma between entries
  if $first; then
    first=false
  else
    printf ',\n' >> "$output_file"
  fi

  # Escape backslashes and quotes in the path
  esc=$(printf '%s' "$img" | sed 's/\\/\\\\/g; s/"/\\"/g')

  # Append JSON object
  printf '  {\n    "path": "%s",\n    "split": true\n  }' "$esc" >> "$output_file"
done < <(find "$root_dir" -type f -iname '*.jpg')

# Close JSON array
printf '\n]\n' >> "$output_file"

echo "Generated JSON with $(grep -c '"path"' "$output_file") entries in '$output_file'."
