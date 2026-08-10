# Session 3 Implementation — Long-running work, background workers, idempotency, and SignalR

This document explains the changes I added to implement Module 7 Session 3 (Exercises 5 & 6) in this repository. It lists new files, modified files, responsibilities, interactions, and the rationale for important design decisions.

---

## Summary of what I implemented

- Background processing for transcript generation using a bounded in-memory channel and a hosted `TranscriptWorker`.
- A status store with a small state machine (`Queued -> Processing -> Ready | Failed`) implemented in-memory for the lab.
- Idempotency-key support so repeated POSTs with the same `Idempotency-Key` return the same `reportId` and do not enqueue duplicate work.
- A `TranscriptsController` that enqueues requests and exposes a status endpoint returning truth about progress.
- SignalR integration: a typed hub `TmsHub`, a `ITmsHubClient` interface, and a `SignalRTranscriptNotificationService` used by the worker to notify the client group when a transcript is ready.
- Wiring in `Program.cs` (service registrations, channel, hosted service, SignalR mapping).

These changes follow the layered (Clean) architecture in the repo: application contracts live in `TmsApi.Application`, the background worker and status store live in `TmsApi.Infrastructure`, and SignalR + API wiring live in `TmsApi.Api`.

---

## Files Added

- `TmsApi.Application/Transcripts/TranscriptModels.cs`
  - `TranscriptRequest` record — request payload with helper `WithReportId`.
  - `TranscriptStatus` record — contains `ReportId`, `StudentId`, `State`, timestamps, `DownloadUrl`, `ErrorMessage`.
  - `TranscriptState` enum.

- `TmsApi.Application/Hubs/ITmsHubClient.cs`
  - Strongly-typed client contract for SignalR (`ReceiveTranscriptReady`, etc.).

- `TmsApi.Application/Notifications/ITranscriptNotificationService.cs`
  - Notification abstraction the worker calls; implementation lives in the Api layer.

- `TmsApi.Infrastructure/Transcripts/ITranscriptStatusStore.cs`
  - Interface for status store and idempotency lookups.

- `TmsApi.Infrastructure/Transcripts/InMemoryTranscriptStatusStore.cs`
  - In-memory `ConcurrentDictionary` implementation with safe transitions.
  - Methods: `CreateAsync`, `MarkProcessingAsync`, `MarkReadyAsync`, `MarkFailedAsync`, `GetAsync`, `GetReportIdForIdempotencyKeyAsync`, `LinkIdempotencyKeyAsync`.

- `TmsApi.Infrastructure/Workers/TranscriptWorker.cs`
  - `BackgroundService` that reads `TranscriptRequest` from a `Channel<TranscriptRequest>`.
  - Simulates work with `Task.Delay`, updates the status store, and calls `ITranscriptNotificationService.NotifyTranscriptReadyAsync` when ready.

- `TmsApi.Api/Hubs/TmsHub.cs`
  - Typed hub `TmsHub : Hub<ITmsHubClient>` with `OnConnectedAsync` auto-join by `studentId` query param.
  - `GroupNames` helper centralizes group naming.

- `TmsApi.Api/Notifications/SignalRTranscriptNotificationService.cs`
  - Implementation of `ITranscriptNotificationService` that uses `IHubContext<TmsHub, ITmsHubClient>` to send to student groups.

- `SESSION3_IMPLEMENTATION.md` (this document).

## Files Modified

- `TmsApi.Api/Controllers/V2/TranscriptsController.cs`
  - Replaced the stub controller with a full implementation that:
    - Accepts `TranscriptRequest` body and optional `Idempotency-Key` header.
    - Checks the status store for an existing `reportId` if idempotency key is supplied.
    - Creates a `reportId`, stores a `Queued` `TranscriptStatus`, links the idempotency key, enqueues the request via the channel, responds `202 Accepted` with a Location (status URL) and the queued `TranscriptStatus` in the body.
    - `GET /api/v{version}/transcripts/{id}/status` returns 200 with the `TranscriptStatus` or 404+ProblemDetails.

- `TmsApi.Api/Program.cs`
  - Registered the bounded channel singleton, `InMemoryTranscriptStatusStore`, `TranscriptWorker` hosted service, `SignalR` and the `SignalRTranscriptNotificationService` singleton.
  - Mapped hub endpoint: `app.MapHub<TmsHub>("/hubs/tms");`.

- `TmsApi.Infrastructure/TmsApi.Infrastructure.csproj`
  - Added `Microsoft.Extensions.Hosting.Abstractions` package reference (required for `BackgroundService` in a class library).

---

## How the pieces fit together (runtime flow)

