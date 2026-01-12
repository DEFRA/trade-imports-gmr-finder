# Trade Imports GMR Finder

The GMR Finder consumes events from BTMS, polls GVMS for valid records, and emits the matched GMRs for further
processing.

The solution includes:

- `src/GmrFinder` – The GMR Finder
- `src/GvmsClient` – A packaged reusable GVMS HTTP Client.
- `src/Domain` – A packaged shared contract for use in consumers.
- `tests/GmrFinder.Tests` – Unit tests for the service.
- `tests/GvmsClient.Tests` – Unit tests for the GVMS client.
- `tests/GmrFinder.IntegrationTests` – HTTP-driven integration scenarios.
- `tests/TestFixtures` – Shared fixture payloads for tests.
- `scripts` – Developer scripts and sample payloads.
- `compose` / `compose.yml` – Service and supporting services via Docker.

## Quick Start

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/) and Docker.
2. Copy `.env.example` (repo root) to `.env` and fill in the secrets.
3. Run via Docker
   ```bash
   docker compose up --build
   ```
4. Run the tests:
   ```bash
   dotnet test
   ```

## Local Development

Use one of the following approaches:

### Run the API

Start supporting services:

```bash
docker compose up localstack mongodb
```

Run the API via Docker (`docker compose up gmr-finder`) or via your IDE using the launchSettings.json provided as a
base environment configuration, adding the relevant environment variables from `.env`.

## Configuration

| Environment Variable                       | Purpose                               |
|--------------------------------------------|---------------------------------------|
| `DataEventsQueueConsumer__QueueName`       | SQS queue for incoming BTMS events    |
| `DataEventsQueueConsumer__WaitTimeSeconds` | Optional long-poll wait time          |
| `GvmsApi__BaseUri`                         | GVMS API base URL                     |
| `GvmsApi__ClientId`                        | GVMS API Authentication Client ID     |
| `GvmsApi__ClientSecret`                    | GVMS API Authentication Client Secret |
| `MatchedGmrsProducer__TopicArn`            | SNS topic for emitting matched GMRs   |
| `Mongo__DatabaseUri`                       | Mongo connection string               |
| `Mongo__DatabaseName`                      | Mongo database name                   |
| `ScheduledJobs__poll_gvms_by_mrn__Cron`    | Polling schedule (Cron Format)        |

### Feature Flags

| Environment Variable    | Purpose                                         |
|-------------------------|-------------------------------------------------|
| `ENABLE_SQS_CONSUMER`   | Enables or disables the SQS queue consumer      |
| `ENABLE_DEV_ENDPOINTS`  | Enables development endpoints                   |
| `DEV_ENDPOINT_USERNAME` | Basic authentication username for dev endpoints |
| `DEV_ENDPOINT_PASSWORD` | Basic authentication password for dev endpoints |

## Testing

- Unit tests: `dotnet test tests/GmrFinder.Tests` and `dotnet test tests/GvmsClient.Tests`.
- Integration tests: `dotnet test tests/GmrFinder.IntegrationTests`.

### Linting & Formatting

We use CSharpier for linting and formatting. To run:

```bash
dotnet tool restore
dotnet csharpier format .
```

## License

The Open Government Licence (OGL) permits reuse of public-sector information with minimal conditions. See `LICENSE` for
the full text.
