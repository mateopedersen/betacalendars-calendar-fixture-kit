using System.Collections.ObjectModel;
using System.Globalization;

namespace BetaCalendars.CalendarFixtureKit;

/// <summary>Severity assigned to an invariant diagnostic.</summary>
public enum ValidationSeverity
{
    /// <summary>A finding that does not invalidate the model.</summary>
    Warning,
    /// <summary>A finding that makes the model invalid.</summary>
    Error
}

/// <summary>Stable, machine-readable invariant diagnostic identifiers.</summary>
public enum CalendarDiagnosticCode
{
    /// <summary>A target-month date is missing.</summary>
    MissingDate,
    /// <summary>A date appears more than once.</summary>
    DuplicateDate,
    /// <summary>A cell has an invalid column.</summary>
    InvalidColumn,
    /// <summary>A cell has an invalid row.</summary>
    InvalidRow,
    /// <summary>A weekday does not align with its date or column.</summary>
    WeekdayMismatch,
    /// <summary>The number of unique target-month dates is incorrect.</summary>
    IncorrectMonthLength,
    /// <summary>A date has the wrong target/adjacent month relation.</summary>
    IncorrectMonthRelation,
    /// <summary>The row or cell count is inconsistent with the grid policy.</summary>
    UnexpectedGridSize,
    /// <summary>Target-month dates are not consecutive.</summary>
    NonSequentialDate,
    /// <summary>A blank grid has inconsistent dimensions or header data.</summary>
    InvalidBlankGrid,
    /// <summary>Multiple blank cells occupy one coordinate.</summary>
    DuplicateCoordinate,
    /// <summary>The grid contains a default or otherwise invalid month value.</summary>
    InvalidMonth
}

/// <summary>A single calendar invariant finding.</summary>
public sealed record ValidationIssue
{
    /// <summary>Creates a diagnostic with a stable identifier and typed category.</summary>
    public ValidationIssue(CalendarDiagnosticCode kind, ValidationSeverity severity, string message,
        GridCoordinate? coordinate = null, DateOnly? date = null)
    {
        Kind = kind;
        Code = kind switch
        {
            CalendarDiagnosticCode.MissingDate => "BCF001",
            CalendarDiagnosticCode.DuplicateDate => "BCF002",
            CalendarDiagnosticCode.InvalidColumn => "BCF003",
            CalendarDiagnosticCode.InvalidRow => "BCF004",
            CalendarDiagnosticCode.WeekdayMismatch => "BCF005",
            CalendarDiagnosticCode.IncorrectMonthLength => "BCF006",
            CalendarDiagnosticCode.IncorrectMonthRelation => "BCF007",
            CalendarDiagnosticCode.UnexpectedGridSize => "BCF008",
            CalendarDiagnosticCode.NonSequentialDate => "BCF009",
            CalendarDiagnosticCode.InvalidBlankGrid => "BCF010",
            CalendarDiagnosticCode.DuplicateCoordinate => "BCF011",
            CalendarDiagnosticCode.InvalidMonth => "BCF012",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        Severity = severity;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Coordinate = coordinate;
        Date = date;
    }

    /// <summary>Gets the stable diagnostic identifier, such as <c>BCF001</c>.</summary>
    public string Code { get; }

    /// <summary>Gets the typed diagnostic category.</summary>
    public CalendarDiagnosticCode Kind { get; }

    /// <summary>Gets the severity of the finding.</summary>
    public ValidationSeverity Severity { get; }

    /// <summary>Gets a human-readable explanation.</summary>
    public string Message { get; }

    /// <summary>Gets the affected row and column, when applicable.</summary>
    public GridCoordinate? Coordinate { get; }

    /// <summary>Gets the affected date, when applicable.</summary>
    public DateOnly? Date { get; }
}

/// <summary>Immutable collection of calendar validation findings.</summary>
public sealed record ValidationResult
{
    /// <summary>Creates a result from the supplied issues.</summary>
    public ValidationResult(IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = new ReadOnlyCollection<ValidationIssue>(issues.ToArray());
    }

