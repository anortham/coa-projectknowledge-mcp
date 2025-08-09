# ProjectKnowledge as a Global .NET Tool

## Why This is Perfect

A global .NET tool is ideal for ProjectKnowledge because:
- ✅ **One command installation**: `dotnet tool install -g COA.ProjectKnowledge`
- ✅ **System-wide availability**: Accessible from any directory
- ✅ **Auto-updates**: `dotnet tool update -g COA.ProjectKnowledge`
- ✅ **Version management**: `dotnet tool install -g COA.ProjectKnowledge --version 1.2.0`
- ✅ **No path configuration**: Automatically in PATH
- ✅ **Cross-platform**: Works on Windows, Mac, Linux

## Implementation Changes Needed

### 1. Update Project File for Tool Packaging

```xml
<!-- COA.ProjectKnowledge.McpServer.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    
    <!-- Add these for .NET tool packaging -->
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>projectknowledge</ToolCommandName>
    <PackageId>COA.ProjectKnowledge</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Team</Authors>
    <Description>Centralized knowledge management MCP server for development teams</Description>
    <PackageTags>mcp;knowledge;development;tools</PackageTags>
    <RepositoryUrl>https://github.com/yourorg/projectknowledge</RepositoryUrl>
    <PackageOutputPath>./nupkg</PackageOutputPath>
    
    <!-- Include all necessary files in the tool package -->
    <IncludeContentInPack>true</IncludeContentInPack>
  </PropertyGroup>

  <!-- Existing package references -->
  <ItemGroup>
    <PackageReference Include="COA.Mcp.Framework" Version="1.4.2" />
    <!-- ... other references ... -->
  </ItemGroup>

  <!-- Include configuration files in the package -->
  <ItemGroup>
    <Content Include="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <PackageCopyToOutput>true</PackageCopyToOutput>
    </Content>
  </ItemGroup>
</Project>
```

### 2. Create NuGet Package

```bash
# Build and pack
dotnet pack -c Release

# This creates: ./nupkg/COA.ProjectKnowledge.1.0.0.nupkg
```

### 3. Publish to NuGet or Private Feed

```bash
# Option A: Public NuGet
dotnet nuget push ./nupkg/COA.ProjectKnowledge.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json

# Option B: Private Azure Artifacts/GitHub Packages
dotnet nuget push ./nupkg/COA.ProjectKnowledge.1.0.0.nupkg \
  --api-key YOUR_PAT \
  --source "https://pkgs.dev.azure.com/yourorg/_packaging/yourfeed/nuget/v3/index.json"
```

## New User Installation Experience

### Super Simple Setup

```bash
# 1. Install ProjectKnowledge globally (one time)
dotnet tool install -g COA.ProjectKnowledge

# 2. Verify installation
projectknowledge --version

# 3. Initialize configuration
projectknowledge init

# 4. Run in MCP mode
projectknowledge mcp
```

### The `init` Command

Add an initialization command to set up Claude Code configuration:

```csharp
// Add to Program.cs
if (args.Contains("init"))
{
    await InitializeUserConfiguration();
    return;
}

static async Task InitializeUserConfiguration()
{
    Console.WriteLine("🚀 ProjectKnowledge Setup");
    Console.WriteLine("========================");
    
    // 1. Create database directory
    var dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".coa", "knowledge"
    );
    Directory.CreateDirectory(dbPath);
    Console.WriteLine($"✅ Database directory: {dbPath}");
    
    // 2. Find Claude Code config
    var claudeConfigPath = Environment.OSVersion.Platform == PlatformID.Win32NT
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "Claude", "claude_desktop_config.json")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "Claude", "claude_desktop_config.json");
    
    // 3. Update Claude config
    if (File.Exists(claudeConfigPath))
    {
        var config = JsonSerializer.Deserialize<Dictionary<string, object>>(
            File.ReadAllText(claudeConfigPath)
        );
        
        // Add ProjectKnowledge to mcpServers
        var mcpServers = config["mcpServers"] as Dictionary<string, object> ?? new();
        mcpServers["projectknowledge"] = new
        {
            command = "projectknowledge",
            args = new[] { "mcp" },
            env = new
            {
                PROJECTKNOWLEDGE_DB_PATH = Path.Combine(dbPath, "workspace.db")
            }
        };
        
        config["mcpServers"] = mcpServers;
        
        File.WriteAllText(claudeConfigPath, 
            JsonSerializer.Serialize(config, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            })
        );
        
        Console.WriteLine("✅ Claude Code configuration updated");
    }
    else
    {
        Console.WriteLine("⚠️  Claude Code config not found. Add manually:");
        Console.WriteLine(@"
{
  ""mcpServers"": {
    ""projectknowledge"": {
      ""command"": ""projectknowledge"",
      ""args"": [""mcp""],
      ""env"": {
        ""PROJECTKNOWLEDGE_DB_PATH"": """ + Path.Combine(dbPath, "workspace.db") + @"""
      }
    }
  }
}");
    }
    
    // 4. Test HTTP endpoint
    Console.WriteLine("\n📡 Starting test server...");
    var cts = new CancellationTokenSource();
    var testTask = Task.Run(async () =>
    {
        var args = new[] { "--mode", "http", "--port", "5100" };
        await Main(args);
    }, cts.Token);
    
    await Task.Delay(3000);
    
    try
    {
        using var client = new HttpClient();
        var response = await client.GetAsync("http://localhost:5100/api/knowledge/health");
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ HTTP federation endpoint working");
        }
    }
    catch
    {
        Console.WriteLine("⚠️  HTTP endpoint test failed");
    }
    
    cts.Cancel();
    
    Console.WriteLine("\n✨ Setup complete! Restart Claude Code to use ProjectKnowledge.");
}
```

