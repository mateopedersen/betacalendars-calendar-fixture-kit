using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BetaCalendars.CalendarFixtureKit;

/// <summary>Kind of structural difference found between two fixtures.</summary>
public enum CalendarDifferenceKind
{
    /// <summary>A cell exists in the expected grid but not the actual grid.</summary>
    MissingCell,
    /// <summary>A cell exists in the actual grid but not the expected grid.</summary>
    UnexpectedCell,
    /// <summary>The date assigned to a coordinate changed.</summary>
    DateMismatch,
    /// <summary>The weekday assigned to a date changed.</summary>
    WeekdayMismatch,
    /// <summary>A date moved to another coordinate.</summary>
    CoordinateMismatch,
    /// <summary>A date's target or adjacent-month relation changed.</summary>
    MonthRelationMismatch,
    /// <summary>The row or cell count changed.</summary>
    GridSizeMismatch,
    /// <summary>A grid-level property changed.</summary>
    PropertyMismatch
}

/// <summary>A structured difference at a cell or grid level.</summary>
public sealed record CalendarDifference
{
    /// <summary>Creates a structured grid difference.</summary>
    public CalendarDifference(CalendarDifferenceKind kind, string message,
        GridCoordinate? coordinate = null, DateOnly? date = null,
        CalendarGridCell? expected = null, CalendarGridCell? actual = null)
    {
        Kind = kind;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Coordinate = coordinate;
        Date = date;
        Expected = expected;
        Actual = actual;
    }

    /// <summary>Gets the difference category.</summary>
    public CalendarDifferenceKind Kind { get; }
    /// <summary>Gets the human-readable explanation.</summary>
    public string Message { get; }
    /// <summary>Gets the affected coordinate, when applicable.</summary>
    public GridCoordinate? Coordinate { get; }
    /// <summary>Gets the affected date, when applicable.</summary>
    public DateOnly? Date { get; }
    /// <summary>Gets the expected cell, when applicable.</summary>
    public CalendarGridCell? Expected { get; }
    /// <summary>Gets the actual cell, when applicable.</summary>
    public CalendarGridCell? Actual { get; }
}

/// <summary>Immutable differences between expected and actual month grids.</summary>
public sealed record CalendarFixtureDiff
{
    private CalendarFixtureDiff(YearMonth month, IEnumerable<CalendarDifference> differences)
    {
        Month = month;
        Differences = Array.AsReadOnly(differences.ToArray());
    }

    /// <summary>Gets the expected target month.</summary>
    public YearMonth Month { get; }

    /// <summary>Gets all differences in stable coordinate order.</summary>
    public IReadOnlyList<CalendarDifference> Differences { get; }

    /// <summary>Gets whether any differences were found.</summary>
    public bool HasDifferences => Differences.Count != 0;

    /// <summary>Compares two grids by their declared properties, dates, and coordinates.</summary>
    public static CalendarFixtureDiff Compare(CalendarGrid expected, CalendarGrid actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        var differences = new List<CalendarDifference>();
        if (expected.Month != actual.Month)
            differences.Add(new(CalendarDifferenceKind.PropertyMismatch, $"Target month differs: expected {expected.Month}, actual {actual.Month}."));
        if (expected.WeekStartsOn != actual.WeekStartsOn)
            differences.Add(new(CalendarDifferenceKind.PropertyMismatch, $"Week start differs: expected {expected.WeekStartsOn}, actual {actual.WeekStartsOn}."));
        if (expected.GridMode != actual.GridMode || expected.AdjacentDayPolicy != actual.AdjacentDayPolicy)
            differences.Add(new(CalendarDifferenceKind.PropertyMismatch, "Grid mode or adjacent-day policy differs."));
        if (expected.RowCount != actual.RowCount || expected.Cells.Count != actual.Cells.Count)
            differences.Add(new(CalendarDifferenceKind.GridSizeMismatch, $"Grid size differs: expected {expected.RowCount} rows/{expected.Cells.Count} cells, actual {actual.RowCount} rows/{actual.Cells.Count} cells."));

        var expectedByCoordinate = expected.Cells.GroupBy(c => new GridCoordinate(c.Row, c.Column)).ToDictionary(g => g.Key, g => g.First());
        var actualByCoordinate = actual.Cells.GroupBy(c => new GridCoordinate(c.Row, c.Column)).ToDictionary(g => g.Key, g => g.First());
        foreach (var coordinate in expectedByCoordinate.Keys.Union(actualByCoordinate.Keys).OrderBy(c => c.Row).ThenBy(c => c.Column))
        {
            var hasExpected = expectedByCoordinate.TryGetValue(coordinate, out var expectedCell);
            var hasActual = actualByCoordinate.TryGetValue(coordinate, out var actualCell);
            if (!hasExpected)
            {
                differences.Add(new(CalendarDifferenceKind.UnexpectedCell, "Unexpected cell exists.", coordinate, actualCell.Date, null, actualCell));
                continue;
            }
            if (!hasActual)
            {
                differences.Add(new(CalendarDifferenceKind.MissingCell, "Expected cell is missing.", coordinate, expectedCell.Date, expectedCell, null));
                continue;
            }
            if (expectedCell.Date != actualCell.Date)
                differences.Add(new(CalendarDifferenceKind.DateMismatch, $"Date differs: expected {FormatDate(expectedCell.Date)}, actual {FormatDate(actualCell.Date)}.", coordinate, expectedCell.Date, expectedCell, actualCell));
            if (expectedCell.DayOfWeek != actualCell.DayOfWeek)
                differences.Add(new(CalendarDifferenceKind.WeekdayMismatch, $"Weekday differs: expected {expectedCell.DayOfWeek}, actual {actualCell.DayOfWeek}.", coordinate, expectedCell.Date, expectedCell, actualCell));
            if (expectedCell.Relation != actualCell.Relation)
                differences.Add(new(CalendarDifferenceKind.MonthRelationMismatch, $"Month relation differs: expected {expectedCell.Relation}, actual {actualCell.Relation}.", coordinate, expectedCell.Date, expectedCell, actualCell));
        }

        var actualByDate = actual.Cells.Where(c => c.Date.HasValue).GroupBy(c => c.Date!.Value).ToDictionary(g => g.Key, g => g.First());
        foreach (var expectedCell in expected.Cells.Where(c => c.Date.HasValue))
        {
            var date = expectedCell.Date!.Value;
            if (actualByDate.TryGetValue(date, out var actualCell) && (expectedCell.Row != actualCell.Row || expectedCell.Column != actualCell.Column))
                differences.Add(new(CalendarDifferenceKind.CoordinateMismatch, $"Date {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} moved from row {expectedCell.Row}, column {expectedCell.Column} to row {actualCell.Row}, column {actualCell.Column}.",
                    new GridCoordinate(expectedCell.Row, expectedCell.Column), date, expectedCell, actualCell));
        }
        return new CalendarFixtureDiff(expected.Month, differences);
    }