    /// <summary>Gets all findings in deterministic order.</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; }

    /// <summary>Gets whether the model satisfies all checked invariants.</summary>
    public bool IsValid => Issues.All(issue => issue.Severity != ValidationSeverity.Error);
}

/// <summary>Checks caller-provided month and blank-grid models against structural invariants.</summary>
public static class CalendarInvariantValidator
{
    /// <summary>Validates all representable date, weekday, relation, and coordinate invariants.</summary>
    public static ValidationResult Validate(CalendarGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var issues = new List<ValidationIssue>();
        if (!grid.Month.IsValid)
        {
            issues.Add(new(CalendarDiagnosticCode.InvalidMonth, ValidationSeverity.Error, "The target month is not initialized."));
            return new ValidationResult(issues);
        }
        var leadingCells = ((int)grid.Month.FirstDay.DayOfWeek - (int)grid.WeekStartsOn + 7) % 7;
        var naturalRows = (leadingCells + grid.Month.DaysInMonth + 6) / 7;
        if (grid.RowCount < 1 ||
            (grid.GridMode == CalendarGridMode.FixedSixWeeks && grid.RowCount != 6) ||
            (grid.GridMode == CalendarGridMode.Natural && grid.RowCount != naturalRows))
            issues.Add(new(CalendarDiagnosticCode.UnexpectedGridSize, ValidationSeverity.Error, "The grid row count is inconsistent with its grid mode."));
        if (!Enum.IsDefined(grid.WeekStartsOn) || !Enum.IsDefined(grid.GridMode) || !Enum.IsDefined(grid.AdjacentDayPolicy))
            issues.Add(new(CalendarDiagnosticCode.UnexpectedGridSize, ValidationSeverity.Error, "The grid contains an undefined layout option."));

        var seenDates = new HashSet<DateOnly>();
        var seenCoordinates = new HashSet<GridCoordinate>();
        var currentMonthDates = new SortedSet<DateOnly>();
        foreach (var cell in grid.Cells)
        {
            var coordinate = new GridCoordinate(cell.Row, cell.Column);
            if (cell.Row < 0 || cell.Row >= grid.RowCount)
                issues.Add(new(CalendarDiagnosticCode.InvalidRow, ValidationSeverity.Error, $"Row {cell.Row} is outside the grid.", coordinate, cell.Date));
            if (cell.Column < 0 || cell.Column >= 7)
                issues.Add(new(CalendarDiagnosticCode.InvalidColumn, ValidationSeverity.Error, $"Column {cell.Column} is outside the seven-column grid.", coordinate, cell.Date));
            if (!seenCoordinates.Add(coordinate))
                issues.Add(new(CalendarDiagnosticCode.DuplicateCoordinate, ValidationSeverity.Error, "More than one cell occupies this coordinate.", coordinate, cell.Date));

            if (cell.Date is not { } date)
            {
                if (cell.DayOfWeek is not null || cell.Relation is not null)
                    issues.Add(new(CalendarDiagnosticCode.IncorrectMonthRelation, ValidationSeverity.Error, "A placeholder must not carry a weekday or month relation.", coordinate));
                continue;
            }

            if (!seenDates.Add(date))
                issues.Add(new(CalendarDiagnosticCode.DuplicateDate, ValidationSeverity.Error, $"Date {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} appears more than once.", coordinate, date));
            if (cell.DayOfWeek != date.DayOfWeek)
                issues.Add(new(CalendarDiagnosticCode.WeekdayMismatch, ValidationSeverity.Error, $"The weekday for {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} does not match its date.", coordinate, date));

            var expectedColumn = ((int)date.DayOfWeek - (int)grid.WeekStartsOn + 7) % 7;
            if (cell.Column >= 0 && cell.Column < 7 && cell.Column != expectedColumn)
                issues.Add(new(CalendarDiagnosticCode.WeekdayMismatch, ValidationSeverity.Error, $"Date {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} is in column {cell.Column}; expected column {expectedColumn}.", coordinate, date));

            var expectedRelation = date.Year == grid.Month.Year && date.Month == grid.Month.Month
                ? MonthRelation.Target
                : date < grid.Month.FirstDay ? MonthRelation.Previous : MonthRelation.Next;
            if (cell.Relation != expectedRelation)
                issues.Add(new(CalendarDiagnosticCode.IncorrectMonthRelation, ValidationSeverity.Error, $"Date {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} has relation {cell.Relation}; expected {expectedRelation}.", coordinate, date));
            if (expectedRelation == MonthRelation.Target) currentMonthDates.Add(date);
        }

        var expectedCount = grid.Month.DaysInMonth;
        if (currentMonthDates.Count != expectedCount)
            issues.Add(new(CalendarDiagnosticCode.IncorrectMonthLength, ValidationSeverity.Error, $"Expected {expectedCount} distinct target-month dates, found {currentMonthDates.Count}."));
        for (var day = 1; day <= expectedCount; day++)
        {
            var date = new DateOnly(grid.Month.Year, grid.Month.Month, day);
            if (!currentMonthDates.Contains(date))
                issues.Add(new(CalendarDiagnosticCode.MissingDate, ValidationSeverity.Error, $"Target-month date {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} is missing.", date: date));
        }

        var ordered = currentMonthDates.ToArray();
        for (var i = 1; i < ordered.Length; i++)
            if (ordered[i - 1].DayNumber + 1 != ordered[i].DayNumber)
                issues.Add(new(CalendarDiagnosticCode.NonSequentialDate, ValidationSeverity.Error, $"Target-month dates {ordered[i - 1].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} and {ordered[i].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} are not consecutive."));

        var expectedCellCount = grid.AdjacentDayPolicy == AdjacentDayPolicy.Exclude
            ? expectedCount
            : grid.RowCount * 7;
        if (grid.Cells.Count != expectedCellCount)
            issues.Add(new(CalendarDiagnosticCode.UnexpectedGridSize, ValidationSeverity.Error, $"Expected {expectedCellCount} cell entries for the grid policy, found {grid.Cells.Count}."));
        return new ValidationResult(issues);
    }

