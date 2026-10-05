using System.Globalization;

namespace BetaCalendars.CalendarFixtureKit;

/// <summary>Identifies a Gregorian civil month without a time zone or time of day.</summary>
public readonly record struct YearMonth : IComparable<YearMonth>, ISpanFormattable
{
    /// <summary>Creates a month for a year in the <see cref="DateOnly"/> range.</summary>
    [System.Text.Json.Serialization.JsonConstructor]
    public YearMonth(int year, int month)
    {
        if (year is < 1 or > 9999) throw new ArgumentOutOfRangeException(nameof(year), "Year must be between 1 and 9999.");
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        Year = year;
        Month = month;
    }

    /// <summary>Gets the four-digit Gregorian year.</summary>
    public int Year { get; }

    /// <summary>Gets the month number from 1 through 12.</summary>
    public int Month { get; }

    /// <summary>Gets whether the value is initialized to a valid Gregorian month.</summary>
    public bool IsValid => Year is >= 1 and <= 9999 && Month is >= 1 and <= 12;

    /// <summary>Gets the first civil date in this month.</summary>
    public DateOnly FirstDay => new(Year, Month, 1);

    /// <summary>Gets the number of days in this month.</summary>
    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);

    /// <summary>Returns this month moved by the requested number of months.</summary>
    public YearMonth AddMonths(int months)
    {
        var absoluteMonth = ((long)Year - 1) * 12 + Month - 1 + months;
        if (absoluteMonth is < 0 or >= 119988) throw new ArgumentOutOfRangeException(nameof(months), "The resulting month must remain in the DateOnly range.");
        return new YearMonth((int)(absoluteMonth / 12) + 1, (int)(absoluteMonth % 12) + 1);
    }

    /// <summary>Compares months in chronological order.</summary>
    public int CompareTo(YearMonth other)
    {
        var yearComparison = Year.CompareTo(other.Year);
        return yearComparison != 0 ? yearComparison : Month.CompareTo(other.Month);
    }

    /// <summary>Formats the month as invariant <c>yyyy-MM</c>.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Year:0000}-{Month:00}");

    /// <summary>Parses the invariant <c>yyyy-MM</c> representation.</summary>
    public static YearMonth Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!TryParse(value.AsSpan(), out var result)) throw new FormatException("Expected a month in yyyy-MM format.");
        return result;
    }

    /// <summary>Attempts to parse the invariant <c>yyyy-MM</c> representation.</summary>
    public static bool TryParse(string? value, out YearMonth result) => TryParse(value.AsSpan(), out result);

    /// <summary>Attempts to parse the invariant <c>yyyy-MM</c> representation.</summary>
    public static bool TryParse(ReadOnlySpan<char> value, out YearMonth result)
    {
        result = default;
        if (value.Length != 7 || value[4] != '-' ||
            !int.TryParse(value[..4], NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(value[5..], NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
            year is < 1 or > 9999 || month is < 1 or > 12) return false;
        result = new YearMonth(year, month);
        return true;
    }

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    /// <inheritdoc />
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        Span<char> value = stackalloc char[7];
        value[0] = (char)('0' + Year / 1000);
        value[1] = (char)('0' + Year / 100 % 10);
        value[2] = (char)('0' + Year / 10 % 10);
        value[3] = (char)('0' + Year % 10);
        value[4] = '-';
        value[5] = (char)('0' + Month / 10);
        value[6] = (char)('0' + Month % 10);
        if (destination.Length < value.Length) { charsWritten = 0; return false; }
        value.CopyTo(destination);
        charsWritten = value.Length;
        return true;
    }
}

/// <summary>Specifies the first weekday column in a calendar grid.</summary>
public enum WeekStart
{
    /// <summary>Sunday is the first column.</summary>
    Sunday = 0,
    /// <summary>Monday is the first column.</summary>
    Monday = 1,
    /// <summary>Tuesday is the first column.</summary>
    Tuesday = 2,
    /// <summary>Wednesday is the first column.</summary>
    Wednesday = 3,
    /// <summary>Thursday is the first column.</summary>
    Thursday = 4,
    /// <summary>Friday is the first column.</summary>
    Friday = 5,
    /// <summary>Saturday is the first column.</summary>
    Saturday = 6
}