    /// <summary>Compares the generated grids from two month fixtures.</summary>
    public static CalendarFixtureDiff Compare(MonthFixture expected, MonthFixture actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        var gridDiff = Compare(expected.Grid, actual.Grid);
        var differences = gridDiff.Differences.ToList();
        AddPropertyMismatch(nameof(expected.ExpectedDayCount), expected.ExpectedDayCount, actual.ExpectedDayCount);
        AddPropertyMismatch(nameof(expected.FirstDayOfWeek), expected.FirstDayOfWeek, actual.FirstDayOfWeek);
        AddPropertyMismatch(nameof(expected.LastDayOfWeek), expected.LastDayOfWeek, actual.LastDayOfWeek);
        AddPropertyMismatch(nameof(expected.ExpectedGridRows), expected.ExpectedGridRows, actual.ExpectedGridRows);
        AddPropertyMismatch(nameof(expected.LeadingCellCount), expected.LeadingCellCount, actual.LeadingCellCount);
        AddPropertyMismatch(nameof(expected.TrailingCellCount), expected.TrailingCellCount, actual.TrailingCellCount);
        AddPropertyMismatch(nameof(expected.IsLeapYear), expected.IsLeapYear, actual.IsLeapYear);
        return new CalendarFixtureDiff(expected.Month, differences);

        void AddPropertyMismatch<T>(string propertyName, T expectedValue, T actualValue)
        {
            if (!EqualityComparer<T>.Default.Equals(expectedValue, actualValue))
                differences.Add(new(CalendarDifferenceKind.PropertyMismatch, $"{propertyName} differs: expected {expectedValue}, actual {actualValue}."));
        }
    }

    /// <summary>Formats the diff for plain text logs using invariant dates and no ANSI escapes.</summary>
    public string ToText()
    {
        if (!HasDifferences) return "Calendar fixtures match.";
        var builder = new StringBuilder();
        builder.Append("Calendar fixture mismatch");
        if (Month.IsValid) builder.Append(": ").Append(Month.ToString());
        builder.AppendLine();
        foreach (var difference in Differences)
        {
            builder.Append(difference.Kind).Append(": ").Append(difference.Message);
            if (difference.Coordinate is { } coordinate) builder.Append(" [row ").Append(coordinate.Row).Append(", column ").Append(coordinate.Column).Append(']');
            builder.AppendLine();
        }
        return StripAnsiControlSequences(builder.ToString().TrimEnd());
    }

    private static string FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "<placeholder>";

    private static string StripAnsiControlSequences(string text)
    {
        var result = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\u001b' && index + 1 < text.Length && text[index + 1] == '[')
            {
                index += 2;
                while (index < text.Length && (text[index] < '@' || text[index] > '~')) index++;
                continue;
            }
            result.Append(text[index]);
        }
        return result.ToString();
    }
}

/// <summary>Stable JSON snapshot helpers for month fixtures.</summary>
public static class CalendarFixtureJson
{
    private static readonly CalendarFixtureJsonContext Context = new(new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = false });

    /// <summary>Serializes a month fixture as indented JSON.</summary>
    public static string Serialize(MonthFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        return JsonSerializer.Serialize(fixture, Context.MonthFixture);
    }

    /// <summary>Deserializes a month fixture snapshot.</summary>
    public static MonthFixture Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize(json, Context.MonthFixture)
            ?? throw new JsonException("The JSON did not contain a month fixture.");
    }
}

[System.Text.Json.Serialization.JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = false)]
[System.Text.Json.Serialization.JsonSerializable(typeof(MonthFixture))]
internal partial class CalendarFixtureJsonContext : System.Text.Json.Serialization.JsonSerializerContext { }
