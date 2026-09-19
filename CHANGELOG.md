# Changelog

All notable changes to this project are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Core (`Hephaestus.Optimisation`): immutable linear and boolean expression records with C# 14 extension operators; problems as a single constraint with an optional objective; normalisation to affine and negation normal forms; MILP encoding with guarded rows, bound propagation and per-row derived big-M values; typed `Quantity<T>` and `Point<T, TDelta>` expressions with projections for `TimeSpan`, `DateTime` and `DateTimeOffset`; the `ISolver` and `IMilpBackend` seams.
- `Hephaestus.Optimisation.OrTools`: MILP backend on Google OR-Tools.
- `Hephaestus.Optimisation.Z3`: SMT backend on Microsoft Z3.
- `Hephaestus.Optimisation.NodaTime`: projections and typed variables for the NodaTime types.
