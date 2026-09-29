# Clean

Understand your storage. Clean what matters.

Windows desktop app that analyzes what fills a drive, explains it, and only cleans what has been proven safe.

## Stack

- C# / .NET 8
- WinUI 3 (Windows App SDK 2.5, unpackaged)
- xUnit

## Solution layout

```
Clean.sln
src/
  Clean.App             WinUI 3 user interface
  Clean.Core            Models, interfaces and business rules, no Windows dependency
  Clean.Infrastructure  Windows and file system access
tests/
  Clean.Tests           Unit tests
```

## Build

Requires the .NET 8 SDK and the Windows App Runtime 2.5 (x64).

```
dotnet restore
dotnet build
dotnet test
```

Run the app:

```
dotnet run --project src/Clean.App
```
