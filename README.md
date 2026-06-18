# Digital-Twin

Digital-Twin is a .NET 9 solution for running and evolving simulation-based digital twin scenarios.
It currently contains two main subsystems:

- **Simulation**: interactive scenario execution engine (console-driven flow)
- **Data-Storage**: storage-facing web API scaffold used by simulation-facing components

The repository is organized to keep simulation logic and data/storage concerns separated while still allowing direct integration through project references.

## Project Goals

- Provide a flexible simulation runner for physics and what-if scenarios
- Support test-case driven execution using JSONC definitions
- Keep persistence and repository concerns isolated in a dedicated subsystem
- Enable external visualization integration (currently Godot HTTP endpoint)

## Repository Structure

At a high level, the solution contains:

- `Systems/Simulation`: simulation runtime and test assets
- `Systems/Data-Storage`: data/storage subsystem
- `Simulation.slnx`: solution entry point including both projects

### Subsystem 1: Simulation

The Simulation subsystem is the executable orchestration layer for test cases.

Main characteristics:

- Starts as an interactive console app (i.e. using the terminal or debug console)
- Loads available test cases from `TestFiles/TestCases.jsonc`
- Lets you choose configuration setup folders under `TestFiles/CompositeSetups`
- Runs simulation loops using internal services and component managers
- Optionally pushes updates to an external visualization endpoint

Important folders inside `Systems/Simulation`:

- `src/components`: core behavior (entity, state, service, transform, interaction)
- `src/interfaces`: subsystem contracts
- `TestFiles/Cases`: scenario test cases (`*.jsonc`)
- `TestFiles/CompositeSetups`: setup variants used by inner simulation services
- `Program.cs`: interactive run entrypoint

### Subsystem 2: Data-Storage

The Data-Storage subsystem is a web API project intended to own repository/persistence-facing responsibilities.

Current status:

- ASP.NET Core Web API scaffold
- Referenced by the Simulation project to support shared runtime composition
- Made to relfect a data-lake layer, which stores all instance data of a running system

Important folders inside `Systems/Data-Storage`:

- `src/Components`: storage-oriented components (repository, mapper, state repository)
- `src/Interfaces`: storage contracts
- `Program.cs`: web host entrypoint

## Prerequisites

- .NET SDK 9.0

To verify installation:

```powershell
dotnet --version
```

## Build and Run

From the repository root:

### 1. Restore and build

```powershell
dotnet restore .\Simulation.slnx
dotnet build .\Simulation.slnx
```

### 2. Run Simulation subsystem

```powershell
dotnet run --project .\Systems\Simulation\Simulation.csproj
```

When running, the app will:

1. List available test cases
2. Ask you to choose a test case
3. Ask for an optional setup override --> Default is best choice
4. Ask whether to use external visualization (Godot) --> This is a separate project which is not included
5. Ask how often to print simulation state --> To reduce IO load, which might throttle execution
6. Execute the selected simulation flow

### 3. Run Data-Storage subsystem

```powershell
dotnet run --project .\Systems\Data-Storage\Data-Storage.csproj
```

By default in development, launch profiles expose local HTTP/HTTPS endpoints.

## Visualization Integration (Optional)

Simulation can send entity updates to an external visualization server.

Default endpoint behavior:

- update endpoint: `http://127.0.0.1:8080/balls/update`
- clear endpoint: `/balls/clear`
- remove endpoint: `/balls/remove`

If the visualization server is unavailable, simulation continues with a warning.
Visualization was hardcoded to match specific test scenarios, as different scenarios required different configuration for optimized visualization experience. Since visualization was not the main scope, this has been left out.

## Test Asset Model

Simulation scenarios are data-driven:

- Test case index: `Systems/Simulation/TestFiles/TestCases.jsonc`
- Individual cases: `Systems/Simulation/TestFiles/Cases/*.jsonc`
- Entity templates: `Systems/Simulation/TestFiles/EntityTemplates.jsonc`
- Setup variants: `Systems/Simulation/TestFiles/CompositeSetups/*`

This structure allows you to add and evolve scenarios without changing runtime code for each case.

## Current Development Snapshot

- Solution targets .NET 9 (`net9.0`)
- Simulation is the primary operational subsystem
- Data-Storage is scaffolded and ready for continued API/storage expansion
