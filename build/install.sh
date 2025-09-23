green=92  # stages info
blue=94   # commands in eval_echo
yellow=93 # useful info
red=91    # errors

color() {
    message=$1
    color=$2

    echo -e "\033[${color}m${message}\033[0m"
}

eval_echo() {
    to_run=$1
    echo -e "\033[${blue}m${to_run}\033[0m"

    eval $to_run
}

# check if .NET SDK is available
color "Checking for .NET SDK..." $yellow
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q "8\."; then
    color ".NET SDK 8.x found in system!" $green
    eval_echo "dotnet --version"
else
    color ".NET SDK not found or version < 8.0, installing to project directory..." $yellow
    
    # create .dotnet directory in project root (go up one level from build/)
    eval_echo "mkdir -p ../.dotnet"
    
    # download installer to build directory with error checking
    color "Downloading .NET install script..." $yellow
    if eval_echo "curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh"; then
        eval_echo "chmod +x dotnet-install.sh"
        
        # install .NET SDK to project's .dotnet directory
        if eval_echo "./dotnet-install.sh --channel 8.0 --install-dir ../.dotnet"; then
            # add project's .NET to PATH for current session
            eval_echo "export PATH=\"\$(pwd)/../.dotnet:\$PATH\""
            
            color ".NET SDK installed successfully!" $green
            eval_echo "dotnet --version"
        else
            color "Failed to install .NET SDK!" $red
            exit 1
        fi
    else
        color "Failed to download .NET install script!" $red
        exit 1
    fi
fi

eval_echo "dotnet --info"

# go to project root and create .NET project
eval_echo "cd .."
eval_echo "dotnet new console -n McpDotnet --force"
eval_echo "cd McpDotnet"

# add NuGet packages
eval_echo "dotnet add package ModelContextProtocol --prerelease"
eval_echo "dotnet add package DotNetEnv --version 3.1.0"
eval_echo "dotnet add package YamlDotNet --version 15.1.2"
eval_echo "dotnet add package Microsoft.Extensions.Logging --version 8.0.0"
eval_echo "dotnet add package Microsoft.Extensions.Logging.Console --version 8.0.0"

# Core framework libraries used by multiple tools
eval_echo "dotnet add package Jint --version 4.4.1"  # JavaScript engine for expression evaluation in pipeline system
eval_echo "dotnet add package xunit --version 2.9.3"  # Unit testing framework for tool tests
eval_echo "dotnet add package xunit.runner.visualstudio --version 3.1.4"  # Test runner for xunit
eval_echo "dotnet add package Microsoft.NET.Test.Sdk --version 17.14.1"  # .NET test SDK for running tests

# verify installation
eval_echo "dotnet restore"
eval_echo "dotnet build"

color ".NET MCP server setup complete!" $green