1. Client POSTs to `POST /api/v2/transcripts` with body `{ studentId }` and an optional `Idempotency-Key` header.
2. Controller checks idempotency store: if an existing `reportId` exists, returns 202 with that `reportId` and status (no new job enqueued).
3. Otherwise controller:
   - Generates a short `reportId`.
   - Creates a `TranscriptStatus` with `State = Queued` in the `ITranscriptStatusStore`.
   - Links the idempotency key (if provided) to `reportId`.
   - Writes a `TranscriptRequest` with `ReportId` into the bounded channel.
   - Returns 202 Accepted with a Location header pointing to the status URL and a Retry-After header.
4. The `TranscriptWorker` (hosted service) reads requests from the channel, marks the status `Processing`, performs the work (simulated here with `Task.Delay`), marks the status `Ready` with `DownloadUrl`, and calls `ITranscriptNotificationService.NotifyTranscriptReadyAsync(studentId, reportId, downloadUrl)`.
5. The `SignalR` notification service sends `ReceiveTranscriptReady(reportId, downloadUrl)` to the group `student-{studentId}`.
6. Clients connected to `TmsHub` with `?studentId=<id>` are auto-joined to their student group and receive the notification in real time.

---

## Important implementation notes and design choices

- In-memory store: The `InMemoryTranscriptStatusStore` uses `ConcurrentDictionary` for lab simplicity. It enforces allowed state transitions and retains idempotency key mappings in-memory. This means data is lost on process restart (documented in code and the lab handout). Production options are discussed below.

- Channel: A bounded channel (`BoundedChannelOptions(100)`) keeps memory usage predictable; `FullMode = Wait` makes the controller await capacity when the channel is saturated rather than dropping requests.

- Idempotency: The `Idempotency-Key` header pattern is implemented in the store. The client is responsible for generating the key (UUID convention). Duplicate client retries within the TTL will yield the same `reportId` and avoid duplicate work.

- SignalR and group model: Until user authentication is wired (JWT), the hub auto-joins clients by `studentId` sent in the query string. When authentication lands, swap join logic to use `Context.UserIdentifier` (no controller/hub code changes required beyond that swap).

- Background service and DI: The worker lives in `Infrastructure`. It receives the notification abstraction from `Application` via DI; the concrete `SignalRTranscriptNotificationService` lives in `Api`. That keeps `Infrastructure` free of framework-specific types like `IHubContext`.

---

## Limitations and production considerations

- Durability: the in-memory store and channel are single-process. On restart, in-flight jobs and idempotency keys are lost. Production options:
  1. Store idempotency + status in Redis (TTL for idempotency entries), or SQL table, and persist worker state.

2.  Use a message broker (Azure Service Bus, RabbitMQ, AWS SQS) for inter-process queues and let workers run as separate processes/machines.

- SignalR scale: when running multiple API pods, use a backplane such as Redis (`AddStackExchangeRedis`) or Azure SignalR Service (`AddAzureSignalR`) so `IHubContext` calls are forwarded across instances.

- Security: swap the query-string `studentId` join to `Context.UserIdentifier` and secure hub with `[Authorize]` and a JWT-based `IUserIdProvider` in production.

---

## How to run / smoke test locally

From the repo root run the API project (the same as existing flow):

```powershell
dotnet build
dotnet run --project TmsApi.Api
```

Then in a separate terminal:

1. Issue a POST (with idempotency key):

```bash
curl -i -X POST https://localhost:5001/api/v2/transcripts \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 11111111-2222-3333-4444-555555555555" \
  -d '{"studentId": 1}'
```

2. Poll status:

```bash
curl https://localhost:5001/api/v2/transcripts/{reportId}/status
```

3. Or test SignalR in the browser console (DevTools) using the snippet in the lab handout, connecting to `/hubs/tms?studentId=1`.

---

## Where I changed code (quick file pointers)

- Transcripts controller: [TmsApi.Api/Controllers/V2/TranscriptsController.cs](TmsApi.Api/Controllers/V2/TranscriptsController.cs)
- Program wiring: [TmsApi.Api/Program.cs](TmsApi.Api/Program.cs)
- Models, hubs, notifications and worker: added under `TmsApi.Application`, `TmsApi.Infrastructure`, and `TmsApi.Api` as described above.

---

If you'd like, I can:

- Add unit tests for `InMemoryTranscriptStatusStore` transitions and idempotency behavior.
- Add an integration test that posts a transcript and verifies the status progression.
- Swap in a Redis-backed status store (requires a running Redis instance).

Tell me which of those you'd like next and I'll add them.
