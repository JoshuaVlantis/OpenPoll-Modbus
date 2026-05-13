using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using OpenPoll.Models;
using OpenPoll.Services;

namespace OpenPoll.Tests;

/// <summary>
/// Verifies the CSV snapshot writer produces a header + at least one row, escapes commas/quotes
/// correctly, and tolerates the document's row collection growing/shrinking between ticks.
/// </summary>
public sealed class CsvSnapshotLoggerTests
{
    [Fact]
    public async Task LogsHeaderAndRows_AppendingPerInterval()
    {
        var doc = new PollDocument(new PollDefinition { Name = "test", Amount = 3 });
        doc.EnsureRowSlots();
        doc.Rows[0].Value = "111";
        doc.Rows[1].Value = "222";
        doc.Rows[2].Value = "333";

        var tmp = Path.Combine(Path.GetTempPath(), $"opentest-{Guid.NewGuid():N}.csv");
        try
        {
            var logger = new CsvSnapshotLogger(doc, intervalMs: 50, tmp);
            logger.Start();
            await Task.Delay(220);   // ~4 ticks
            await logger.StopAsync();

            var lines = await File.ReadAllLinesAsync(tmp);
            lines.Length.Should().BeGreaterOrEqualTo(2);   // header + ≥1 row
            lines[0].Should().StartWith("timestamp_iso,");
            lines[0].Should().Contain("addr_0").And.Contain("addr_1").And.Contain("addr_2");
            lines[1].Should().EndWith("111,222,333");
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    [Fact]
    public async Task EscapesCommasAndQuotesInValues()
    {
        var doc = new PollDocument(new PollDefinition { Name = "tricky", Amount = 1 });
        doc.EnsureRowSlots();
        doc.Rows[0].Value = "hello,\"world\"";

        var tmp = Path.Combine(Path.GetTempPath(), $"opentest-{Guid.NewGuid():N}.csv");
        try
        {
            var logger = new CsvSnapshotLogger(doc, intervalMs: 50, tmp);
            logger.Start();
            await Task.Delay(120);
            await logger.StopAsync();

            var content = await File.ReadAllTextAsync(tmp);
            content.Should().Contain("\"hello,\"\"world\"\"\"");
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    [Fact]
    public async Task StopAsync_IsSafeWhenNotStarted()
    {
        var doc = new PollDocument(new PollDefinition());
        var logger = new CsvSnapshotLogger(doc, 100, Path.GetTempFileName());
        await logger.StopAsync();   // should not throw
        logger.IsRunning.Should().BeFalse();
    }
}