/// <summary>Specifies the number of rows used for a month grid.</summary>
public enum CalendarGridMode
{
    /// <summary>Use the minimum whole number of weeks needed to contain the month.</summary>
    Natural,
    /// <summary>Always use six rows of seven cells.</summary>
    FixedSixWeeks
}

/// <summary>Specifies how cells outside the target month are represented.</summary>
public enum AdjacentDayPolicy
{
    /// <summary>Include real dates from adjacent months.</summary>
    Include,
    /// <summary>Omit adjacent-month cells from the collection.</summary>
    Exclude,
    /// <summary>Keep adjacent positions as cells without dates.</summary>
    Placeholder
}

/// <summary>Describes a date's relationship to the grid's target month.</summary>
public enum MonthRelation
{
    /// <summary>The date belongs to the preceding month.</summary>
    Previous,
    /// <summary>The date belongs to the target month.</summary>
    Target,
    /// <summary>The date belongs to the following month.</summary>
    Next
}

/// <summary>Options for deterministic month-grid generation.</summary>
public sealed record CalendarGridOptions
{
    /// <summary>Gets or initializes the first weekday column.</summary>
    public WeekStart WeekStartsOn { get; init; } = WeekStart.Sunday;

    /// <summary>Gets or initializes the row-count policy.</summary>
    public CalendarGridMode GridMode { get; init; } = CalendarGridMode.Natural;

    /// <summary>Gets or initializes how adjacent month cells are represented.</summary>
    public AdjacentDayPolicy AdjacentDayPolicy { get; init; } = AdjacentDayPolicy.Include;
}

/// <summary>A single dated or explicitly empty position in a month grid.</summary>
public readonly record struct CalendarGridCell
{
    /// <summary>Creates a dated cell or a date-free placeholder.</summary>
    [System.Text.Json.Serialization.JsonConstructor]
    public CalendarGridCell(DateOnly? date, int row, int column, DayOfWeek? dayOfWeek, MonthRelation? relation)
    {
        Date = date;
        Row = row;
        Column = column;
        DayOfWeek = dayOfWeek;
        Relation = relation;
    }

    /// <summary>Gets the civil date, or null for an explicit placeholder.</summary>
    public DateOnly? Date { get; init; }

    /// <summary>Gets the zero-based row.</summary>
    public int Row { get; init; }

    /// <summary>Gets the zero-based weekday column.</summary>
    public int Column { get; init; }

    /// <summary>Gets the date's weekday, or null for a placeholder.</summary>
    public DayOfWeek? DayOfWeek { get; init; }

    /// <summary>Gets the date's month relation, or null for a placeholder.</summary>
    public MonthRelation? Relation { get; init; }

    /// <summary>Gets whether the cell has no date and represents an empty placeholder.</summary>
    public bool IsPlaceholder => Date is null;

    /// <summary>Gets whether this cell's date belongs to the target month.</summary>
    public bool IsInTargetMonth => Relation == MonthRelation.Target;
}

/// <summary>A rectangular month-grid model; consumers cannot mutate its cell collection.</summary>
public sealed record CalendarGrid
{
    /// <summary>Creates a grid model, including for validating a caller-provided layout.</summary>
    [System.Text.Json.Serialization.JsonConstructor]
    public CalendarGrid(YearMonth month, WeekStart weekStartsOn, int rowCount, IReadOnlyList<CalendarGridCell> cells,
        CalendarGridMode gridMode = CalendarGridMode.Natural,
        AdjacentDayPolicy adjacentDayPolicy = AdjacentDayPolicy.Include)
    {
        ArgumentNullException.ThrowIfNull(cells);
        Month = month;
        WeekStartsOn = weekStartsOn;
        RowCount = rowCount;
        Cells = Array.AsReadOnly(cells.ToArray());
        GridMode = gridMode;
        AdjacentDayPolicy = adjacentDayPolicy;
    }

