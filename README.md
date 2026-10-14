# Claims API

Backend for the insurance claims handling task. Built with .NET 10.

- **MongoDB** stores claims and covers
- **SQL Server** stores the audit log
- The original task description is in [docs/README.md](docs/README.md)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/), running
- Optional: Visual Studio 2026 / Rider

## Get the code

```bash
git clone <repository-url>
cd backend-coding-task
```

## Run it

### Option 1: Visual Studio or `dotnet run`

Open `Claims.slnx`, make sure `Claims.Api` is the startup project and press F5.

Or from the command line:

```bash
dotnet run --project src/Claims.Api
```

In Development the app starts MongoDB and SQL Server for you with Testcontainers, so there is nothing to configure.
The first start takes a minute or two while Docker pulls the images.

- Scalar: https://localhost:7052/scalar (or http://localhost:5052/scalar)
- Swagger UI: https://localhost:7052/swagger

If Visual Studio asks to trust the ASP.NET Core development certificate, say yes (it's only for localhost), or use the http URL.

### Option 2: Docker Compose

Runs the API, MongoDB and SQL Server in containers:

```bash
docker compose up --build
```

- Swagger UI: http://localhost:8080/swagger
- Scalar: http://localhost:8080/scalar
- Traces and logs (Aspire dashboard): http://localhost:18888

Stop with `docker compose down`.

## Try the API

The easiest way is Swagger or Scalar ("Try it out").
There is also [src/Claims.Api/Claims.Api.http](src/Claims.Api/Claims.Api.http) - open it in Visual Studio and run the requests from top to bottom.
It creates a cover, a claim, reads and lists them, deletes them, and has two requests that show the validation errors.

A claim needs an existing cover, so create the cover first:

`POST /api/v1/covers`
```json
{
  "startDate": "2026-11-01",
  "endDate": "2027-04-30",
  "type": "Yacht"
}
```

Take the `id` from the response and create a claim:

`POST /api/v1/claims`
```json
{
  "coverId": "<cover id>",
  "name": "Hull damage",
  "type": "Collision",
  "damageCost": 25000,
  "created": "2026-12-10"
}
```

| Method | Endpoint | |
|---|---|---|
| GET | `/api/v1/claims?page=1&pageSize=20` | list claims |
| GET | `/api/v1/claims/{id}` | get a claim |
| POST | `/api/v1/claims` | create a claim |
| DELETE | `/api/v1/claims/{id}` | delete a claim |
| GET | `/api/v1/covers?page=1&pageSize=20` | list covers |
| GET | `/api/v1/covers/{id}` | get a cover |
| POST | `/api/v1/covers` | create a cover (premium is calculated) |
| DELETE | `/api/v1/covers/{id}` | delete a cover |
| GET | `/api/v1/covers/premium?startDate=&endDate=&coverType=` | calculate a premium without saving |
| GET | `/health/ready` | health of MongoDB, SQL Server and the audit queue |

Claim types: `Collision`, `Grounding`, `BadWeather`, `Fire`.
Cover types: `Yacht`, `PassengerShip`, `ContainerShip`, `BulkCarrier`, `Tanker`.

Errors are returned as `application/problem+json`. Validation errors list every broken rule per field, plus an `errorCodes` array.

## Run the tests

Docker must be running, because the integration tests start MongoDB and SQL Server containers.

```bash
dotnet test Claims.slnx
```

- `tests/Claims.UnitTests` - business rules, premium calculation, handlers. No Docker needed.
- `tests/Claims.ArchitectureTests` - checks the layers don't reference each other the wrong way. No Docker needed.
- `tests/Claims.IntegrationTests` - the real API against real databases, including the audit log.

## What was done

**Task 1 - structure.** The code is split into `Domain`, `Application`, `Infrastructure` and `Api` projects.
Controllers only translate HTTP into commands/queries; each use case has its own handler.
Logging, validation and saving happen in a small request pipeline instead of in every handler.

**Task 2 - validation.** The rules live in the domain classes (`Claim`, `Cover`, `DamageCost`, `InsurancePeriod`):
damage cost max 100.000, claim date inside the cover period, cover can't start in the past, cover max one year.
All broken rules are returned together with a `400`.

**Task 3 - auditing.** Creating or deleting a claim/cover saves the change and an "outbox" message in the same MongoDB transaction and returns right away.
A background worker then writes the audit rows to SQL Server, with retries. Nothing is lost if the audit DB is down, and the request never waits for it.
The audit tables keep their original structure; one migration adds an `EventId` column so the same event is never written twice.
Azure Service Bus can be switched on with `Messaging:Transport = AzureServiceBus`.

**Task 4 - tests.** Unit, architecture and integration tests (see above). The original test is still there and now checks the response body too.

**Task 5 - premium.** The old loop charged the first 30 days three times and days 31-180 twice, and applied the last discount to the wrong days.
The calculation is now done per discount band in `PremiumCalculator`, with the rates in `StandardPremiumTariff`, and tested against every band boundary.

## Configuration

| Setting | Description |
|---|---|
| `ConnectionStrings:ClaimsDatabase` | MongoDB. Needs a replica set (e.g. `?replicaSet=rs0`) because of transactions |
| `ConnectionStrings:AuditDatabase` | SQL Server |
| `Messaging:Transport` | `InProcess` (default) or `AzureServiceBus` |
| `LocalContainers:Enabled` | start databases with Testcontainers when no connection strings are set (on in Development) |

## Project structure

```
src/
  Claims.Domain           entities, business rules, premium calculation
  Claims.Application      commands, queries, handlers, validation
  Claims.Infrastructure   MongoDB, SQL Server audit, outbox worker, Service Bus
  Claims.Api              controllers, Swagger/Scalar, startup
tests/
  Claims.UnitTests
  Claims.ArchitectureTests
  Claims.IntegrationTests
```
