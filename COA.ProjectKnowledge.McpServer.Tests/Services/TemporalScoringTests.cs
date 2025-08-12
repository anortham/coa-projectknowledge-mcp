using COA.ProjectKnowledge.McpServer.Models;
using COA.ProjectKnowledge.McpServer.Services;
using COA.ProjectKnowledge.McpServer.Tests.TestBase;
using FluentAssertions;
using NUnit.Framework;

namespace COA.ProjectKnowledge.McpServer.Tests.Services;

[TestFixture]
public class TemporalScoringTests : ProjectKnowledgeTestBase
{
    [Test]
    public void TemporalDecayFunction_Default_CalculatesCorrectDecay()
    {
        // Arrange
        var decayFunction = TemporalDecayFunction.Default;
        
        // Act & Assert
        var dayZero = decayFunction.Calculate(0); // Just created
        var dayOne = decayFunction.Calculate(1);  // 1 day old
        var dayThirty = decayFunction.Calculate(30); // 1 month old (half-life)
        var dayNinety = decayFunction.Calculate(90); // 3 months old
        
        // Debug output
        Console.WriteLine($"Day 0: {dayZero}, Day 1: {dayOne}, Day 30: {dayThirty}, Day 90: {dayNinety}");
        
        // Assertions
        dayZero.Should().Be(1.0f, "newly created items should have full score");
        dayOne.Should().BeLessThan(1.0f, "1-day old items should have reduced score");
        dayThirty.Should().BeApproximately(0.5f, 0.1f, "30-day old items should be near half-life");
        dayNinety.Should().BeLessThan(dayThirty, "90-day old items should score lower than 30-day old");
    }
    
    [Test]
    public void TemporalDecayFunction_Aggressive_DecaysFasterThanDefault()
    {
        // Arrange
        var aggressive = TemporalDecayFunction.Aggressive;
        var defaultDecay = TemporalDecayFunction.Default;
        
        // Act
        var aggressiveScore = aggressive.Calculate(7); // 1 week old
        var defaultScore = defaultDecay.Calculate(7);
        
        // Assert
        aggressiveScore.Should().BeLessThan(defaultScore, "aggressive decay should reduce scores faster");
    }
    
    [Test]
    public void TemporalDecayFunction_Gentle_DecaysSlowerThanDefault()
    {
        // Arrange
        var gentle = TemporalDecayFunction.Gentle;
        var defaultDecay = TemporalDecayFunction.Default;
        
        // Act
        var gentleScore = gentle.Calculate(30); // 1 month old
        var defaultScore = defaultDecay.Calculate(30);
        
        // Assert
        gentleScore.Should().BeGreaterThan(defaultScore, "gentle decay should preserve scores longer");
    }
    
    [Test]
    public void TemporalDecayFunction_FutureDates_ReturnFullScore()
    {
        // Arrange
        var decayFunction = TemporalDecayFunction.Default;
        
        // Act
        var futureScore = decayFunction.Calculate(-1); // Future date
        
        // Assert
        futureScore.Should().Be(1.0f, "future dates should not be penalized");
    }
    
    [Test]
    public async Task KnowledgeSearchParameters_TemporalScoring_AffectsResultOrder()
    {
        // Arrange
        var knowledgeService = GetRequiredService<KnowledgeService>();
        
        // Create test knowledge items with different ages
        var oldKnowledge = new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "Old knowledge item",
            Tags = new[] { "test" }
        };
        
