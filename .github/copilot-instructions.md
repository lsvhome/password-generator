# Copilot Instructions for password-generator

## Project state

This repository currently contains only `README.txt` — no application code has been
committed to this branch yet. Treat this repo as pre-scaffold: your first task in most
sessions will be to create the project structure described below before any build/test
commands become meaningful.

## Intended project

A single-page, installable Progressive Web App built with **.NET Blazor (standalone,
WebAssembly), targeting .NET 10.0**, that generates a per-site password deterministically
from a master password + site hostname (no server-side component).

Functional requirements (from `README.txt`):
- Two inputs: a **master password** (persisted client-side via cookies/localStorage) and a
  **site URL**.
- The site URL must be normalized to **hostname only** — strip protocol, path, and query
  string before use.
- As soon as both the master password and site URL are present, derive and display the
  generated password on the page (no explicit submit step implied).

## Expected layout

Per `README.txt`, the app goes in `src/WebApp` with the solution file(s) at the `src/`
level, e.g.:
```
src/
  PasswordGenerator.sln        # or a WebApp.sln at this level
  WebApp/
    WebApp.csproj
    Program.cs
    Pages/, Layout/, wwwroot/  # standard Blazor standalone WASM PWA structure
```
Use `dotnet new blazorwasm --pwa -o src/WebApp` (or equivalent `dotnet new blazor`
standalone template with PWA support) as the starting scaffold, then wire up the
master-password / hostname / password-generation UI on the home page.

## Build, run, test (once scaffolded)

From `src/`:
- Build: `dotnet build`
- Run locally: `dotnet run --project WebApp`
- Run a single test (once a test project exists, e.g. `WebApp.Tests`):
  `dotnet test WebApp.Tests --filter FullyQualifiedName~<TestName>`

There is no CI/lint configuration in the repo yet — if you add one, document the exact
commands here.

## Commit convention

Commit messages should follow the pattern used to build this project step by step:
```
Result of {step text} {command line called}
```
e.g. `Result of "create new dotnet blazor standalone application" dotnet new blazorwasm --pwa -o src/WebApp`.
Keep this one-commit-per-step style when scaffolding or making structural changes so the
history documents how the app was assembled.
