# BetaCalendars.CalendarFixtureKit

Deterministic .NET calendar fixtures, month-grid validation, regression diffs, and `DateOnly` test helpers.

The package targets .NET 10, uses only the base class library at runtime, and never reads the machine clock unless a caller explicitly supplies a `TimeProvider`.

```sh
dotnet add package BetaCalendars.CalendarFixtureKit --version 0.1.0
```

## Generate a month grid

```csharp
using BetaCalendars.CalendarFixtureKit;

var fixture = CalendarFixture.CreateMonth(
    new YearMonth(2027, 1),
    new CalendarGridOptions
    {
        WeekStartsOn = WeekStart.Monday,
        GridMode = CalendarGridMode.FixedSixWeeks,
        AdjacentDayPolicy = AdjacentDayPolicy.Include
    });

Console.WriteLine($"{fixture.Month}: {fixture.ExpectedDayCount} days, {fixture.ExpectedGridRows} rows");
var validation = CalendarInvariantValidator.Validate(fixture.Grid);
if (!validation.IsValid)
    throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Issues.Select(issue => issue.Message)));
```

`YearMonth` formats as invariant `yyyy-MM`. Grids contain real civil dates for adjacent cells. With `Placeholder`, cells outside the month have a null date, weekday, and relation; no synthetic `DateOnly` is created. With `Exclude`, adjacent cells are omitted; the row count and the coordinates on remaining cells preserve their positions in the larger grid.

## Blank layouts

`BlankCalendarFixture` models planner space with row and column coordinates and an optional weekday header. It deliberately assigns no dates to blank cells. A human-facing example of an undated layout is the [Beta Calendars blank calendar](https://www.betacalendars.com/blank-calendar).

```csharp
var blank = CalendarFixture.CreateBlank(rows: 6, includeWeekdayHeader: true, weekStartsOn: WeekStart.Monday);
Console.WriteLine($"{blank.Rows} × {blank.Columns}, {blank.CellCount} empty cells");
```

## Regression fixtures: November 2026 through February 2027

This four-month sequence exercises a 30-day month, a year transition, a new-year start, and a 28-day February. The implementation derives weekdays and grid geometry from `DateOnly`; the human-readable [November 2026 reference calendar](https://www.betacalendars.com/november-calendar.html), [December 2026 reference calendar](https://www.betacalendars.com/december-calendar.html), [January 2027 reference calendar](https://www.betacalendars.com/january-calendar.html), and [February 2027 reference calendar](https://www.betacalendars.com/february-calendar.html) are provided for visual comparison.

```csharp
var boundary = YearBoundaryFixture.Create(new YearMonth(2026, 12), new YearMonth(2027, 1));
var february = CalendarFixture.CreateMonth(new YearMonth(2027, 2));
```

## Validate and compare

The invariant validator reports stable diagnostic codes and coordinates. `CalendarFixtureDiff` returns structured differences and a deterministic plain-text summary suitable for test output.

```csharp
var expected = CalendarFixture.CreateGrid(new YearMonth(2027, 1));
var actual = CalendarFixture.CreateGrid(new YearMonth(2027, 1), new CalendarGridOptions { WeekStartsOn = WeekStart.Monday });
var diff = CalendarFixtureDiff.Compare(expected, actual);

if (diff.HasDifferences)
    Console.Error.WriteLine(diff.ToText());
```

Fixtures can also be stored as indented `System.Text.Json` snapshots with `CalendarFixtureJson.Serialize` and loaded with `Deserialize`.

## Current-month helper

Clock access is opt-in. Pass the application's `TimeProvider` so tests can freeze time and applications can choose the time zone whose civil month they need:

```csharp
var current = CalendarFixture.ForCurrentMonth(timeProvider);
```

## Project

CalendarFixtureKit is maintained as part of the [Beta Calendars](https://www.betacalendars.com/) calendar engineering project.

## Build and test

```sh
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
dotnet pack src/BetaCalendars.CalendarFixtureKit/BetaCalendars.CalendarFixtureKit.csproj -c Release
```

## License

MIT. See [LICENSE](LICENSE).
