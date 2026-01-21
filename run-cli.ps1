param(
    [string] $Configuration = "Release",
    [string] $SpecPath = "template-spec.json"
)

Write-Host "Running CLI (configuration: $Configuration) with spec: $SpecPath"
dotnet run --project src/PdfTemplateBuilder.Cli/PdfTemplateBuilder.Cli.csproj -c $Configuration -- "$SpecPath"