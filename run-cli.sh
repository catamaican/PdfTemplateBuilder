#!/usr/bin/env bash
cfg=${1:-Release}
spec=${2:-template-spec.json}
set -euo pipefail
printf "Running CLI (configuration: %s) with spec: %s\n" "$cfg" "$spec"
dotnet run --project src/PdfTemplateBuilder.Cli/PdfTemplateBuilder.Cli.csproj -c "$cfg" -- "$spec"