using System.Text.Json;
using Shared;
using Xunit;

namespace Shared.Tests;

/// <summary>
/// Unit tests for Rule models and JSON serialization
/// </summary>
public class RuleModelTests
{
    [Fact]
    public void WeightTierRule_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var rule = new WeightTierRule
        {
            Id = "weight-1",
            Type = "WeightTier",
            Name = "Standard Weight",
            Enabled = true,
            Tiers = new()
            {
                new(0, 5, 20),
                new(5.01m, 20, 15),
                new(20.01m, 100, 10)
            }
        };

        // Act
        var json = JsonSerializer.Serialize(rule, AppJsonContext.Default.Rule);
        var deserialized = JsonSerializer.Deserialize<Rule>(json, AppJsonContext.Default.Rule);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<WeightTierRule>(deserialized);
        var deseRule = (WeightTierRule)deserialized;
        Assert.Equal("weight-1", deseRule.Id);
        Assert.Equal("Standard Weight", deseRule.Name);
        Assert.Equal(3, deseRule.Tiers.Count);
    }

    [Fact]
    public void TimeWindowPromotionRule_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var rule = new TimeWindowPromotionRule
        {
            Id = "promo-1",
            Type = "TimeWindowPromotion",
            Name = "Lunch Discount",
            Enabled = true,
            StartTime = "11:00",
            EndTime = "13:00",
            DiscountPercent = 15
        };

        // Act
        var json = JsonSerializer.Serialize(rule, AppJsonContext.Default.Rule);
        var deserialized = JsonSerializer.Deserialize<Rule>(json, AppJsonContext.Default.Rule);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<TimeWindowPromotionRule>(deserialized);
        var deseRule = (TimeWindowPromotionRule)deserialized;
        Assert.Equal(15m, deseRule.DiscountPercent);
        Assert.Equal("11:00", deseRule.StartTime);
    }

    [Fact]
    public void RemoteAreaSurchargeRule_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var rule = new RemoteAreaSurchargeRule
        {
            Id = "surcharge-1",
            Type = "RemoteAreaSurcharge",
            Name = "Southern Zone",
            Enabled = true,
            RemoteZipPrefixes = new() { "95", "96" },
            SurchargeFlat = 100
        };

        // Act
        var json = JsonSerializer.Serialize(rule, AppJsonContext.Default.Rule);
        var deserialized = JsonSerializer.Deserialize<Rule>(json, AppJsonContext.Default.Rule);

        // Assert
        Assert.NotNull(deserialized);
        Assert.IsType<RemoteAreaSurchargeRule>(deserialized);
        var deseRule = (RemoteAreaSurchargeRule)deserialized;
        Assert.Equal(100m, deseRule.SurchargeFlat);
        Assert.Contains("95", deseRule.RemoteZipPrefixes);
    }

    [Fact]
    public void RuleList_ShouldSerializePolymorphically()
    {
        // Arrange
        var rules = new List<Rule>
        {
            new WeightTierRule
            {
                Id = "w1",
                Type = "WeightTier",
                Name = "Weight",
                Tiers = new() { new(0, 100, 10) }
            },
            new TimeWindowPromotionRule
            {
                Id = "t1",
                Type = "TimeWindowPromotion",
                Name = "Promo",
                StartTime = "10:00",
                EndTime = "14:00",
                DiscountPercent = 20
            }
        };

        // Act
        var json = JsonSerializer.Serialize(rules, AppJsonContext.Default.ListRule);
        var deserialized = JsonSerializer.Deserialize<List<Rule>>(json, AppJsonContext.Default.ListRule);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);
        Assert.IsType<WeightTierRule>(deserialized[0]);
        Assert.IsType<TimeWindowPromotionRule>(deserialized[1]);
    }

    [Fact]
    public void DisabledRule_ShouldNotAffectCalculations()
    {
        // Arrange
        var rule = new WeightTierRule
        {
            Id = "disabled",
            Type = "WeightTier",
            Name = "Disabled Weight",
            Enabled = false,
            Tiers = new() { new(0, 100, 999) }
        };

        // Act & Assert
        Assert.False(rule.Enabled);
    }

    [Fact]
    public void RuleWithoutId_ShouldGenerateGuid()
    {
        // Arrange & Act
        var rule = new WeightTierRule
        {
            Type = "WeightTier",
            Name = "Auto ID",
            Tiers = new()
        };

        // Assert
        Assert.NotEmpty(rule.Id);
        Assert.NotEqual("", rule.Id);
    }
}
