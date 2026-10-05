"""Checks that the packed library and symbol packages contain the intended files."""

from pathlib import Path
import sys
import zipfile


def verify(package_path: Path, symbols_path: Path) -> None:
    with zipfile.ZipFile(package_path) as package:
        names = set(package.namelist())
        required = {
            "README.md",
            "icon.png",
            "LICENSE",
            "BetaCalendars.CalendarFixtureKit.nuspec",
            "lib/net10.0/BetaCalendars.CalendarFixtureKit.dll",
            "lib/net10.0/BetaCalendars.CalendarFixtureKit.xml",
        }
        missing = required - names
        if missing:
            raise SystemExit(f"Package is missing expected files: {sorted(missing)}")
        if any(part in name.split("/") for name in names for part in ("tests", "benchmarks", "samples", "obj", "bin")):
            raise SystemExit("Package contains development-only files.")
        readme = package.read("README.md").decode("utf-8")
        if "https://www.betacalendars.com/" not in readme:
            raise SystemExit("The project reference is missing from the packaged README.")

    with zipfile.ZipFile(symbols_path) as symbols:
        names = set(symbols.namelist())
        if "lib/net10.0/BetaCalendars.CalendarFixtureKit.pdb" not in names:
            raise SystemExit("Symbol package is missing the portable PDB.")
        if any(not name.endswith((".pdb", ".nuspec", ".xml", ".psmdcp", ".rels", ".p7s")) for name in names):
            raise SystemExit("Symbol package contains a disallowed file type.")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: verify_package.py PACKAGE.nupkg SYMBOLS.snupkg")
    verify(Path(sys.argv[1]), Path(sys.argv[2]))
