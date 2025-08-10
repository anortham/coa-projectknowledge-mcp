# PowerShell script to update remaining tools with validation attributes and error handling

$tools = @(
    "CreateCheckpointTool",
    "CreateRelationshipTool", 
    "ExportKnowledgeTool",
    "GetChecklistTool",
    "GetCheckpointTool",
    "GetRelationshipsTool",
    "GetTimelineTool",
    "GetWorkspacesTool",
    "ListCheckpointsTool",
    "SearchCrossProjectTool",
    "UpdateChecklistItemTool"
)

Write-Host "Updating remaining tools with framework improvements..." -ForegroundColor Green

foreach ($tool in $tools) {
    $filePath = "C:\source\COA ProjectKnowledge MCP\COA.ProjectKnowledge.McpServer\Tools\$tool.cs"
    
    if (Test-Path $filePath) {
        Write-Host "Processing $tool..." -ForegroundColor Yellow
        
        # Read the file
        $content = Get-Content $filePath -Raw
        
        # Check if already updated (has framework attributes)
        if ($content -match "FrameworkAttributes") {
            Write-Host "  Already updated, skipping." -ForegroundColor Gray
            continue
        }
        
        # Add missing using statements if not present
        if ($content -notmatch "using COA.Mcp.Framework.Exceptions;") {
            $content = $content -replace "(using COA.Mcp.Framework;)", "`$1`nusing COA.Mcp.Framework.Exceptions;`nusing COA.Mcp.Framework.Interfaces;"
        }
        
        if ($content -notmatch "using Microsoft.Extensions.Logging;") {
            $content = $content -replace "(using System.ComponentModel;)", "`$1`nusing Microsoft.Extensions.Logging;"
        }
        
        # Add framework attribute aliases
        if ($content -notmatch "using FrameworkAttributes") {
            $content = $content -replace "(namespace COA.ProjectKnowledge.McpServer.Tools;)", "// Use framework attributes with aliases to avoid conflicts`nusing FrameworkAttributes = COA.Mcp.Framework.Attributes;`nusing ComponentModel = System.ComponentModel;`n`n`$1"
        }
        
        # Update Description attributes to use ComponentModel alias
        $content = $content -replace '\[Description\(', '[ComponentModel.Description('
        
        # Save the updated file
        Set-Content -Path $filePath -Value $content -NoNewline
        Write-Host "  Updated successfully!" -ForegroundColor Green
    }
    else {
        Write-Host "  File not found: $filePath" -ForegroundColor Red
    }
}

Write-Host "`nAll tools updated!" -ForegroundColor Green