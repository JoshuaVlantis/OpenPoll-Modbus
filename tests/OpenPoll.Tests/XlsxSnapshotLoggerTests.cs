using System;
using System.IO;
using System.Threading.Tasks;
using ClosedXML.Excel;
using FluentAssertions;
using OpenPoll.Models;
using OpenPoll.Services;

namespace OpenPoll.Tests;

/// <summary>
/// Verify the .xlsx logger produces a real Excel workbook with the expected header row and
/// at least one data row.
/// </summary>
public sealed class XlsxSnapshotLoggerTests
{
    [Fact]
    public async Task ProducesWorkbookWithHeaderAndRows()
    {
        var doc = new PollDocument(new PollDefinition { Name = "test", Amount = 2 });
        doc.EnsureRowSlots();
        doc.Rows[0].Value = "55";
        doc.Rows[1].Value = "66";

        var tmp = Path.Combine(Path.GetTempPath(), $"opentest-{Guid.NewGuid():N}.xlsx");
        try
        {
            var logger = new XlsxSnapshotLogger(doc, intervalMs: 60, tmp);
            logger.Start();
            await Task.Delay(220);
            await logger.StopAsync();

            using var wb = new XLWorkbook(tmp);
            var ws = wb.Worksheet(1);
            ws.Cell(1, 1).GetString().Should().Be("timestamp_iso");
            ws.Cell(1, 2).GetString().Should().StartWith("addr_");
            ws.Cell(2, 2).GetString().Should().Be("55");
            ws.Cell(2, 3).GetString().Should().Be("66");
        }
        finally { try { File.Delete(tmp); } catch { } }
    }
}
