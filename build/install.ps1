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

function Get-UserChoice {
    param([string]$Prompt, [string[]]$ValidOptions)
    do {
        Write-Color $Prompt $Yellow
        $choice = Read-Host
        $choice = $choice.ToLower()
    } while ($ValidOptions -notcontains $choice)
    return $choice
}

function Show-FileComparison {
    param([string]$CurrentFile, [string]$NewFile, [string]$TargetPath)
    
    Write-Color "=== CURRENT FILE: $TargetPath ===" $Blue
    if (Test-Path $CurrentFile) {
        Get-Content $CurrentFile | Write-Host
    } else {
        Write-Host "(File does not exist)" -ForegroundColor Gray
    }
    
    Write-Color "=== NEW FILE CONTENT ===" $Blue
    Get-Content $NewFile | Write-Host
    
    $replace = Get-UserChoice "Do you want to replace the file? (y/n)" @("y", "n")
    return $replace -eq "y"
}

function Copy-ConfigFile {
    param([string]$SourcePath, [string]$TargetPath, [string]$WorkspaceRoot = $null)
    
    if (-not (Test-Path $SourcePath)) {
        Write-Color "Warning: Source file not found: $SourcePath" $Red
        return
    }
    
    # Create target directory if it doesn't exist
    $targetDir = Split-Path $TargetPath -Parent
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }
    
    # Read and process content
    $content = Get-Content $SourcePath -Raw
    if ($WorkspaceRoot) {
        # For JSON files, escape backslashes
        if ($TargetPath -like "*.json") {
            $escapedPath = $WorkspaceRoot -replace '\\', '\\'
            $content = $content -replace '\{\{workspaceFolder\}\}', $escapedPath
        } else {
            $content = $content -replace '\{\{workspaceFolder\}\}', $WorkspaceRoot
        }
    }
    
    # Create temp file for comparison
    $tempFile = "$env:TEMP\mcp_temp_$(Get-Random).tmp"
    $content | Out-File -FilePath $tempFile -Encoding UTF8
    
    # Show comparison and ask for confirmation
    if (Show-FileComparison $TargetPath $tempFile $TargetPath) {
        # Backup existing file if it exists
        if (Test-Path $TargetPath) {
            $backupPath = "$TargetPath.bak"
            Copy-Item $TargetPath $backupPath -Force
            Write-Color "Created backup: $backupPath" $Yellow
        }
        
        # Copy new content
        $content | Out-File -FilePath $TargetPath -Encoding UTF8
        Write-Color "File copied successfully: $TargetPath" $Green
    } else {
        Write-Color "File copy skipped: $TargetPath" $Yellow
    }
    
    # Clean up temp file
    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
}

# Check if .NET SDK is available
Write-Color "Checking for .NET SDK..." $Yellow
$dotnetFound = $false
$useSystemDotnet = $false

# First check if we have a local .NET installation
$localDotnetPath = Resolve-Path "..\..\.dotnet" -ErrorAction SilentlyContinue
if ($localDotnetPath -and (Test-Path "$localDotnetPath\dotnet.exe")) {
    Write-Color "Local .NET SDK found in project directory!" $Green
    # Add local .NET to PATH for current session
    $env:PATH = "$localDotnetPath;$env:PATH"
    Invoke-Echo "dotnet --version"
    $dotnetFound = $true
    $useSystemDotnet = $false  # Local installation = without-dotnet config
}
elseif (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $sdks = dotnet --list-sdks 2>$null
    if ($sdks -match "8\.") {
        Write-Color ".NET SDK 8.x found in system!" $Green
        Invoke-Echo "dotnet --version"
        $dotnetFound = $true
        $useSystemDotnet = $true  # System installation = with-dotnet config
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

# Determine CONFIG_BASE based on .NET SDK type
if ($useSystemDotnet) {
    $CONFIG_BASE = ".\config\with-dotnet"
    Write-Color "Using configuration for systems with .NET SDK installed globally" $Green
} else {
    $CONFIG_BASE = ".\config\without-dotnet"
    Write-Color "Using configuration for systems without .NET SDK (using local installation)" $Green
}

# Ask user for IDE choice
$ideChoice = Get-UserChoice "Choose your IDE: (c)ursor or (v)scode?" @("c", "v")
$workspaceRoot = Resolve-Path "..\..\"

if ($ideChoice -eq "c") {
    Write-Color "Configuring for Cursor..." $Green
    # Copy mcp.json for Cursor with workspaceFolder replacement
    Copy-ConfigFile "$CONFIG_BASE\.cursor\mcp.json" "..\..\/.cursor\mcp.json" $workspaceRoot
} else {
    Write-Color "Configuring for VSCode..." $Green
    # Copy VSCode configuration files
    Copy-ConfigFile "$CONFIG_BASE\.vscode\mcp.json" "..\..\/.vscode\mcp.json"
    Copy-ConfigFile "$CONFIG_BASE\.vscode\settings.json" "..\..\/.vscode\settings.json"  
    Copy-ConfigFile "$CONFIG_BASE\.github\copilot-instructions.md" "..\..\/.github\copilot-instructions.md"    
}

# Copy .env configuration file
Copy-ConfigFile "$CONFIG_BASE\..\.env" "..\..\/.env" $workspaceRoot

# Restore NuGet packages for existing projects
Write-Color "Restoring NuGet packages..." $Yellow
Invoke-Echo "Set-Location .."
Invoke-Echo "dotnet restore mcp.csproj"
Invoke-Echo "dotnet restore run.csproj"

Write-Color ".NET MCP server setup complete!" $Green

Write-Host ""
Write-Host "Happy coding!" -ForegroundColor Magenta
Write-Host ""

Write-Color "Press any key to exit..." $Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