    /// <summary>Creates a grid from any enumerable collection of cells.</summary>
    public CalendarGrid(YearMonth month, WeekStart weekStartsOn, int rowCount, IEnumerable<CalendarGridCell> cells,
        CalendarGridMode gridMode = CalendarGridMode.Natural,
        AdjacentDayPolicy adjacentDayPolicy = AdjacentDayPolicy.Include)
        : this(month, weekStartsOn, rowCount, cells?.ToArray() ?? throw new ArgumentNullException(nameof(cells)), gridMode, adjacentDayPolicy) { }

    /// <summary>Gets the month represented by the grid.</summary>
    public YearMonth Month { get; init; }

    /// <summary>Gets the first weekday column.</summary>
    public WeekStart WeekStartsOn { get; init; }

    /// <summary>Gets the intended row count.</summary>
    public int RowCount { get; init; }

    /// <summary>Gets the grid cells.</summary>
    public IReadOnlyList<CalendarGridCell> Cells { get; init; }

    /// <summary>Gets the requested row-count policy.</summary>
    public CalendarGridMode GridMode { get; init; }

    /// <summary>Gets the policy used for adjacent dates.</summary>
    public AdjacentDayPolicy AdjacentDayPolicy { get; init; }
}

/// <summary>Generates reproducible calendar fixtures and grids.</summary>
public static class CalendarFixture
{
    /// <summary>Creates a fixture for the specified month.</summary>
    public static MonthFixture CreateMonth(YearMonth month, CalendarGridOptions? options = null)
    {
        options ??= new CalendarGridOptions();
        ValidateOptions(options);
        var grid = CreateGrid(month, options);
        return new MonthFixture(month, month.DaysInMonth, month.FirstDay.DayOfWeek,
            month.FirstDay.AddDays(month.DaysInMonth - 1).DayOfWeek, grid.RowCount,
            (int)(((int)month.FirstDay.DayOfWeek - (int)options.WeekStartsOn + 7) % 7),
            grid.RowCount * 7 - (int)(((int)month.FirstDay.DayOfWeek - (int)options.WeekStartsOn + 7) % 7) - month.DaysInMonth,
            DateTime.IsLeapYear(month.Year), grid);
    }

    /// <summary>Creates a month grid for the specified month.</summary>
    public static CalendarGrid CreateGrid(YearMonth month, CalendarGridOptions? options = null)
    {
        if (!month.IsValid) throw new ArgumentOutOfRangeException(nameof(month), "The month must be initialized and valid.");
        options ??= new CalendarGridOptions();
        ValidateOptions(options);
        var leading = (int)(((int)month.FirstDay.DayOfWeek - (int)options.WeekStartsOn + 7) % 7);
        var naturalCellCount = ((leading + month.DaysInMonth + 6) / 7) * 7;
        var cellCount = options.GridMode == CalendarGridMode.FixedSixWeeks ? 42 : naturalCellCount;
        var rows = cellCount / 7;
        var startDayNumber = month.FirstDay.DayNumber - leading;
        var cells = new List<CalendarGridCell>(cellCount);
        for (var index = 0; index < cellCount; index++)
        {
            var dayNumber = startDayNumber + index;
            DateOnly? date = dayNumber is >= 0 and <= 3652058 ? DateOnly.FromDayNumber(dayNumber) : null;
            var inTarget = date is { } candidate && candidate.Year == month.Year && candidate.Month == month.Month;
            if (!inTarget && options.AdjacentDayPolicy == AdjacentDayPolicy.Exclude) continue;
            if (!inTarget && options.AdjacentDayPolicy == AdjacentDayPolicy.Include && date is null)
                throw new ArgumentOutOfRangeException(nameof(month), "Adjacent dates outside the DateOnly range cannot be included.");
            var isPlaceholder = options.AdjacentDayPolicy == AdjacentDayPolicy.Placeholder && !inTarget;
            cells.Add(new CalendarGridCell(
                isPlaceholder ? null : date,
                index / 7, index % 7,
                isPlaceholder ? null : date?.DayOfWeek,
                isPlaceholder || date is null ? null : inTarget ? MonthRelation.Target : date.Value < month.FirstDay ? MonthRelation.Previous : MonthRelation.Next));
        }
        return new CalendarGrid(month, options.WeekStartsOn, rows, cells, options.GridMode, options.AdjacentDayPolicy);
    }

