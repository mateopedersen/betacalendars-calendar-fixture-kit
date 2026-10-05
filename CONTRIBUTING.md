# Contributing

Issues and pull requests are welcome. Keep public APIs immutable and date-only where possible, preserve deterministic behavior, and include focused tests for new invariants or edge cases.

Before opening a pull request, run:

```sh
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
dotnet format --verify-no-changes
```

Do not include generated `bin`, `obj`, benchmark output, credentials, or package artifacts in a source change.