        var newKnowledge = new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "New knowledge item",
            Tags = new[] { "test" }
        };
        
        // Store old item first
        var oldResult = await knowledgeService.StoreKnowledgeAsync(oldKnowledge);
        oldResult.Success.Should().BeTrue();
        
        // Wait a moment to ensure different timestamps
        await Task.Delay(10);
        
        // Store new item
        var newResult = await knowledgeService.StoreKnowledgeAsync(newKnowledge);
        newResult.Success.Should().BeTrue();
        
        // Act - Search with temporal scoring enabled
        var searchParams = new KnowledgeSearchParameters
        {
            Query = "knowledge",
            TemporalScoring = TemporalScoringMode.Aggressive,
            BoostRecent = true,
            MaxResults = 10
        };
        
        var searchResponse = await knowledgeService.SearchEnhancedAsync(searchParams);
        
        // Assert
        searchResponse.Success.Should().BeTrue();
        searchResponse.Items.Should().HaveCountGreaterOrEqualTo(2);
        
        // The newer item should appear before the older item
        var newItemIndex = searchResponse.Items.FindIndex(i => i.Id == newResult.KnowledgeId);
        var oldItemIndex = searchResponse.Items.FindIndex(i => i.Id == oldResult.KnowledgeId);
        
        FluentAssertions.AssertionExtensions.Should(newItemIndex).BeLessThan(oldItemIndex, "newer item should rank higher with temporal scoring");
    }
    
    [Test]
    public async Task KnowledgeSearchParameters_BoostFrequent_AffectsResultOrder()
    {
        // Arrange
        var knowledgeService = GetRequiredService<KnowledgeService>();
        
        // Create test knowledge items
        var knowledge1 = new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "Frequently accessed knowledge",
            Tags = new[] { "boost-test" }
        };
        
        var knowledge2 = new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "Rarely accessed knowledge",
            Tags = new[] { "boost-test" }
        };
        
        var result1 = await knowledgeService.StoreKnowledgeAsync(knowledge1);
        var result2 = await knowledgeService.StoreKnowledgeAsync(knowledge2);
        
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        
        // Simulate frequent access to first item by getting it multiple times
        for (int i = 0; i < 5; i++)
        {
            await knowledgeService.GetKnowledgeAsync(result1.KnowledgeId!);
        }
        
        // Act - Search with frequent boosting enabled
        var searchParams = new KnowledgeSearchParameters
        {
            Query = "knowledge",
            TemporalScoring = TemporalScoringMode.Default,
            BoostFrequent = true,
            MaxResults = 10
        };
        
        var searchResponse = await knowledgeService.SearchEnhancedAsync(searchParams);
        
        // Assert
        searchResponse.Success.Should().BeTrue();
        
        var frequentItem = searchResponse.Items.FirstOrDefault(i => i.Id == result1.KnowledgeId);
        var rareItem = searchResponse.Items.FirstOrDefault(i => i.Id == result2.KnowledgeId);
        
        frequentItem.Should().NotBeNull();
        rareItem.Should().NotBeNull();
        
        // The frequently accessed item should have higher access count
        FluentAssertions.AssertionExtensions.Should(frequentItem!.AccessCount).BeGreaterThan(rareItem!.AccessCount);
    }
    
    [Test]
    public async Task KnowledgeSearchParameters_DateRangeFilter_FiltersCorrectly()
    {
        // Arrange
        var knowledgeService = GetRequiredService<KnowledgeService>();
        
        var knowledge = new StoreKnowledgeRequest
        {
            Type = KnowledgeTypes.WorkNote,
            Content = "Date range test knowledge",
            Tags = new[] { "date-test" }
        };
        
        var result = await knowledgeService.StoreKnowledgeAsync(knowledge);
        result.Success.Should().BeTrue();
        
        // Act - Search with date range that excludes the item
        var searchParams = new KnowledgeSearchParameters
        {
            Query = "date range",
            FromDate = DateTime.UtcNow.AddDays(1), // Tomorrow
            ToDate = DateTime.UtcNow.AddDays(2),   // Day after tomorrow
            MaxResults = 10
        };
        
        var searchResponse = await knowledgeService.SearchEnhancedAsync(searchParams);
        
        // Assert
        searchResponse.Success.Should().BeTrue();
        searchResponse.Items.Should().NotContain(i => i.Id == result.KnowledgeId, 
            "items created today should be excluded from future date range");
        
        // Act - Search with date range that includes the item
        var inclusiveParams = new KnowledgeSearchParameters
        {
            Query = "date range",
            FromDate = DateTime.UtcNow.AddDays(-1), // Yesterday
            ToDate = DateTime.UtcNow.AddDays(1),    // Tomorrow
            MaxResults = 10
        };
        
        var inclusiveResponse = await knowledgeService.SearchEnhancedAsync(inclusiveParams);
        
        // Assert
        inclusiveResponse.Success.Should().BeTrue();
        inclusiveResponse.Items.Should().Contain(i => i.Id == result.KnowledgeId,
            "items created today should be included in inclusive date range");
    }
}