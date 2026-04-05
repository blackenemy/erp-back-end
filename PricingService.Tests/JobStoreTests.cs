using System.Collections.Concurrent;
using System.Text.Json;
using Shared;
using Xunit;

namespace PricingService.Tests;

/// <summary>
/// Integration tests for PricingService endpoints and background job processing
/// </summary>
public class JobStoreTests : IDisposable
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), $"job-store-{Guid.NewGuid()}");
    private readonly string _testFilePath;

    public JobStoreTests()
    {
        Directory.CreateDirectory(_testDir);
        _testFilePath = Path.Combine(_testDir, "jobs.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void JobStore_ShouldCreateNewJob()
    {
        // Arrange
        var store = new JobStore();
        var job = new JobRecord();

        // Act
        store.Add(job);
        var retrieved = store.GetById(job.JobId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(job.JobId, retrieved.JobId);
        Assert.Equal("pending", retrieved.Status);
    }

    [Fact]
    public void JobStore_ShouldSetPendingItems()
    {
        // Arrange
        var store = new JobStore();
        var job = new JobRecord();
        var items = new List<QuoteRequest>
        {
            new(5m, "10100", "95120"),
            new(10m, "20100", "84000")
        };

        // Act
        store.Add(job);
        store.SetPending(job.JobId, items);
        var pending = store.TakePending(job.JobId);

        // Assert
        Assert.Equal(2, pending.Count);
        Assert.Equal(5m, pending[0].WeightKg);
        Assert.Equal(10m, pending[1].WeightKg);
    }

    [Fact]
    public void JobStore_TakePending_ShouldClearQueue()
    {
        // Arrange
        var store = new JobStore();
        var job = new JobRecord();
        var items = new List<QuoteRequest>
        {
            new(5m, "10100", "95120")
        };

        // Act
        store.Add(job);
        store.SetPending(job.JobId, items);
        var first = store.TakePending(job.JobId);
        var second = store.TakePending(job.JobId);

        // Assert
        Assert.Single(first);
        Assert.Empty(second);
    }

    [Fact]
    public void JobStore_ShouldPersistJobsToDisk()
    {
        // Arrange
        var store = new JobStore();
        var job = new JobRecord { Status = "completed" };
        job.Results.Add(new QuoteResult
        {
            BasePrice = 100m,
            Discount = 10m,
            Surcharge = 0m,
            FinalPrice = 90m
        });

        // Act
        store.Add(job);
        store.Flush();

        var store2 = new JobStore();
        var retrieved = store2.GetById(job.JobId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("completed", retrieved.Status);
        Assert.Single(retrieved.Results);
    }

    [Fact]
    public void JobStore_GetByIdNonExistent_ShouldReturnNull()
    {
        // Arrange
        var store = new JobStore();

        // Act
        var result = store.GetById("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void JobStore_ShouldHandleMultipleJobs()
    {
        // Arrange
        var store = new JobStore();
        var jobs = new[]
        {
            new JobRecord { Status = "pending" },
            new JobRecord { Status = "processing" },
            new JobRecord { Status = "completed" }
        };

        // Act
        foreach (var job in jobs)
            store.Add(job);

        // Assert
        Assert.NotNull(store.GetById(jobs[0].JobId));
        Assert.NotNull(store.GetById(jobs[1].JobId));
        Assert.NotNull(store.GetById(jobs[2].JobId));
    }
}