    /// <summary>Creates a blank grid with coordinates but no assigned dates.</summary>
    public static BlankCalendarFixture CreateBlank(int rows, int columns = 7, bool includeWeekdayHeader = true,
        WeekStart weekStartsOn = WeekStart.Sunday)
    {
        if (rows is < 1 or > 52) throw new ArgumentOutOfRangeException(nameof(rows), "Rows must be between 1 and 52.");
        if (columns is < 1 or > 7) throw new ArgumentOutOfRangeException(nameof(columns), "Columns must be between 1 and 7.");
        if (!Enum.IsDefined(weekStartsOn)) throw new ArgumentOutOfRangeException(nameof(weekStartsOn));
        var cells = Enumerable.Range(0, rows).SelectMany(row => Enumerable.Range(0, columns).Select(column => new GridCoordinate(row, column))).ToArray();
        var header = includeWeekdayHeader
            ? Enumerable.Range(0, columns).Select(offset => (DayOfWeek)(((int)weekStartsOn + offset) % 7)).ToArray()
            : Array.Empty<DayOfWeek>();
        return new BlankCalendarFixture(rows, columns, Array.AsReadOnly(cells), Array.AsReadOnly(header));
    }

    /// <summary>Creates the month containing the local civil date supplied by the time provider.</summary>
    public static MonthFixture ForCurrentMonth(TimeProvider timeProvider, CalendarGridOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var localDate = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        return CreateMonth(new YearMonth(localDate.Year, localDate.Month), options);
    }

    private static void ValidateOptions(CalendarGridOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.WeekStartsOn)) throw new ArgumentOutOfRangeException(nameof(options), "WeekStartsOn is not defined.");
        if (!Enum.IsDefined(options.GridMode)) throw new ArgumentOutOfRangeException(nameof(options), "GridMode is not defined.");
        if (!Enum.IsDefined(options.AdjacentDayPolicy)) throw new ArgumentOutOfRangeException(nameof(options), "AdjacentDayPolicy is not defined.");
    }
}

/// <summary>A zero-based row and column in a grid.</summary>
public readonly record struct GridCoordinate
{
    /// <summary>Creates a row and column coordinate.</summary>
    [System.Text.Json.Serialization.JsonConstructor]
    public GridCoordinate(int row, int column) { Row = row; Column = column; }
    /// <summary>Gets the zero-based row.</summary>
    public int Row { get; init; }
    /// <summary>Gets the zero-based column.</summary>
    public int Column { get; init; }
}

/// <summary>A date-free blank layout for planner and component tests.</summary>
public sealed record BlankCalendarFixture
{
    /// <summary>Creates a blank layout using immutable snapshots of the supplied coordinate lists.</summary>
    public BlankCalendarFixture(int rows, int columns, IReadOnlyList<GridCoordinate> coordinates, IReadOnlyList<DayOfWeek> weekdayHeader)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(weekdayHeader);
        Rows = rows;
        Columns = columns;
        Coordinates = Array.AsReadOnly(coordinates.ToArray());
        WeekdayHeader = Array.AsReadOnly(weekdayHeader.ToArray());
    }

    /// <summary>Gets the row count.</summary>
    public int Rows { get; init; }

    /// <summary>Gets the column count.</summary>
    public int Columns { get; init; }

    /// <summary>Gets all cell coordinates without dates.</summary>
    public IReadOnlyList<GridCoordinate> Coordinates { get; init; }

    /// <summary>Gets the optional weekday header.</summary>
    public IReadOnlyList<DayOfWeek> WeekdayHeader { get; init; }

    /// <summary>Gets the total number of blank cells.</summary>
    public int CellCount => Coordinates.Count;
}

