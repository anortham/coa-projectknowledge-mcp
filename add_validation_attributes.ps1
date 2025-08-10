# PowerShell script to add validation attributes to all tool parameter classes

$updates = @{
    "UpdateChecklistItemTool" = @{
        ChecklistId = "[FrameworkAttributes.Required(ErrorMessage = `"Checklist ID is required`")]`n    [FrameworkAttributes.StringLength(50, ErrorMessage = `"Checklist ID cannot exceed 50 characters`")]"
        ItemId = "[FrameworkAttributes.Required(ErrorMessage = `"Item ID is required`")]`n    [FrameworkAttributes.StringLength(50, ErrorMessage = `"Item ID cannot exceed 50 characters`")]"
        IsCompleted = "[FrameworkAttributes.Required(ErrorMessage = `"IsCompleted is required`")]"
    }
    "GetChecklistTool" = @{
        ChecklistId = "[FrameworkAttributes.Required(ErrorMessage = `"Checklist ID is required`")]`n    [FrameworkAttributes.StringLength(50, ErrorMessage = `"Checklist ID cannot exceed 50 characters`")]"
    }
    "GetCheckpointTool" = @{
        CheckpointId = "[FrameworkAttributes.StringLength(50, ErrorMessage = `"Checkpoint ID cannot exceed 50 characters`")]"
        SessionId = "[FrameworkAttributes.StringLength(100, ErrorMessage = `"Session ID cannot exceed 100 characters`")]"
    }
    "ListCheckpointsTool" = @{
        SessionId = "[FrameworkAttributes.Required(ErrorMessage = `"Session ID is required`")]`n    [FrameworkAttributes.StringLength(100, ErrorMessage = `"Session ID cannot exceed 100 characters`")]"
        MaxResults = "[FrameworkAttributes.Range(1, 100, ErrorMessage = `"MaxResults must be between 1 and 100`")]"
    }
    "GetTimelineTool" = @{
        Workspace = "[FrameworkAttributes.StringLength(100, ErrorMessage = `"Workspace cannot exceed 100 characters`")]"
        DaysAgo = "[FrameworkAttributes.Range(1, 365, ErrorMessage = `"DaysAgo must be between 1 and 365`")]"
        HoursAgo = "[FrameworkAttributes.Range(1, 8760, ErrorMessage = `"HoursAgo must be between 1 and 8760`")]"
        MaxResults = "[FrameworkAttributes.Range(1, 1000, ErrorMessage = `"MaxResults must be between 1 and 1000`")]"
        MaxPerGroup = "[FrameworkAttributes.Range(1, 100, ErrorMessage = `"MaxPerGroup must be between 1 and 100`")]"
    }
    "ExportKnowledgeTool" = @{
        OutputPath = "[FrameworkAttributes.StringLength(500, ErrorMessage = `"Output path cannot exceed 500 characters`")]"
        Workspace = "[FrameworkAttributes.StringLength(100, ErrorMessage = `"Workspace cannot exceed 100 characters`")]"
    }
    "SearchCrossProjectTool" = @{
        Query = "[FrameworkAttributes.Required(ErrorMessage = `"Query is required`")]`n    [FrameworkAttributes.StringLength(500, ErrorMessage = `"Query must be between 1 and 500 characters`")]"
        MaxResults = "[FrameworkAttributes.Range(1, 100, ErrorMessage = `"MaxResults must be between 1 and 100`")]"
    }
}

foreach ($toolName in $updates.Keys) {
    $filePath = "C:\source\COA ProjectKnowledge MCP\COA.ProjectKnowledge.McpServer\Tools\$toolName.cs"
    
    if (Test-Path $filePath) {
        Write-Host "Processing $toolName..." -ForegroundColor Yellow
        $content = Get-Content $filePath -Raw
        
        foreach ($paramName in $updates[$toolName].Keys) {
            $validation = $updates[$toolName][$paramName]
            
            # Find the parameter property and add validation
            $pattern = "(\s+)\[ComponentModel\.Description\(([^)]+)\)\]`r?`n(\s+)public ([^{]+) $paramName"
            $replacement = "`$1$validation`r`n`$1[ComponentModel.Description(`$2)]`r`n`$3public `$4 $paramName"
            
            $content = $content -replace $pattern, $replacement
        }
        
        Set-Content -Path $filePath -Value $content -NoNewline
        Write-Host "  Updated with validation attributes!" -ForegroundColor Green
    }
}

Write-Host "`nAll validation attributes added!" -ForegroundColor Green