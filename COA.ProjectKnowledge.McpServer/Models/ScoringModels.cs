namespace COA.ProjectKnowledge.McpServer.Models;

/// <summary>
/// Temporal scoring modes for knowledge search
/// </summary>
public enum TemporalScoringMode
{
    /// <summary>
    /// No temporal scoring - original relevance only
    /// </summary>
    None,
    
    /// <summary>
    /// Default temporal scoring - moderate decay over 30 days
    /// </summary>
    Default,
    
    /// <summary>
    /// Aggressive temporal scoring - strongly favor recent knowledge (7 day half-life)
    /// </summary>
    Aggressive,
    
    /// <summary>
    /// Gentle temporal scoring - slow decay over long periods (90 day half-life)
    /// </summary>
    Gentle
}

/// <summary>
/// Types of temporal decay functions for scoring
/// </summary>
public enum DecayType
{
    /// <summary>
    /// Exponential decay - aggressive aging
    /// </summary>
    Exponential,
    
    /// <summary>
    /// Linear decay - gradual aging
    /// </summary>
    Linear,
    
    /// <summary>
    /// Gaussian decay - bell curve aging
    /// </summary>
    Gaussian
}

/// <summary>
/// Temporal decay function for scoring knowledge relevance based on age
/// </summary>
public class TemporalDecayFunction
{
    /// <summary>
    /// Rate of decay (0.0 to 1.0, where 1.0 = no decay)
    /// </summary>
    public float DecayRate { get; set; } = 0.95f;
    
    /// <summary>
    /// Half-life in days (time for score to decay to 50%)
    /// </summary>
    public float HalfLife { get; set; } = 30;
    
    /// <summary>
    /// Type of decay function to apply
    /// </summary>
    public DecayType Type { get; set; } = DecayType.Exponential;
    
    /// <summary>
    /// Default decay function - moderate exponential decay
    /// </summary>
    public static TemporalDecayFunction Default => new TemporalDecayFunction();
    
    /// <summary>
    /// Aggressive decay - strongly favors recent knowledge
    /// </summary>
    public static TemporalDecayFunction Aggressive => new TemporalDecayFunction
    {
        DecayRate = 0.9f,
        HalfLife = 7,
        Type = DecayType.Exponential
    };
    
    /// <summary>
    /// Gentle decay - slowly ages knowledge over long periods
    /// </summary>
    public static TemporalDecayFunction Gentle => new TemporalDecayFunction
    {
        DecayRate = 0.98f,
        HalfLife = 90,
        Type = DecayType.Linear
    };
    
    /// <summary>
    /// Calculate the temporal boost factor for a given age
    /// </summary>
    /// <param name="ageInDays">Age of the knowledge in days</param>
    /// <returns>Boost factor (0.0 to 1.0+)</returns>
    public float Calculate(double ageInDays)
    {
        if (ageInDays < 0) return 1.0f; // Future dates get no penalty
        
        return Type switch
        {
            DecayType.Exponential => (float)Math.Pow(0.5, ageInDays / HalfLife),
            DecayType.Linear => Math.Max(0.1f, 1 - (float)(ageInDays / (HalfLife * 2))),
            DecayType.Gaussian => (float)Math.Exp(-Math.Pow(ageInDays / (HalfLife * 0.5), 2)),
            _ => 1.0f
        };
    }
    
    /// <summary>
    /// Get a descriptive name for this decay function
    /// </summary>
    public string GetDescription()
    {
        var typeName = Type.ToString().ToLowerInvariant();
        return $"{typeName} decay (rate: {DecayRate:F2}, half-life: {HalfLife:F0} days)";
    }
}

/// <summary>
/// Enhanced search parameters for knowledge queries
/// </summary>
public class KnowledgeSearchParameters
{
    /// <summary>
    /// Search query text
    /// </summary>
    public string? Query { get; set; }
    
    /// <summary>
    /// Filter by knowledge types
    /// </summary>
    public string[]? Types { get; set; }
    
    /// <summary>
    /// Filter by workspace
    /// </summary>
    public string? Workspace { get; set; }
    
    /// <summary>
    /// Maximum results to return
    /// </summary>
    public int MaxResults { get; set; } = 50;
    
    /// <summary>
    /// Order by field (created, modified, accessed, relevance)
    /// </summary>
    public string? OrderBy { get; set; }
    
    /// <summary>
    /// Order descending
    /// </summary>
    public bool OrderDescending { get; set; } = true;
    
    /// <summary>
    /// Boost recent knowledge in scoring
    /// </summary>
    public bool BoostRecent { get; set; } = true;
    
    /// <summary>
    /// Boost frequently accessed knowledge
    /// </summary>
    public bool BoostFrequent { get; set; } = false;
    
    /// <summary>
    /// Temporal scoring mode to apply
    /// </summary>
    public TemporalScoringMode TemporalScoring { get; set; } = TemporalScoringMode.Default;
    
    /// <summary>
    /// Include archived/deleted items
    /// </summary>
    public bool IncludeArchived { get; set; } = false;
    
    /// <summary>
    /// Date range filter - from date
    /// </summary>
    public DateTime? FromDate { get; set; }
    
    /// <summary>
    /// Date range filter - to date
    /// </summary>
    public DateTime? ToDate { get; set; }
    
    /// <summary>
    /// Tags to filter by (any match)
    /// </summary>
    public string[]? Tags { get; set; }
    
    /// <summary>
    /// Priority levels to filter by
    /// </summary>
    public string[]? Priorities { get; set; }
    
    /// <summary>
    /// Status values to filter by
    /// </summary>
    public string[]? Statuses { get; set; }
}