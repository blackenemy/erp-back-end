using System.Collections.Concurrent;
using System.Text.Json;
using Shared;

namespace RuleService;

/// <summary>
/// In-memory rule store with file-based persistence
/// Provides thread-safe CRUD operations for pricing rules
/// </summary>
public sealed class RuleStore
{
    private static readonly string FilePath =
        Path.Combine(AppContext.BaseDirectory, "rules.json");

    private readonly ConcurrentDictionary<string, Rule> _rules;
    private readonly Lock _fileLock = new();

    public RuleStore()
    {
        if (File.Exists(FilePath))
        {
            var json = File.ReadAllText(FilePath);
            var list = JsonSerializer.Deserialize<List<Rule>>(json, AppJsonContext.Default.ListRule) ?? [];
            _rules = new(list.ToDictionary(r => r.Id));
        }
        else
        {
            _rules = new();
            Flush();
        }
    }

    public List<Rule> GetAll() => [.. _rules.Values];

    public Rule? GetById(string id) =>
        _rules.TryGetValue(id, out var rule) ? rule : null;

    public void Upsert(Rule rule)
    {
        _rules[rule.Id] = rule;
        Flush();
    }

    public void Remove(string id)
    {
        _rules.TryRemove(id, out _);
        Flush();
    }

    private void Flush()
    {
        lock (_fileLock)
        {
            var json = JsonSerializer.Serialize(
                [.. _rules.Values], AppJsonContext.Default.ListRule);
            File.WriteAllText(FilePath, json);
        }
    }
}