    /// <summary>Validates a blank layout without inventing dates for its cells.</summary>
    public static ValidationResult Validate(BlankCalendarFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        var issues = new List<ValidationIssue>();
        if (fixture.Rows < 1 || fixture.Columns is < 1 or > 7 || fixture.Coordinates.Count != fixture.Rows * fixture.Columns ||
            (fixture.WeekdayHeader.Count != 0 && fixture.WeekdayHeader.Count != fixture.Columns))
            issues.Add(new(CalendarDiagnosticCode.InvalidBlankGrid, ValidationSeverity.Error, "Blank-grid dimensions, coordinates, or weekday header are inconsistent."));
        var seen = new HashSet<GridCoordinate>();
        foreach (var coordinate in fixture.Coordinates)
        {
            if (coordinate.Row < 0 || coordinate.Row >= fixture.Rows)
                issues.Add(new(CalendarDiagnosticCode.InvalidRow, ValidationSeverity.Error, "A blank-grid coordinate has an invalid row.", coordinate));
            if (coordinate.Column < 0 || coordinate.Column >= fixture.Columns)
                issues.Add(new(CalendarDiagnosticCode.InvalidColumn, ValidationSeverity.Error, "A blank-grid coordinate has an invalid column.", coordinate));
            if (!seen.Add(coordinate))
                issues.Add(new(CalendarDiagnosticCode.DuplicateCoordinate, ValidationSeverity.Error, "A blank-grid coordinate occurs more than once.", coordinate));
        }
        if (fixture.WeekdayHeader.Any(day => !Enum.IsDefined(day)))
            issues.Add(new(CalendarDiagnosticCode.InvalidBlankGrid, ValidationSeverity.Error, "The weekday header contains an invalid day."));
        return new ValidationResult(issues);
    }
}
