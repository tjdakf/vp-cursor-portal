param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Output = "artifacts\vp-cursor-portal-win-x64",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

Write-Host "Restoring solution..."
dotnet restore H2CursorRouter.sln

Write-Host "Building solution..."
dotnet build H2CursorRouter.sln --configuration $Configuration --no-restore

Write-Host "Running tests..."
dotnet test H2CursorRouter.sln --configuration $Configuration --no-build

Write-Host "Publishing WPF app..."
$selfContainedValue = if ($SelfContained) { "true" } else { "false" }
dotnet publish src\H2CursorRouter.App\H2CursorRouter.App.csproj `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained $selfContainedValue `
    --output $Output

Copy-Item config.sample.json $Output -Force

Write-Host ""
Write-Host "Published to: $Output"
Write-Host "Run H2CursorRouter.App.exe on Windows."
