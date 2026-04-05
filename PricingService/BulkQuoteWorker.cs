using System.Threading.Channels;
using Shared;

namespace PricingService;

/// <summary>
/// Background worker that processes bulk quote jobs asynchronously
/// Reads job IDs from a Channel, fetches rules, and calculates quotes
/// </summary>
public sealed class BulkQuoteWorker(
    Channel<string> channel,
    JobStore jobs,
    RuleServiceClient ruleClient,
    ILogger<BulkQuoteWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var jobId in channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                var job = jobs.GetById(jobId);
                if (job is null) continue;

                job.Status = "processing";
                jobs.Flush();

                var rules = await ruleClient.GetRulesAsync();
                var pending = jobs.TakePending(jobId);

                foreach (var item in pending)
                    job.Results.Add(PricingEngine.Calculate(item, rules));

                job.Status = "completed";
                job.CompletedAt = DateTime.UtcNow;
                jobs.Flush();

                logger.LogInformation("Job {JobId} completed — {Count} quotes", jobId, job.Results.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Job {JobId} failed", jobId);
                if (jobs.GetById(jobId) is { } failedJob)
                {
                    failedJob.Status = "failed";
                    jobs.Flush();
                }
            }
        }
    }
}
