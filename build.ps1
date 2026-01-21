param(
    [string] $Configuration = "Release"
)

Write-Host "Building solution (configuration: $Configuration)..."
$rc = dotnet build PdfTemplateBuilder.sln -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "Build succeeded."