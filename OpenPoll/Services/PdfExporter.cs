using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace OpenPoll.Services;

/// <summary>
/// Render a snapshot of a poll's row table to a multi-page PDF. Avalonia 11 has no native
/// cross-platform print pipeline; producing a PDF and letting the user print it via their OS
/// is the portable workaround. Pages are A4 portrait with a simple monospace table layout.
/// </summary>
public static class PdfExporter
{
    private const float PageWidthPts  = 595;   // A4 portrait, 72 dpi pts
    private const float PageHeightPts = 842;
    private const float MarginPts     = 40;

    public static void ExportRowsToPdf(string path, string title, IReadOnlyList<(string Address, string Value, string Type)> rows)
    {
        using var stream = File.Create(path);
        using var document = SKDocument.CreatePdf(stream);

        using var titlePaint = MakePaint(SKTypeface.FromFamilyName("Sans", SKFontStyle.Bold), 16);
        using var headerPaint = MakePaint(SKTypeface.FromFamilyName("Sans", SKFontStyle.Bold), 11);
        using var bodyPaint = MakePaint(SKTypeface.FromFamilyName("monospace"), 10);
        using var rulePaint = new SKPaint
        {
            Color = new SKColor(0xCC, 0xCC, 0xCC),
            StrokeWidth = 0.5f,
            IsStroke = true,
        };

        float rowHeight = 16f;
        int rowsPerPage = (int)((PageHeightPts - MarginPts * 2 - 80) / rowHeight);
        int pages = Math.Max(1, (rows.Count + rowsPerPage - 1) / rowsPerPage);

        for (int p = 0; p < pages; p++)
        {
            using var canvas = document.BeginPage(PageWidthPts, PageHeightPts);
            float y = MarginPts;

            canvas.DrawText(title, MarginPts, y + 16, titlePaint);
            y += 24;
            canvas.DrawText($"Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}   ·   Page {p + 1} of {pages}",
                            MarginPts, y + 12, bodyPaint);
            y += 24;

            canvas.DrawText("ADDR",   MarginPts,        y + 12, headerPaint);
            canvas.DrawText("VALUE",  MarginPts + 120,  y + 12, headerPaint);
            canvas.DrawText("TYPE",   MarginPts + 320,  y + 12, headerPaint);
            y += 16;
            canvas.DrawLine(MarginPts, y, PageWidthPts - MarginPts, y, rulePaint);
            y += 4;

            int start = p * rowsPerPage;
            int end = Math.Min(rows.Count, start + rowsPerPage);
            for (int i = start; i < end; i++)
            {
                var r = rows[i];
                canvas.DrawText(r.Address, MarginPts,        y + 11, bodyPaint);
                canvas.DrawText(r.Value,   MarginPts + 120,  y + 11, bodyPaint);
                canvas.DrawText(r.Type,    MarginPts + 320,  y + 11, bodyPaint);
                y += rowHeight;
            }

            document.EndPage();
        }

        document.Close();
    }

    private static SKPaint MakePaint(SKTypeface? typeface, float textSize) => new()
    {
        Color = SKColors.Black,
        IsAntialias = true,
#pragma warning disable CS0618 // Type or member is obsolete — Typeface/TextSize on SKPaint are obsolete in newer SkiaSharp but the version transitively pulled by LiveCharts2 2.0.2 still needs them.
        Typeface = typeface,
        TextSize = textSize,
#pragma warning restore CS0618
    };
}
