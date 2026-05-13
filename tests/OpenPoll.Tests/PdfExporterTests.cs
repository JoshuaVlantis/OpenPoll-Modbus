using System;
using System.IO;
using FluentAssertions;
using OpenPoll.Services;

namespace OpenPoll.Tests;

/// <summary>
/// Smoke-test the PDF exporter: produces a file with a PDF signature, contains at least one page,
/// and tolerates more rows than fit on one page (multi-page path).
/// </summary>
public sealed class PdfExporterTests
{
    [Fact]
    public void ExportRowsToPdf_ProducesValidPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opentest-{Guid.NewGuid():N}.pdf");
        try
        {
            PdfExporter.ExportRowsToPdf(path, "Test", new[]
            {
                ("0", "111", "Unsigned"),
                ("1", "222", "Unsigned"),
                ("2", "333", "Hex"),
            });
            var bytes = File.ReadAllBytes(path);
            bytes.Length.Should().BeGreaterThan(100);
            // PDF files start with the signature "%PDF-".
            System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public void ExportRowsToPdf_TolerablesManyRows_MultiPage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opentest-{Guid.NewGuid():N}.pdf");
        try
        {
            var many = new (string, string, string)[200];
            for (int i = 0; i < many.Length; i++) many[i] = (i.ToString(), $"v{i}", "Unsigned");
            PdfExporter.ExportRowsToPdf(path, "Many rows", many);
            new FileInfo(path).Length.Should().BeGreaterThan(2000);
        }
        finally { try { File.Delete(path); } catch { } }
    }
}
