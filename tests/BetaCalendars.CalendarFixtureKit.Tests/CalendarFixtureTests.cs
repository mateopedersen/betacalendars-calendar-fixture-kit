using System.Globalization;
using BetaCalendars.CalendarFixtureKit;
using Xunit;

namespace BetaCalendars.CalendarFixtureKit.Tests;

public sealed class CalendarFixtureTests
{
    [Fact]
    public void YearMonth_UsesInvariantCanonicalFormatAndBounds()
    {
        var january = new YearMonth(2027, 1);
        Assert.Equal("2027-01", january.ToString());
        Assert.Equal(january, YearMonth.Parse("2027-01"));
        Assert.True(YearMonth.TryParse("0001-12", out var firstYearDecember));
        Assert.Equal(new YearMonth(2, 1), firstYearDecember.AddMonths(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new YearMonth(2027, 13));
        Assert.Throws<ArgumentOutOfRangeException>(() => new YearMonth(0, 1));
        Assert.Throws<FormatException>(() => YearMonth.Parse("2027-1"));
        Assert.Equal(-1, new YearMonth(2026, 12).CompareTo(new YearMonth(2027, 1)));
    }

    [Theory]
    [InlineData(1900, false)]
    [InlineData(2000, true)]
    [InlineData(2024, true)]
    [InlineData(2027, false)]
    [InlineData(2100, false)]
    [InlineData(2400, true)]
    public void LeapYearFixture_FollowsGregorianRules(int year, bool expected)
    {
        var fixture = LeapYearFixture.Create(year);
        Assert.Equal(expected, fixture.IsLeapYear);
        Assert.Equal(expected ? 29 : 28, fixture.February.ExpectedDayCount);
    }

    [Fact]
    public void November2026_HasCorrectSundayAndMondayLayouts()
    {
        var sunday = CalendarFixture.CreateMonth(new YearMonth(2026, 11));
        var monday = CalendarFixture.CreateMonth(new YearMonth(2026, 11), new CalendarGridOptions { WeekStartsOn = WeekStart.Monday });
        var fixedGrid = CalendarFixture.CreateGrid(new YearMonth(2026, 11), new CalendarGridOptions { GridMode = CalendarGridMode.FixedSixWeeks });
        Assert.Equal(30, sunday.ExpectedDayCount);
        Assert.Equal(DayOfWeek.Sunday, sunday.FirstDayOfWeek);
        Assert.Equal(DayOfWeek.Monday, sunday.LastDayOfWeek);
        Assert.Equal(5, sunday.ExpectedGridRows);
        Assert.Equal(6, monday.ExpectedGridRows);
        Assert.Equal(42, fixedGrid.Cells.Count);
        Assert.Empty(CalendarInvariantValidator.Validate(sunday.Grid).Issues);
        Assert.Empty(CalendarInvariantValidator.Validate(monday.Grid).Issues);
    }

    [Fact]
    public void YearBoundaryFixture_ContinuesFromDecemberToJanuary()
    {
        var boundary = YearBoundaryFixture.Create(new YearMonth(2026, 12), new YearMonth(2027, 1));
        Assert.Equal(new DateOnly(2026, 12, 31), boundary.Before.Grid.Cells.Single(c => c.Date == new DateOnly(2026, 12, 31)).Date);
        Assert.Contains(boundary.Before.Grid.Cells, c => c.Date == new DateOnly(2027, 1, 1) && c.Relation == MonthRelation.Next);
        Assert.Contains(boundary.After.Grid.Cells, c => c.Date == new DateOnly(2026, 12, 31) && c.Relation == MonthRelation.Previous);
        Assert.Throws<ArgumentException>(() => YearBoundaryFixture.Create(new YearMonth(2026, 11), new YearMonth(2027, 1)));
    }

    [Fact]
    public void DecemberJanuaryAndFebruary2027HaveExpectedCivilDateBoundaries()
    {
        var december = CalendarFixture.CreateMonth(new YearMonth(2026, 12));
        var january = CalendarFixture.CreateMonth(new YearMonth(2027, 1));
        var february = CalendarFixture.CreateMonth(new YearMonth(2027, 2));
        Assert.Equal(31, december.ExpectedDayCount);
        Assert.Equal(DayOfWeek.Thursday, december.LastDayOfWeek);
        Assert.Equal(DayOfWeek.Friday, january.FirstDayOfWeek);
        Assert.Equal(28, february.ExpectedDayCount);
        Assert.False(february.IsLeapYear);
        Assert.Contains(february.Grid.Cells, cell => cell.Date == new DateOnly(2027, 3, 1) && cell.Relation == MonthRelation.Next);
        Assert.Equal(5, CalendarFixture.CreateMonth(new YearMonth(2027, 2), new CalendarGridOptions { WeekStartsOn = WeekStart.Sunday }).ExpectedGridRows);
        Assert.Equal(4, CalendarFixture.CreateMonth(new YearMonth(2027, 2), new CalendarGridOptions { WeekStartsOn = WeekStart.Monday }).ExpectedGridRows);
    }

    [Fact]
    public void AdjacentPolicies_PreserveOrOmitDatesWithoutInventingPlaceholderDates()
    {
        var month = new YearMonth(2027, 2);
        var included = CalendarFixture.CreateGrid(month, new CalendarGridOptions { AdjacentDayPolicy = AdjacentDayPolicy.Include });
        var excluded = CalendarFixture.CreateGrid(month, new CalendarGridOptions { AdjacentDayPolicy = AdjacentDayPolicy.Exclude });
        var placeholders = CalendarFixture.CreateGrid(month, new CalendarGridOptions { AdjacentDayPolicy = AdjacentDayPolicy.Placeholder });
        Assert.Contains(included.Cells, c => c.Relation == MonthRelation.Next);
        Assert.DoesNotContain(excluded.Cells, c => c.Relation != MonthRelation.Target);
        Assert.Contains(placeholders.Cells, c => c.IsPlaceholder && c.Date is null);
        Assert.Empty(CalendarInvariantValidator.Validate(included).Issues);
        Assert.Empty(CalendarInvariantValidator.Validate(excluded).Issues);
        Assert.Empty(CalendarInvariantValidator.Validate(placeholders).Issues);
    }

    [Fact]
    public void OutOfRangeAdjacentDatesBecomePlaceholdersOrAreRejectedWhenInclusionIsRequested()
    {
        var start = new YearMonth(1, 1);
        var placeholders = CalendarFixture.CreateGrid(start, new CalendarGridOptions { AdjacentDayPolicy = AdjacentDayPolicy.Placeholder });
        Assert.Contains(placeholders.Cells, cell => cell.IsPlaceholder);
        Assert.True(CalendarInvariantValidator.Validate(placeholders).IsValid);
        Assert.Throws<ArgumentOutOfRangeException>(() => CalendarFixture.CreateGrid(start));
        Assert.Throws<ArgumentOutOfRangeException>(() => CalendarFixture.CreateGrid(new YearMonth(9999, 12)));
    }

    [Fact]
    public void BlankFixture_IsDateFreeAndValidatesItsCoordinates()
    {
        var blank = CalendarFixture.CreateBlank(6, includeWeekdayHeader: true, weekStartsOn: WeekStart.Monday);
        Assert.Equal(42, blank.CellCount);
        Assert.Equal(DayOfWeek.Monday, blank.WeekdayHeader[0]);
        Assert.Empty(CalendarInvariantValidator.Validate(blank).Issues);
        Assert.All(blank.Coordinates, coordinate => Assert.True(coordinate.Row >= 0 && coordinate.Column >= 0));
    }

    [Fact]
    public void ValidatorReportsMissingDuplicateAndMisplacedCells()
    {
        var expected = CalendarFixture.CreateGrid(new YearMonth(2027, 1));
        var cells = expected.Cells.ToList();
        var jan14 = cells.FindIndex(c => c.Date == new DateOnly(2027, 1, 14));
        cells.RemoveAt(jan14);
        cells.Add(expected.Cells.Single(c => c.Date == new DateOnly(2027, 1, 21)));
        var jan1 = cells.FindIndex(c => c.Date == new DateOnly(2027, 1, 1));
        cells[jan1] = cells[jan1] with { Column = (cells[jan1].Column + 1) % 7 };
        var result = CalendarInvariantValidator.Validate(new CalendarGrid(expected.Month, expected.WeekStartsOn, expected.RowCount, cells));
        Assert.Contains(result.Issues, issue => issue.Kind == CalendarDiagnosticCode.MissingDate && issue.Code == "BCF001");
        Assert.Contains(result.Issues, issue => issue.Kind == CalendarDiagnosticCode.DuplicateDate && issue.Code == "BCF002");
        Assert.Contains(result.Issues, issue => issue.Kind == CalendarDiagnosticCode.WeekdayMismatch && issue.Code == "BCF005");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Diff_IsStructuredAndTextOutputIsDeterministic()
    {
        var expected = CalendarFixture.CreateGrid(new YearMonth(2027, 1));
        var changed = expected.Cells.Select(c => c.Date == new DateOnly(2027, 1, 1) ? c with { Column = c.Column + 1 } : c);
        var actual = new CalendarGrid(expected.Month, expected.WeekStartsOn, expected.RowCount, changed.ToArray());
        var diff = CalendarFixtureDiff.Compare(expected, actual);
        Assert.True(diff.HasDifferences);
        Assert.Contains(diff.Differences, item => item.Kind == CalendarDifferenceKind.CoordinateMismatch);
        Assert.Contains("2027-01", diff.ToText());
        var text = diff.ToText();
        Assert.DoesNotContain('\u001b', text);
    }

    [Fact]
    public void Json_RoundTripsMonthFixture()
    {
        var fixture = CalendarFixture.CreateMonth(new YearMonth(2027, 2), new CalendarGridOptions { WeekStartsOn = WeekStart.Monday });
        var json = CalendarFixtureJson.Serialize(fixture);
        var copy = CalendarFixtureJson.Deserialize(json);
        Assert.Equal(fixture.Month, copy.Month);
        Assert.Equal(fixture.Grid.Cells, copy.Grid.Cells);
        Assert.Equal(json, CalendarFixtureJson.Serialize(copy));
    }

    [Fact]
    public void Geometry_IsIndependentOfCurrentCulture()
    {
        var priorCulture = CultureInfo.CurrentCulture;
        var priorUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            var fixture = CalendarFixture.CreateMonth(new YearMonth(2027, 1));
            Assert.Equal("2027-01", fixture.Month.ToString());
            Assert.Equal(31, fixture.Grid.Cells.Count(c => c.IsInTargetMonth));
        }
        finally
        {
            CultureInfo.CurrentCulture = priorCulture;
            CultureInfo.CurrentUICulture = priorUiCulture;
        }
    }

    [Fact]
    public void TimeProviderMustBeExplicitAndUsesItsLocalCivilDate()
    {
        var provider = new FixedTimeProvider(new DateTimeOffset(2027, 1, 31, 23, 30, 0, TimeSpan.Zero));
        Assert.Equal(new YearMonth(2027, 1), CalendarFixture.ForCurrentMonth(provider).Month);
        Assert.Throws<ArgumentNullException>(() => CalendarFixture.ForCurrentMonth(null!));
    }

    [Fact]
    public void EveryMonthFrom1900Through2100HasConsistentDateGeometry()
    {
        for (var year = 1900; year <= 2100; year++)
            for (var month = 1; month <= 12; month++)
                foreach (var weekStart in Enum.GetValues<WeekStart>())
                {
                    var grid = CalendarFixture.CreateGrid(new YearMonth(year, month), new CalendarGridOptions { WeekStartsOn = weekStart, GridMode = CalendarGridMode.FixedSixWeeks });
                    Assert.Equal(42, grid.Cells.Count);
                    Assert.Equal(grid.Month.DaysInMonth, grid.Cells.Count(c => c.IsInTargetMonth));
                    Assert.All(grid.Cells.Where(c => c.Date.HasValue), cell => Assert.Equal(cell.Date!.Value.DayOfWeek, cell.DayOfWeek));
                    Assert.True(CalendarInvariantValidator.Validate(grid).IsValid);
                }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => now;
    }
}
