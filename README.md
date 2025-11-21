# Trade Imports GMR Finder

The GMR Finder consumes events from BTMS, polls GVMS for valid records and emits the matched GMRs for further processing.

The solution includes:
- `src/GmrFinder` – The GMR Finder
- `src/GvmsClient` – A packaged reusable GVMS HTTP Client.
- `src/Domain` – A packaged shared contract for use in consumers.

## Quick Start

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/) and Docker.
2. Copy `.env.example` to `.env` and fill in the secrets.
3. Run via Docker:
   ```bash
   docker compose up --build
   ```
4. Run the tests:
   ```bash
   dotnet test
   ```

### Linting & Formatting

We use CSharpier for linting and formatting. To run:

```bash
dotnet tool restore
dotnet csharpier format .
```

## Licence

The Open Government Licence (OGL) permits reuse of public-sector information with minimal conditions. See `LICENCE` for the full text.
