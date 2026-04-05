using System.Text.Json;
using Shared;
using Xunit;

namespace RuleService.Tests;

/// <summary>
/// Unit tests for RuleStore persistence and CRUD operations
/// </summary>
public class RuleStoreTests : IDisposable
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), $"rule-store-{Guid.NewGuid()}");
    private readonly string _testFilePath;

    public RuleStoreTests()
    {
        Directory.CreateDirectory(_testDir);
        _testFilePath = Path.Combine(_testDir, "rules.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private RuleStore CreateStore()
    {
        // Simulate store creation by loading from test file path
        // Since RuleStore uses AppContext.BaseDirectory, we'll test the logic
        return new RuleStore();
    }

    [Fact]
    public void RuleStore_ShouldInitialize()
    {
        // Arrange & Act
        var store = new RuleStore();
        var rules = store.GetAll();

        // Assert
        Assert.NotNull(rules);
        Assert.IsType<List<Rule>>(rules);
    }

    [Fact]
    public void RuleStore_ShouldUpsertRule()
    {
        // Arrange
        var store = new RuleStore();
        var rule = new WeightTierRule
        {
            Id = "test-weight",
            Type = "WeightTier",
            Name = "Test Weight",
            Tiers = new() { new(0, 100, 10) }
        };

        // Act
        store.Upsert(rule);
        var retrieved = store.GetById("test-weight");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("test-weight", retrieved.Id);
        Assert.Equal("Test Weight", retrieved.Name);
    }

    [Fact]
    public void RuleStore_ShouldUpdateExistingRule()
    {
        // Arrange
        var store = new RuleStore();
        var rule = new WeightTierRule
        {
            Id = "update-test",
            Type = "WeightTier",
            Name = "Original Name",
            Tiers = new() { new(0, 50, 10) }
        };
        store.Upsert(rule);

        // Act
        var updated = new WeightTierRule
        {
            Id = "update-test",
            Type = "WeightTier",
            Name = "Updated Name",
            Tiers = new() { new(0, 100, 15) }
        };
        store.Upsert(updated);
        var retrieved = store.GetById("update-test");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Updated Name", retrieved.Name);
    }

    [Fact]
    public void RuleStore_ShouldRemoveRule()
    {
        // Arrange
        var store = new RuleStore();
        var rule = new WeightTierRule
        {
            Id = "remove-test",
            Type = "WeightTier",
            Name = "To Remove",
            Tiers = new()
        };
        store.Upsert(rule);

        // Act
        store.Remove("remove-test");
        var retrieved = store.GetById("remove-test");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void RuleStore_ShouldReturnAllRules()
    {
        // Arrange
        var store = new RuleStore();

        // Clear existing rules
        foreach (var rule in store.GetAll())
            store.Remove(rule.Id);

        var rule1 = new WeightTierRule
        {
            Id = "all-1",
            Type = "WeightTier",
            Name = "Rule 1",
            Tiers = new()
        };
        var rule2 = new TimeWindowPromotionRule
        {
            Id = "all-2",
            Type = "TimeWindowPromotion",
            Name = "Rule 2",
            StartTime = "10:00",
            EndTime = "14:00",
            DiscountPercent = 10
        };

        // Act
        store.Upsert(rule1);
        store.Upsert(rule2);
        var all = store.GetAll();

        // Assert
        Assert.NotNull(all);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void RuleStore_ShouldHandleMultipleRules()
    {
        // Arrange
        var store = new RuleStore();

        // Clear existing rules
        foreach (var rule in store.GetAll())
            store.Remove(rule.Id);

        var rules = new Rule[]
        {
            new WeightTierRule
            {
                Id = "w1",
                Type = "WeightTier",
                Name = "Weight 1",
                Tiers = new() { new(0, 50, 10) }
            },
            new TimeWindowPromotionRule
            {
                Id = "t1",
                Type = "TimeWindowPromotion",
                Name = "Time 1",
                StartTime = "09:00",
                EndTime = "12:00",
                DiscountPercent = 15
            },
            new RemoteAreaSurchargeRule
            {
                Id = "r1",
                Type = "RemoteAreaSurcharge",
                Name = "Remote 1",
                RemoteZipPrefixes = new() { "95", "96" },
                SurchargeFlat = 50
            }
        };

        // Act
        foreach (var rule in rules)
            store.Upsert(rule);

        var all = store.GetAll();

        // Assert
        Assert.Equal(3, all.Count);
        Assert.Contains(all, r => r.Id == "w1");
        Assert.Contains(all, r => r.Id == "t1");
        Assert.Contains(all, r => r.Id == "r1");
    }

    [Fact]
    public void RuleStore_GetByIdNonExistent_ShouldReturnNull()
    {
        // Arrange
        var store = new RuleStore();

        // Act
        var result = store.GetById("non-existent");

        // Assert
        Assert.Null(result);
    }
}
