# Bootstrap script for mcpyrex-dotnet MCP server
# This script downloads the latest version and runs the installation
param(
    [string]$WorkDir = ".mcp-dotnet"
)

# Colors for output
$Green = "Green"
$Blue = "Blue" 
$Yellow = "Yellow"
$Red = "Red"

function Write-Color {
    param([string]$Message, [string]$Color)
    Write-Host ""
    Write-Host $Message -ForegroundColor $Color
    Write-Host ""
}

Write-Color "🚀 mcpyrex-dotnet MCP Server Bootstrap" $Green
Write-Color "This script will download and install the latest version of mcpyrex-dotnet" $Blue

# Configuration
$work = $WorkDir
$url = "https://github.com/mcpyrex/mcpyrex-dotnet/archive/refs/heads/main.zip"

try {
    Write-Color "📥 Downloading latest version from GitHub..." $Yellow
    
    # Create work directory
    New-Item -ItemType Directory -Force -Path $work | Out-Null
    
    # Download zip file
    Invoke-WebRequest -Uri $url -OutFile "$work\project.zip"
    Write-Host "✓ Downloaded project archive" -ForegroundColor Green
    
    # Extract archive
    Write-Color "📂 Extracting files..." $Yellow
    Expand-Archive -Path "$work\project.zip" -DestinationPath "$work\tmp" -Force
    Remove-Item "$work\project.zip"
    
    # Move files to final location
    Write-Color "📁 Moving files to final location..." $Yellow
    Move-Item "$work\tmp\mcpyrex-dotnet-main\*" "$work" -Force
    Move-Item "$work\tmp\mcpyrex-dotnet-main\.*" "$work" -Force -ErrorAction SilentlyContinue
    Remove-Item "$work\tmp" -Recurse -Force
    Write-Host "✓ Files extracted successfully" -ForegroundColor Green
    
    # Navigate to build directory and run installation
    Write-Color "🔧 Starting installation process..." $Green
    Set-Location "$work\build"
    
    if (Test-Path ".\install.ps1") {
        Write-Color "Running install.ps1..." $Blue
        .\install.ps1
    } else {
        Write-Color "Error: install.ps1 not found in build directory!" $Red
        exit 1
    }
    
} catch {
    Write-Color "❌ Error occurred during bootstrap process:" $Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Color "Please check your internet connection and try again." $Yellow
    exit 1
}

Write-Color "✅ Bootstrap process completed!" $Green
