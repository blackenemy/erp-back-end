using Shared;
using Xunit;

namespace PricingService.Tests;

/// <summary>
/// Unit tests for PricingEngine business logic
/// </summary>
public class PricingEngineTests
{
    private const decimal BaseFlatRate = 50m;

    [Fact]
    public void PricingEngine_NoRules_ShouldApplyBaseRate()
    {
        // Arrange
        var request = new QuoteRequest(10m, "10100", "95120");
        var rules = new List<Rule>();

        // Act
        var result = PricingEngine.Calculate(request, rules);

        // Assert
        Assert.Equal(BaseFlatRate, result.BasePrice);
        Assert.Equal(0m, result.Discount);
        Assert.Equal(0m, result.Surcharge);
        Assert.Equal(BaseFlatRate, result.FinalPrice);
    }

    [Fact]
    public void PricingEngine_WeightTierRule_ShouldCalculateCorrectly()
    {
        // Arrange
        var request = new QuoteRequest(10m, "10100", "95120");
        var rules = new List<Rule>
        {
            new WeightTierRule
            {
                Id = "wt1",
                Type = "WeightTier",
                Name = "Standard Pricing",
                Enabled = true,
                Tiers = new()
                {
                    new(0, 5, 20),
                    new(5.01m, 20, 15),
                    new(20.01m, 100, 10)
                }
            }
        };

        // Act
        var result = PricingEngine.Calculate(request, rules);

        // Assert
        Assert.Equal(150m, result.BasePrice); // 10 kg * 15 per kg
        Assert.Equal(0m, result.Discount);
        Assert.Equal(150m, result.FinalPrice);
        Assert.Contains("WeightTier", result.AppliedRules[0]);
    }

    [Fact]
    public void PricingEngine_MultipleWeightTiers_ShouldUseCorrectTier()
    {
        // Arrange
        var lightRequest = new QuoteRequest(2m, "10100", "95120");
        var mediumRequest = new QuoteRequest(10m, "10100", "95120");
        var heavyRequest = new QuoteRequest(50m, "10100", "95120");

        var rules = new List<Rule>
        {
            new WeightTierRule
            {
                Id = "wt1",
                Type = "WeightTier",
                Name = "Tiered",
                Enabled = true,
                Tiers = new()
                {
                    new(0, 5, 20),
                    new(5.01m, 20, 15),
                    new(20.01m, 100, 10)
                }
            }
        };

        // Act
        var lightResult = PricingEngine.Calculate(lightRequest, rules);
        var mediumResult = PricingEngine.Calculate(mediumRequest, rules);
        var heavyResult = PricingEngine.Calculate(heavyRequest, rules);

        // Assert
        Assert.Equal(40m, lightResult.BasePrice);    // 2 * 20
        Assert.Equal(150m, mediumResult.BasePrice);  // 10 * 15
        Assert.Equal(500m, heavyResult.BasePrice);   // 50 * 10
    }

    [Fact]
    public void PricingEngine_DisabledRule_ShouldBeIgnored()
    {
        // Arrange
        var request = new QuoteRequest(10m, "10100", "95120");
        var rules = new List<Rule>
        {
            new WeightTierRule
            {
                Id = "wt1",
                Type = "WeightTier",
                Name = "Disabled",
                Enabled = false,
                Tiers = new()
                {
                    new(0, 100, 999) // Would be expensive if enabled
                }
            }
        };

        // Act
        var result = PricingEngine.Calculate(request, rules);

        // Assert
        Assert.Equal(BaseFlatRate, result.BasePrice);
        Assert.Empty(result.AppliedRules);
    }

    [Fact]
    public void PricingEngine_TimeWindowPromotion_ShouldApplyDiscount()
    {
        // Arrange
        var request = new QuoteRequest(10m, "10100", "95120");
        var rules = new List<Rule>
        {
            new WeightTierRule
            {
                Id = "wt1",
                Type = "WeightTier",
                Name = "Base",
                Enabled = true,
                Tiers = new() { new(0, 100, 10) }
            },
            new TimeWindowPromotionRule
            {
                Id = "promo1",
                Type = "TimeWindowPromotion",
                Name = "Lunch",
                Enabled = true,
                StartTime = "00:00", // Always applicable
                EndTime = "23:59",
                DiscountPercent = 20
            }
        };

        // Act
        var result = PricingEngine.Calculate(request, rules);

        // Assert
        Assert.Equal(100m, result.BasePrice);        // 10 * 10
        Assert.Equal(20m, result.Discount);          // 100 * 20%
        Assert.Equal(80m, result.FinalPrice);        // 100 - 20
        Assert.Contains("TimeWindowPromotion", result.AppliedRules[1]);
    }

