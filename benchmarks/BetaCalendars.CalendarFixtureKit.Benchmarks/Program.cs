using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BetaCalendars.CalendarFixtureKit;

BenchmarkRunner.Run<FixtureBenchmarks>();

[MemoryDiagnoser]
public class FixtureBenchmarks
{
    private readonly YearMonth month = new(2027, 1);
    private readonly MonthFixture fixture = CalendarFixture.CreateMonth(new YearMonth(2027, 1));
    private readonly CalendarGrid grid = CalendarFixture.CreateGrid(new YearMonth(2027, 1), new CalendarGridOptions { GridMode = CalendarGridMode.FixedSixWeeks });

    [Benchmark] public MonthFixture GenerateMonthFixture() => CalendarFixture.CreateMonth(month);
    [Benchmark] public CalendarGrid GenerateFixedSixWeekGrid() => CalendarFixture.CreateGrid(month, new CalendarGridOptions { GridMode = CalendarGridMode.FixedSixWeeks });
    [Benchmark] public ValidationResult ValidateGrid() => CalendarInvariantValidator.Validate(grid);
    [Benchmark] public CalendarFixtureDiff CompareFixtures() => CalendarFixtureDiff.Compare(grid, grid);
    [Benchmark] public string SerializeFixture() => CalendarFixtureJson.Serialize(fixture);
}