### Update Program.cs for Tool Commands

```csharp
public static async Task Main(string[] args)
{
    // Handle special commands
    if (args.Length > 0)
    {
        switch (args[0])
        {
            case "init":
                await InitializeUserConfiguration();
                return;
                
            case "--version":
            case "-v":
                Console.WriteLine($"ProjectKnowledge v{Assembly.GetExecutingAssembly().GetName().Version}");
                return;
                
            case "--help":
            case "-h":
                ShowHelp();
                return;
                
            case "mcp":
                // Run in MCP mode (STDIO)
                args = args.Skip(1).ToArray();
                break;
                
            case "http":
                // Run in HTTP mode
                args = new[] { "--mode", "http" };
                break;
                
            case "export":
                await ExportKnowledge(args.Skip(1).ToArray());
                return;
                
            case "stats":
                await ShowStatistics();
                return;
        }
    }
    
    // Continue with normal MCP server startup
    await RunMcpServer(args);
}

static void ShowHelp()
{
    Console.WriteLine(@"
ProjectKnowledge - Centralized Development Knowledge Management

Usage: projectknowledge [command] [options]

Commands:
  init          Initialize ProjectKnowledge and configure Claude Code
  mcp           Run as MCP server (STDIO mode) - used by Claude Code
  http          Run HTTP federation endpoint
  export        Export knowledge to Markdown
  stats         Show knowledge statistics
  
Options:
  --version     Show version information
  --help        Show this help message

Examples:
  projectknowledge init           # First-time setup
  projectknowledge mcp            # Run for Claude Code (automatic)
  projectknowledge http           # Run federation endpoint
  projectknowledge export --all   # Export all knowledge

Documentation: https://github.com/yourorg/projectknowledge
");
}
```

## Distribution Strategy

### For Internal Teams

1. **Private NuGet Feed** (Recommended)
```bash
# Add private source
dotnet nuget add source https://pkgs.dev.azure.com/yourorg/_packaging/tools/nuget/v3/index.json \
  --name "CompanyTools"

# Install from private feed
dotnet tool install -g COA.ProjectKnowledge --add-source CompanyTools
```

2. **Network Share**
```bash
# Install from network location
dotnet tool install -g COA.ProjectKnowledge \
  --add-source \\server\nuget-packages
```

### For Open Source

1. **Publish to NuGet.org**
```bash
dotnet tool install -g COA.ProjectKnowledge
```

2. **GitHub Releases**
```bash
# Install specific version from GitHub packages
dotnet tool install -g COA.ProjectKnowledge \
  --add-source https://nuget.pkg.github.com/yourorg/index.json
```

## User Experience Comparison

### Before (Manual Installation)
```bash
# 1. Clone repo
git clone https://github.com/yourorg/projectknowledge
cd projectknowledge

# 2. Build
dotnet build -c Release

# 3. Find the output path
cd bin/Release/net9.0

# 4. Update PATH or create scripts
# 5. Configure Claude manually
# 6. Debug path issues
```

### After (Global Tool)
```bash
# 1. Install
dotnet tool install -g COA.ProjectKnowledge

# 2. Initialize
projectknowledge init

# Done! ✨
```

## Updating ProjectKnowledge

### For Users
```bash
# Check current version
projectknowledge --version

# Update to latest
dotnet tool update -g COA.ProjectKnowledge

# Update to specific version
dotnet tool update -g COA.ProjectKnowledge --version 1.2.0
```

### For Developers (Publishing Updates)
```bash
# 1. Update version in .csproj
<Version>1.1.0</Version>

# 2. Build and pack
dotnet pack -c Release

# 3. Publish
dotnet nuget push ./nupkg/COA.ProjectKnowledge.1.1.0.nupkg --source nuget.org

# 4. Users auto-update
# Users get notified: "Tool 'coa.projectknowledge' has an update available."
```

## CI/CD Pipeline

### GitHub Actions Example
```yaml
name: Publish ProjectKnowledge Tool

on:
  push:
    tags:
      - 'v*'

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '9.0.x'
    
    - name: Build and Pack
      run: |
        dotnet build -c Release
        dotnet pack -c Release
    
    - name: Publish to NuGet
      run: |
        dotnet nuget push ./nupkg/*.nupkg \
          --api-key ${{ secrets.NUGET_API_KEY }} \
          --source https://api.nuget.org/v3/index.json
```

## Summary

Converting ProjectKnowledge to a global .NET tool:

✅ **Simplifies installation** to one command
✅ **Ensures consistency** across all users
✅ **Enables easy updates** with version management
✅ **Provides professional** distribution
✅ **Maintains the system-level** service architecture

This is the ideal distribution model for a system-level MCP server!