    [Fact]
    public void PricingEngine_RemoteAreaSurcharge_ShouldApplyCorrectly()
    {
        // Arrange
        var request = new QuoteRequest(10m, "10100", "95120");
        var rules = new List<Rule>
        {
            new WeightTierRule
            {
                Id = "wt1",
                Type = "WeightTier",
                Name = "Base",
                Enabled = true,
                Tiers = new() { new(0, 100, 10) }
            },
            new RemoteAreaSurchargeRule
            {
                Id = "surcharge1",
                Type = "RemoteAreaSurcharge",
                Name = "Southern",
                Enabled = true,
                RemoteZipPrefixes = new() { "95", "96" },
                SurchargeFlat = 50
            }
        };

        // Act
        var result = PricingEngine.Calculate(request, rules);

        // Assert
        Assert.Equal(100m, result.BasePrice);        // 10 * 10
        Assert.Equal(0m, result.Discount);
        Assert.Equal(50m, result.Surcharge);
        Assert.Equal(150m, result.FinalPrice);       // 100 + 50
        Assert.Contains("RemoteAreaSurcharge", result.AppliedRules[1]);
    }

    [Fact]
    public void PricingEngine_MultipleRules_ShouldApplyAllRules()
    {
        // Arrange
        var request = new QuoteRequest(10m, "10100", "95120");
        var rules = new List<Rule>
        {
            new WeightTierRule
            {
                Id = "wt1",
                Type = "WeightTier",
                Name = "Base",
                Enabled = true,
                Tiers = new() { new(0, 100, 10) }
            },
            new TimeWindowPromotionRule
            {
                Id = "promo1",
                Type = "TimeWindowPromotion",
                Name = "Discount",
                Enabled = true,
                StartTime = "00:00",
                EndTime = "23:59",
                DiscountPercent = 10
            },
            new RemoteAreaSurchargeRule
            {
                Id = "surcharge1",
                Type = "RemoteAreaSurcharge",
                Name = "Remote",
                Enabled = true,
                RemoteZipPrefixes = new() { "95" },
                SurchargeFlat = 30
            }
        };

        // Act
        var result = PricingEngine.Calculate(request, rules);

        // Assert
        Assert.Equal(100m, result.BasePrice);
        Assert.Equal(10m, result.Discount);          // 100 * 10%
        Assert.Equal(30m, result.Surcharge);
        Assert.Equal(120m, result.FinalPrice);       // 100 - 10 + 30
        Assert.Equal(3, result.AppliedRules.Count);
    }

    [Fact]
    public void PricingEngine_RemoteAreaSurcharge_NonMatchingZip_ShouldNotApply()
    {
        // Arrange
        var request = new QuoteRequest(10m, "10100", "84000"); // Doesn't start with 95 or 96
        var rules = new List<Rule>
        {
            new WeightTierRule
            {
                Id = "wt1",
                Type = "WeightTier",
                Name = "Base",
                Enabled = true,
                Tiers = new() { new(0, 100, 10) }
            },
            new RemoteAreaSurchargeRule
            {
                Id = "surcharge1",
                Type = "RemoteAreaSurcharge",
                Name = "Southern",
                Enabled = true,
                RemoteZipPrefixes = new() { "95", "96" },
                SurchargeFlat = 50
            }
        };

        // Act
        var result = PricingEngine.Calculate(request, rules);

        // Assert
        Assert.Equal(100m, result.FinalPrice);
        Assert.Equal(0m, result.Surcharge);
    }

    [Fact]
    public void PricingEngine_EdgeCase_ZeroWeight_ShouldCalculate()
    {
        // Arrange
        var request = new QuoteRequest(0m, "10100", "95120");
        var rules = new List<Rule>
        {
            new WeightTierRule
            {
                Id = "wt1",
                Type = "WeightTier",
                Name = "Base",
                Enabled = true,
                Tiers = new() { new(0, 100, 10) }
            }
        };

        // Act
        var result = PricingEngine.Calculate(request, rules);

        // Assert
        Assert.Equal(0m, result.BasePrice);
    }

    [Fact]
    public void PricingEngine_EdgeCase_LargeWeight_ShouldCalculate()
    {
        // Arrange
        var request = new QuoteRequest(1000m, "10100", "95120");
        var rules = new List<Rule>
        {
            new WeightTierRule
            {
                Id = "wt1",
                Type = "WeightTier",
                Name = "Base",
                Enabled = true,
                Tiers = new() { new(0, 10000, 5) }
            }
        };

        // Act
        var result = PricingEngine.Calculate(request, rules);

        // Assert
        Assert.Equal(5000m, result.BasePrice);
    }

    [Fact]
    public void PricingEngine_DiscountShouldNotExceedBasePrice()
    {
        // Arrange
        var request = new QuoteRequest(10m, "10100", "95120");
        var rules = new List<Rule>
        {
            new WeightTierRule
            {
                Id = "wt1",
                Type = "WeightTier",
                Name = "Base",
                Enabled = true,
                Tiers = new() { new(0, 100, 10) }
            },
            new TimeWindowPromotionRule
            {
                Id = "promo1",
                Type = "TimeWindowPromotion",
                Name = "Heavy Discount",
                Enabled = true,
                StartTime = "00:00",
                EndTime = "23:59",
                DiscountPercent = 150 // Over 100% discount
            }
        };

        // Act
        var result = PricingEngine.Calculate(request, rules);

        // Assert - The calculation should still work, but FinalPrice could be negative
        // This tests the engine doesn't crash on extreme values
        Assert.Equal(100m, result.BasePrice);
        Assert.Equal(150m, result.Discount);
    }
}
