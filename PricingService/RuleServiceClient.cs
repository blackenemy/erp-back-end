using System.Text.Json;
using Shared;

namespace PricingService;

/// <summary>
/// Typed HTTP client for communicating with RuleService
/// Fetches pricing rules for quote calculations
/// </summary>
public sealed class RuleServiceClient(HttpClient http)
{
    public async Task<List<Rule>> GetRulesAsync()
    {
        var stream = await http.GetStreamAsync("/rules");
        return await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.ListRule) ?? [];
    }
}