/// <summary>Expected structural facts and the generated grid for a civil month.</summary>
public sealed record MonthFixture
{
    /// <summary>Creates a month fixture.</summary>
    public MonthFixture(YearMonth month, int expectedDayCount, DayOfWeek firstDayOfWeek, DayOfWeek lastDayOfWeek,
        int expectedGridRows, int leadingCellCount, int trailingCellCount, bool isLeapYear, CalendarGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        Month = month;
        ExpectedDayCount = expectedDayCount;
        FirstDayOfWeek = firstDayOfWeek;
        LastDayOfWeek = lastDayOfWeek;
        ExpectedGridRows = expectedGridRows;
        LeadingCellCount = leadingCellCount;
        TrailingCellCount = trailingCellCount;
        IsLeapYear = isLeapYear;
        Grid = grid;
    }

    /// <summary>Gets the represented month.</summary>
    public YearMonth Month { get; init; }
    /// <summary>Gets the number of dates in the month.</summary>
    public int ExpectedDayCount { get; init; }
    /// <summary>Gets the weekday of the first date.</summary>
    public DayOfWeek FirstDayOfWeek { get; init; }
    /// <summary>Gets the weekday of the final date.</summary>
    public DayOfWeek LastDayOfWeek { get; init; }
    /// <summary>Gets the grid row count.</summary>
    public int ExpectedGridRows { get; init; }
    /// <summary>Gets the number of positions preceding the first date.</summary>
    public int LeadingCellCount { get; init; }
    /// <summary>Gets the number of positions following the final date.</summary>
    public int TrailingCellCount { get; init; }
    /// <summary>Gets whether the Gregorian year is a leap year.</summary>
    public bool IsLeapYear { get; init; }
    /// <summary>Gets the generated month grid.</summary>
    public CalendarGrid Grid { get; init; }
}

/// <summary>A pair of fixtures around a consecutive month or year boundary.</summary>
public sealed record YearBoundaryFixture
{
    /// <summary>Creates a pair from fixtures for consecutive months.</summary>
    public YearBoundaryFixture(MonthFixture before, MonthFixture after) { Before = before; After = after; }
    /// <summary>Gets the earlier month's fixture.</summary>
    public MonthFixture Before { get; init; }
    /// <summary>Gets the later month's fixture.</summary>
    public MonthFixture After { get; init; }

    /// <summary>Creates fixtures for two consecutive months.</summary>
    public static YearBoundaryFixture Create(YearMonth before, YearMonth after, CalendarGridOptions? options = null)
    {
        if (before.AddMonths(1) != after) throw new ArgumentException("The months must be consecutive.", nameof(after));
        return new YearBoundaryFixture(CalendarFixture.CreateMonth(before, options), CalendarFixture.CreateMonth(after, options));
    }
}

/// <summary>Useful leap-year facts and its February fixture.</summary>
public sealed record LeapYearFixture
{
    /// <summary>Creates a Gregorian leap-year fixture.</summary>
    public LeapYearFixture(int year, bool isLeapYear, MonthFixture february) { Year = year; IsLeapYear = isLeapYear; February = february; }
    /// <summary>Gets the Gregorian year.</summary>
    public int Year { get; init; }
    /// <summary>Gets whether this year is a leap year.</summary>
    public bool IsLeapYear { get; init; }
    /// <summary>Gets the February month fixture.</summary>
    public MonthFixture February { get; init; }

    /// <summary>Creates the Gregorian leap-year fixture for a year.</summary>
    public static LeapYearFixture Create(int year, CalendarGridOptions? options = null)
    {
        if (year is < 1 or > 9999) throw new ArgumentOutOfRangeException(nameof(year));
        return new LeapYearFixture(year, DateTime.IsLeapYear(year), CalendarFixture.CreateMonth(new YearMonth(year, 2), options));
    }
}
