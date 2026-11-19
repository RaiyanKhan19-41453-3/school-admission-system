using System.Globalization;
using System.Text;

namespace Admissions.Services;

/// <summary>
/// Minimal PDF 1.4 writer — single page, Helvetica text.
/// No third-party library: zero licensing and zero dependency-vulnerability risk.
/// Supports ASCII text with simple line positioning, enough for a receipt/letter.
/// </summary>
public static class SimplePdfWriter
{
    static string Escape(string s) => s
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal)
        .Replace(")", "\\)", StringComparison.Ordinal);

    public static byte[] Build(string title, IReadOnlyList<string> lines)
    {
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 20 Tf");
        content.AppendLine("1 0 0 1 56 790 Tm");
        content.AppendLine($"({Escape(title)}) Tj");
        content.AppendLine("/F1 11 Tf");
        int y = 760;
        foreach (var line in lines)
        {
            content.AppendLine($"1 0 0 1 56 {y} Tm");
            content.AppendLine($"({Escape(line)}) Tj");
            y -= 22;
            if (y < 60) break;   // stay on the page
        }
        content.AppendLine("ET");
        var stream = content.ToString();
        var streamBytes = Encoding.ASCII.GetBytes(stream);

        var objects = new List<byte[]>();
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"));
        objects.Add(Encoding.ASCII.GetBytes(
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] " +
            "/Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>"));
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));

        var streamObj = new List<byte>();
        streamObj.AddRange(Encoding.ASCII.GetBytes($"<< /Length {streamBytes.Length} >>\nstream\n"));
        streamObj.AddRange(streamBytes);
        streamObj.AddRange(Encoding.ASCII.GetBytes("\nendstream"));
        objects.Add(streamObj.ToArray());

        using var pdf = new MemoryStream();
        void Write(string s) { var b = Encoding.ASCII.GetBytes(s); pdf.Write(b, 0, b.Length); }
        Write("%PDF-1.4\n");

        var offsets = new long[objects.Count + 1];
        for (int i = 0; i < objects.Count; i++)
        {
            offsets[i + 1] = pdf.Position;
            Write($"{i + 1} 0 obj\n");
            pdf.Write(objects[i], 0, objects[i].Length);
            Write("\nendobj\n");
        }

        var xref = pdf.Position;
        Write($"xref\n0 {objects.Count + 1}\n");
        Write("0000000000 65535 f \n");
        for (int i = 1; i <= objects.Count; i++)
            Write($"{offsets[i].ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");

        return pdf.ToArray();
    }
}
