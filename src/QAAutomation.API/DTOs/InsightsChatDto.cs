namespace QAAutomation.API.DTOs;

/// <summary>Request payload for the Insights Chat endpoint.</summary>
public class InsightsChatRequestDto
{
    /// <summary>The free-text question asked by the user.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>Project (tenant) scope. Only data for this project is queried.</summary>
    public int? ProjectId { get; set; }
}

/// <summary>Response payload for the Insights Chat endpoint.</summary>
public class InsightsChatResponseDto
{
    public string Question { get; set; } = string.Empty;

    /// <summary>The SELECT SQL that was generated (without the wrapping tenant CTEs).</summary>
    public string Sql { get; set; } = string.Empty;

    /// <summary>Column names from the result set.</summary>
    public List<string> Columns { get; set; } = new();

    /// <summary>Rows of data. Each row is a list of values (string | number | null).</summary>
    public List<List<object?>> Rows { get; set; } = new();

    /// <summary>AI-generated narrative insights about the result data.</summary>
    public string Insights { get; set; } = string.Empty;

    /// <summary>Total rows returned (may be capped at 500).</summary>
    public int RowCount { get; set; }

    /// <summary>Non-null when an error prevents a valid result.</summary>
    public string? Error { get; set; }
}
