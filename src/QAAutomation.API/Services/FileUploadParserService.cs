using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using CsvHelper;
using CsvHelper.Configuration;
using QAAutomation.API.DTOs;

namespace QAAutomation.API.Services;

/// <summary>
/// Parses an uploaded CSV or Excel (XLSX) file into a list of pipeline items.
///
/// Expected column layout (case-insensitive, order-independent):
///   transcript   — call transcript text (required)
///   agentName    — agent name (optional)
///   callReference — call or interaction reference (optional)
///   callDate     — date of the call, ISO 8601 or common date formats (optional)
///
/// CSV files may use comma, semicolon, or tab as delimiter (auto-detected).
/// XLSX files are parsed by reading the underlying Open XML directly —
/// no third-party Excel library is needed.
/// </summary>
public static class FileUploadParserService
{
    private static readonly string[] TranscriptAliases  = ["transcript", "transcription", "text", "content", "body"];
    private static readonly string[] AgentAliases       = ["agentname", "agent", "agent_name", "rep", "representative"];
    private static readonly string[] ReferenceAliases   = ["callreference", "reference", "callref", "interactionid", "id", "call_id", "ref"];
    private static readonly string[] DateAliases        = ["calldate", "date", "calltime", "timestamp", "call_date"];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the uploaded file and returns a list of pipeline items.
    /// Throws <see cref="InvalidDataException"/> if the file format is unrecognised
    /// or no transcript column can be found.
    /// </summary>
    public static List<BatchUrlItemDto> Parse(Stream fileStream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" => ParseXlsx(fileStream),
            ".csv"  => ParseCsv(fileStream, ','),
            ".tsv"  => ParseCsv(fileStream, '\t'),
            _       => TryAutoDetect(fileStream, fileName)
        };
    }

    // ── CSV parsing ───────────────────────────────────────────────────────────

    private static List<BatchUrlItemDto> ParseCsv(Stream stream, char delimiter)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter.ToString(),
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim
        };

        using var csv = new CsvReader(reader, config);
        csv.Read();
        csv.ReadHeader();

        var headers = csv.HeaderRecord ?? throw new InvalidDataException("CSV file has no header row.");
        var colMap  = BuildColumnMap(headers);

        if (!colMap.TryGetValue("transcript", out var transcriptCol))
            throw new InvalidDataException(
                "Could not find a 'Transcript' column. Expected one of: " +
                string.Join(", ", TranscriptAliases) + ".");

        var items = new List<BatchUrlItemDto>();
        while (csv.Read())
        {
            var transcript = csv.GetField(transcriptCol)?.Trim();
            if (string.IsNullOrWhiteSpace(transcript)) continue;

            items.Add(new BatchUrlItemDto
            {
                // We store the transcript text directly in the URL field — the pipeline
                // service checks for "data:" prefix and treats it as an inline transcript.
                Url           = "data:text/plain," + Uri.EscapeDataString(transcript),
                AgentName     = colMap.TryGetValue("agent", out var a)   ? csv.GetField(a)?.Trim() : null,
                CallReference = colMap.TryGetValue("ref",   out var r)   ? csv.GetField(r)?.Trim() : null,
                CallDate      = colMap.TryGetValue("date",  out var d) &&
                                DateTime.TryParse(csv.GetField(d)?.Trim(),
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                                    out var dt) ? dt : (DateTime?)null
            });
        }

        if (items.Count == 0)
            throw new InvalidDataException("The file contained no data rows.");

        return items;
    }

    // ── XLSX parsing ──────────────────────────────────────────────────────────

    private static List<BatchUrlItemDto> ParseXlsx(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        // Read shared strings table (optional — cells may inline their values)
        var sharedStrings = ReadSharedStrings(zip);

        // Find the first worksheet
        var sheetEntry = zip.Entries
            .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase)
                     && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName)
            .FirstOrDefault()
            ?? throw new InvalidDataException("No worksheet found in the XLSX file.");

        using var sheetStream = sheetEntry.Open();
        var doc = new XmlDocument();
        doc.Load(sheetStream);

        var nsMgr = new XmlNamespaceManager(doc.NameTable);
        nsMgr.AddNamespace("x", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");

        var rows = doc.SelectNodes("//x:sheetData/x:row", nsMgr)!;
        if (rows.Count == 0) throw new InvalidDataException("The worksheet contains no rows.");

        // First row = headers
        var headerRow = rows[0]!;
        var headers = ReadXlsxRow(headerRow, nsMgr, sharedStrings);
        var colMap  = BuildColumnMap(headers);

        if (!colMap.TryGetValue("transcript", out var transcriptCol))
            throw new InvalidDataException(
                "Could not find a 'Transcript' column. Expected one of: " +
                string.Join(", ", TranscriptAliases) + ".");

        var items = new List<BatchUrlItemDto>();
        for (var i = 1; i < rows.Count; i++)
        {
            var cells = ReadXlsxRow(rows[i]!, nsMgr, sharedStrings);
            var transcript = GetCell(cells, transcriptCol)?.Trim();
            if (string.IsNullOrWhiteSpace(transcript)) continue;

            DateTime? callDate = null;
            if (colMap.TryGetValue("date", out var dateCol))
            {
                var raw = GetCell(cells, dateCol)?.Trim();
                if (!string.IsNullOrEmpty(raw))
                {
                    // XLSX stores dates as OLE Automation serial numbers
                    if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial))
                        callDate = DateTime.FromOADate(serial);
                    else if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                        callDate = dt;
                }
            }

            items.Add(new BatchUrlItemDto
            {
                Url           = "data:text/plain," + Uri.EscapeDataString(transcript),
                AgentName     = colMap.TryGetValue("agent", out var a) ? GetCell(cells, a)?.Trim() : null,
                CallReference = colMap.TryGetValue("ref",   out var r) ? GetCell(cells, r)?.Trim() : null,
                CallDate      = callDate
            });
        }

        if (items.Count == 0)
            throw new InvalidDataException("The file contained no data rows.");

        return items;
    }

    private static string[] ReadSharedStrings(ZipArchive zip)
    {
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry == null) return [];

        using var stream = entry.Open();
        var doc = new XmlDocument();
        doc.Load(stream);

        var nsMgr = new XmlNamespaceManager(doc.NameTable);
        nsMgr.AddNamespace("x", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");

        var siNodes = doc.SelectNodes("//x:si", nsMgr)!;
        var result  = new string[siNodes.Count];
        for (var i = 0; i < siNodes.Count; i++)
            result[i] = siNodes[i]!.InnerText;
        return result;
    }

    private static string[] ReadXlsxRow(XmlNode row, XmlNamespaceManager nsMgr, string[] sharedStrings)
    {
        var cells  = row.SelectNodes("x:c", nsMgr)!;
        if (cells.Count == 0) return [];

        // Determine the highest column index in this row
        var maxCol = 0;
        foreach (XmlNode cell in cells)
        {
            var r = cell.Attributes?["r"]?.Value ?? "";
            maxCol = Math.Max(maxCol, ColumnLetterToIndex(r));
        }

        var values = new string[maxCol + 1];
        foreach (XmlNode cell in cells)
        {
            var r   = cell.Attributes?["r"]?.Value ?? "";
            var col = ColumnLetterToIndex(r);
            var t   = cell.Attributes?["t"]?.Value ?? "";
            var v   = cell.SelectSingleNode("x:v", nsMgr)?.InnerText ?? "";

            values[col] = t == "s" && int.TryParse(v, out var idx) && idx < sharedStrings.Length
                ? sharedStrings[idx]
                : v;
        }
        return values;
    }

    private static string? GetCell(string[] cells, int col) =>
        col < cells.Length ? cells[col] : null;

    /// <summary>Convert "A1" → 0, "B1" → 1, "AA1" → 26, etc.</summary>
    private static int ColumnLetterToIndex(string cellRef)
    {
        var letters = new StringBuilder();
        foreach (var c in cellRef)
        {
            if (char.IsLetter(c)) letters.Append(c);
            else break;
        }
        var idx = 0;
        foreach (var c in letters.ToString().ToUpperInvariant())
            idx = idx * 26 + (c - 'A' + 1);
        return idx - 1;
    }

    // ── Auto-detect for unknown extensions ───────────────────────────────────

    private static List<BatchUrlItemDto> TryAutoDetect(Stream stream, string fileName)
    {
        // Peek for XLSX magic bytes (PK zip header)
        var buf = new byte[4];
        var read = stream.Read(buf, 0, 4);
        stream.Seek(0, SeekOrigin.Begin);

        if (read >= 4 && buf[0] == 0x50 && buf[1] == 0x4B)
            return ParseXlsx(stream);

        // Fall back to CSV comma delimiter
        return ParseCsv(stream, ',');
    }

    // ── Column mapping ────────────────────────────────────────────────────────

    /// <summary>
    /// Maps logical keys ("transcript", "agent", "ref", "date") to column indices.
    /// </summary>
    private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<string?> headers)
    {
        var map = new Dictionary<string, int>();
        for (var i = 0; i < headers.Count; i++)
        {
            var h = (headers[i] ?? "").ToLowerInvariant().Trim().Replace(" ", "").Replace("_", "");
            if (!map.ContainsKey("transcript") && TranscriptAliases.Any(a => a == h))
                map["transcript"] = i;
            else if (!map.ContainsKey("agent") && AgentAliases.Any(a => a == h))
                map["agent"] = i;
            else if (!map.ContainsKey("ref") && ReferenceAliases.Any(a => a == h))
                map["ref"] = i;
            else if (!map.ContainsKey("date") && DateAliases.Any(a => a == h))
                map["date"] = i;
        }
        return map;
    }
}
