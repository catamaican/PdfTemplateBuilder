#!/usr/bin/env bash
cfg=${1:-Release}
set -euo pipefail
printf "Building solution (configuration: %s)...\n" "$cfg"
dotnet build PdfTemplateBuilder.sln -c "$cfg"
printf "Build succeeded.\n"