# .NET MCP Server Installation Script for Windows
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

function Invoke-Echo {
    param([string]$Command)
    Write-Host ""
    Write-Host $Command -ForegroundColor Blue
    Write-Host ""
    Invoke-Expression $Command
}

# Check if .NET SDK is available
Write-Color "Checking for .NET SDK..." $Yellow
$dotnetFound = $false
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $sdks = dotnet --list-sdks 2>$null
    if ($sdks -match "8\.") {
        Write-Color ".NET SDK 8.x found in system!" $Green
        Invoke-Echo "dotnet --version"
        $dotnetFound = $true
    }
}

if (-not $dotnetFound) {
    Write-Color ".NET SDK not found or version < 8.0, installing to project directory..." $Yellow
    
    # Create .dotnet directory in workspace root (2 levels up from build/)
    Invoke-Echo "New-Item -ItemType Directory -Path ..\..\.dotnet -Force"
    
    # Download PowerShell installer
    Write-Color "Downloading .NET install script..." $Yellow
    Invoke-Echo "Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'dotnet-install.ps1'"
    
    # Install .NET SDK to workspace root
    Invoke-Echo ".\dotnet-install.ps1 -Channel 8.0 -InstallDir ..\..\.dotnet"
    
    # Add to PATH for current session
    $workspaceRoot = Resolve-Path "..\..\"
    $dotnetPath = Join-Path $workspaceRoot ".dotnet"
    $env:PATH = "$dotnetPath;$env:PATH"
    Write-Color "Added $dotnetPath to PATH" $Yellow
    
    Write-Color ".NET SDK installed successfully!" $Green
    Invoke-Echo "dotnet --version"
}

Invoke-Echo "dotnet --info"

# Go to project root and create .NET project there
Invoke-Echo "Set-Location .."
Invoke-Echo "dotnet new console --force"

# Add NuGet packages
Invoke-Echo "dotnet add package ModelContextProtocol --prerelease"
Invoke-Echo "dotnet add package DotNetEnv --version 3.1.0"
Invoke-Echo "dotnet add package YamlDotNet --version 15.1.2"
Invoke-Echo "dotnet add package Microsoft.Extensions.Logging --version 8.0.0"
Invoke-Echo "dotnet add package Microsoft.Extensions.Logging.Console --version 8.0.0"
Invoke-Echo "dotnet add package Microsoft.Extensions.Hosting --version 8.0.0"

# Core framework libraries used by multiple tools
Invoke-Echo "dotnet add package Jint --version 4.4.1"  # JavaScript engine for expression evaluation in pipeline system
Invoke-Echo "dotnet add package xunit --version 2.9.3"  # Unit testing framework for tool tests
Invoke-Echo "dotnet add package xunit.runner.visualstudio --version 3.1.4"  # Test runner for xunit
Invoke-Echo "dotnet add package Microsoft.NET.Test.Sdk --version 17.14.1"  # .NET test SDK for running tests

# Verify installation
Invoke-Echo "dotnet restore"
Invoke-Echo "dotnet build"

Write-Color ".NET MCP server setup complete!" $Green
