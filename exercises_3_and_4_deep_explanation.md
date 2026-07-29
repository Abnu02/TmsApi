# Exercise 3 & 4: Deep Technical Explanation

---

## Exercise 3: HybridCache with Observable Stampede Protection

---

### 1. The Real-World Problem Being Solved

Imagine your "Popular Courses" endpoint on a Monday morning. A thousand students are all logging in at the same time and their browsers immediately hit `GET /api/v2/courses`. What happens without caching?

Every one of those 1,000 requests reaches your ASP.NET Core app. Each request independently opens a database connection, runs the exact same SQL query (`SELECT * FROM Courses`), waits for the result, maps the rows to C# objects, and returns the response. That is:

- **1,000 database connections** opened simultaneously
- **1,000 identical SQL queries** running at the exact same moment
- **Database CPU and I/O** spiked to 100%
- **Connection pool exhausted** — new requests start timing out
- **5 seconds of latency** for every user, exactly as the outage report described

The naive "just add caching" instinct introduces a subtler bug called the **Cache Stampede** (also called Thundering Herd):

1. The cache is empty (cold start, or TTL just expired).
2. 1,000 requests arrive within the same millisecond.
3. All 1,000 requests check the cache — all see `null` (empty).
4. All 1,000 requests decide "I'll go fetch it from the database."
5. 1,000 database queries run simultaneously. The database crashes anyway.

The cache didn't help at all. You need **atomic refetching** — a mechanism where only one request goes to the database, and all others wait for that one result.

---

### 2. What is HybridCache?

`HybridCache` is a new ASP.NET Core API (introduced in .NET 9, preview in .NET 8) that sits in the `Microsoft.Extensions.Caching.Hybrid` NuGet package. It is a **two-level cache**:

| Level | Name | Storage | Speed | Scope |
|---|---|---|---|---|
| L1 | In-process | RAM (same process) | Microseconds | Single pod only |
| L2 | Distributed | Redis / SQL / any `IDistributedCache` | Milliseconds | All pods in cluster |

When you ask HybridCache for a value:
1. It checks L1 first (fastest, no network).
2. If L1 misses, it checks L2 (Redis — still fast, cross-pod shared).
3. If both miss, it calls your factory lambda **exactly once** (stampede protection).
4. It stores the result in both L1 and L2 for future requests.

The critical behavior is point 3. If 1,000 threads call `GetOrCreateAsync` with the same key simultaneously, the framework places an internal lock on that key. **Exactly 1 thread runs the factory.** The other 999 wait and receive the cached value the moment it's ready. This is called **atomic refetching**.

---

### 3. Architecture: Why a Decorator Pattern?

The exercise creates `CachedCourseService` as a **decorator** of `ICourseService`. This is a deliberate architectural decision.

**What is a decorator?**
A decorator is a class that implements the same interface as the class it wraps, adds new behavior (caching in this case), and delegates the real work to the wrapped class.

```
Request → ICachedCourseService → (cache miss?) → ICourseService → Database
                ↑                                       ↑
           CachedCourseService                     CourseService
```

**Why not just put caching inside `CourseService` directly?**

1. **Single Responsibility Principle**: `CourseService` should know how to talk to the database. It should not also know about cache keys, TTLs, and tags. Mixing these concerns makes the class harder to test and change.
2. **Testability**: You can unit-test `CourseService` without any caching infrastructure. You can unit-test `CachedCourseService` by mocking `ICourseService`.
3. **Optionality**: You can inject `ICourseService` directly in any consumer that should never use the cache (e.g., an admin endpoint that always needs fresh data) and inject `ICachedCourseService` where caching is appropriate.
4. **Replaceability**: If you later want to replace HybridCache with a different caching library, you only change `CachedCourseService`. Nothing else in your codebase needs to change.

---

### 4. File-by-File Deep Dive

#### 4.1 `CacheKeys.cs` — The Key Strategy Contract

**File:** [CacheKeys.cs](file:///d:/ab/C#/TmsApi/TmsApi.Infrastructure/Caching/CacheKeys.cs)

```csharp
public static class CacheKeys
{
    private const string SchemaVersion = "v2";

    public static string Course(string code) => $"{SchemaVersion}:course:{code}";
    public static string CoursesAll => $"{SchemaVersion}:courses:all";
    public const string CoursesTag = "courses";
}
```

**Why this matters — the Schema Version problem:**

Cache keys look up entries by string. If you cache `CourseDto` as JSON and later add a new property (say `DepartmentName`) to `CourseDto`, the old JSON sitting in Redis doesn't have `DepartmentName`. When your app deserializes it, the field is `null`. Every user sees broken data for up to 10 minutes (your TTL).

The `SchemaVersion = "v2"` prefix is the escape hatch. A cache key `v2:courses:all` stores the v2 shape of your DTO. When you add `DepartmentName` and bump to `SchemaVersion = "v3"`, the new code looks for `v3:courses:all`. That key doesn't exist yet (cache miss), so it fetches from the database and caches the correct v3 shape. The `v2:courses:all` entry is orphaned and will expire naturally on its own TTL.

**Result:** No coordinated cache flush, no deployment runbook, no support tickets. Just bump the version.

**Why the `CoursesTag` constant?**

Tag-based invalidation (explained in section 4.3) requires that the same string is used when writing to the cache (during `GetOrCreateAsync`) and when invalidating (`RemoveByTagAsync`). A hardcoded string in two places is a bug waiting to happen — someone will typo it. A single constant means the compiler catches any mismatch.

---

#### 4.2 `CourseDto.cs` — The Cache-Safe DTO

**File:** [CourseDto.cs](file:///d:/ab/C#/TmsApi/TmsApi.Application/DTOs/CourseDto.cs)

```csharp
public record CourseDto(int Id, string Title, string Code, int MaxCapacity, int EnrollmentCount);
```

**Why a separate DTO and not `CourseResponseDto`?**

Your project already had `CourseResponseDto`. The exercise intentionally uses `CourseDto` because these are conceptually different:

- `CourseResponseDto` is the shape you return from your REST API controllers — it may include HATEOAS links, pagination metadata, and fields specific to the HTTP response.
- `CourseDto` is what the cache stores — a pure, serializable data snapshot with no HTTP concerns.

Using a `record` (instead of a `class`) is also intentional. Records are immutable by default. Once a `CourseDto` is created and placed in the cache, nothing can accidentally mutate it through a shared reference.

---

#### 4.3 `ICachedCourseService.cs` — The Interface Contract

**File:** [ICachedCourseService.cs](file:///d:/ab/C#/TmsApi/TmsApi.Application/Interfaces/ICachedCourseService.cs)

```csharp
public interface ICachedCourseService
{
    Task<CourseDto> GetCourseAsync(string code, CancellationToken ct);
    Task<List<CourseDto>> GetAllCoursesAsync(CancellationToken ct);
    Task InvalidateCourseCacheAsync(CancellationToken ct);
}
```

This interface lives in the `Application` layer (not `Infrastructure`) because the application layer defines *what capabilities exist*, not *how they are implemented*. The controller depends on this interface. It never knows or cares that the implementation uses HybridCache. This is the **Dependency Inversion Principle** in practice.

---

#### 4.4 `NotFoundException.cs` — Domain-Meaningful Errors

**File:** [NotFoundException.cs](file:///d:/ab/C#/TmsApi/TmsApi.Application/Exceptions/NotFoundException.cs)

The exercise's factory throws `NotFoundException` when the database has no matching course. This is important — it means a cache miss that leads to a "not found" in the database is not silently cached as `null`. The exception propagates up to the controller, which returns a proper `404 Not Found` to the client.

---

#### 4.5 `CachedCourseService.cs` — The Heart of the Exercise

**File:** [CachedCourseService.cs](file:///d:/ab/C#/TmsApi/TmsApi.Infrastructure/Services/CachedCourseService.cs)

This is the most important file. Let's go line by line:

**Constructor — Primary constructor injection:**
```csharp
public class CachedCourseService(
    HybridCache cache,
    ICourseService service,
    ILogger<CachedCourseService> logger)
    : ICachedCourseService
```
Three dependencies are injected:
- `HybridCache` — the caching engine
- `ICourseService` — the real database service (the inner decorator)
- `ILogger` — for the hit/miss observability log lines

**The `GetAllCoursesAsync` method — every part explained:**

```csharp
public async Task<List<CourseDto>> GetAllCoursesAsync(CancellationToken ct)
{
    var key = CacheKeys.CoursesAll;      // "v2:courses:all"
    var dbHit = false;                   // ← DECLARED OUTSIDE the factory

    var list = await cache.GetOrCreateAsync(
        key,                             // the cache key to look up
        service,                         // STATE passed into the factory (no closure)
        async (state, token) =>
        {
            dbHit = true;                // ← SET INSIDE the factory (only runs on MISS)
            logger.LogInformation("Cache MISS for {Key} fetching from DB", key);

            var courses = await state.GetAllAsync(token);

            return courses.Select(c => new CourseDto(
                c.Id, c.Title, c.Code,
                c.MaxCapacity, c.Enrollments.Count)).ToList();
        },
        tags: [CacheKeys.CoursesTag],    // tag this entry as "courses"
        cancellationToken: ct);

    if (!dbHit)                          // factory didn't run → we got a cache HIT
        logger.LogInformation("Cache HIT for {Key}", key);

    return list;
}
```

**Why is `dbHit` declared outside the factory and set inside it?**

`HybridCache` has no public `bool IsHit` API. The recommended Microsoft pattern is this flag trick:
- The factory lambda only executes when the cache has no value (a "miss").
- If `GetOrCreateAsync` returns without running the factory, it was a "hit" — the value came from L1 or L2.
- By reading `dbHit` after `GetOrCreateAsync` completes, you know which path was taken.

This gives you the exact observability signal the dashboard team asked for.

**Why is `service` passed as `state` and not captured in a closure?**

This is a performance optimization for hot paths.

In C#, when a lambda "captures" a variable from its outer scope (like `service`), the compiler generates a hidden class (a "closure object") to hold that variable. Every call to `GetAllCoursesAsync` that results in a miss would allocate a new closure object on the heap. At 1,000 requests/minute, that's a lot of short-lived allocations, adding GC pressure.

By passing `service` as the `state` parameter, no closure is created. The lambda only uses the `state` it receives as an argument. `GetOrCreateAsync` is designed exactly for this pattern — the `state` parameter exists specifically to avoid closure allocations in hot paths.

**The `tags` parameter:**

```csharp
tags: [CacheKeys.CoursesTag]
```

This registers the cache entry under the tag `"courses"`. Later, when any course is created, updated, or deleted, you call `RemoveByTagAsync("courses")`. HybridCache finds every entry tagged with `"courses"` and removes them all — regardless of how many individual course keys exist. This is far simpler than maintaining a list of all course-specific keys and deleting them one by one.

---

#### 4.6 `GetAllAsync` added to `ICourseService` and `CourseService`

**File:** [ICourseService.cs](file:///d:/ab/C#/TmsApi/TmsApi.Application/Interfaces/ICourseService.cs)
**File:** [CourseService.cs](file:///d:/ab/C#/TmsApi/TmsApi.Infrastructure/Services/CourseService.cs)

The exercise text assumed a `GetAllAsync` method existed. Your project only had `GetCoursesAsync` (paginated). We added:

```csharp
public async Task<List<Course>> GetAllAsync(CancellationToken ct)
{
    return await db.Courses.Include(c => c.Enrollments).AsNoTracking().ToListAsync(ct);
}
```

**Why `Include(c => c.Enrollments)`?**

The `CourseDto` includes `EnrollmentCount = c.Enrollments.Count`. Without `Include`, EF Core would lazy-load enrollments, causing N+1 queries (one query per course). The `Include` eagerly loads all enrollments in a single JOIN query.

**Why `AsNoTracking()`?**

These entities are being cached — they will never be updated via this DbContext. EF Core's change tracker watches every tracked entity for modifications. For read-only caching scenarios, tracking is pure overhead. `AsNoTracking()` skips the tracker and reduces both memory usage and CPU time.

---

#### 4.7 `Program.cs` — Registration

```csharp
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),       // L2 (Redis) TTL
        LocalCacheExpiration = TimeSpan.FromMinutes(2)  // L1 (in-process) TTL
    };
});

builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();
```

**Why different TTLs for L1 and L2?**

- **L1 (in-process, 2 min):** This cache lives in the RAM of each individual pod. When you deploy a new version, old pods die and L1 is instantly cleared. In a 3-pod cluster, you want L1 to be short so stale data doesn't persist too long in a specific pod's memory.
- **L2 (Redis, 10 min):** This is shared across all pods. It's the primary defense against database load. A longer TTL means more requests are served from Redis, reducing database queries significantly.

---

#### 4.8 `V2/CoursesController.cs` — Integration + Invalidation on Write

**Why is cache invalidation on writes "the part teams forget"?**

The classic production bug: your write path (`PUT /courses`) updates the database but never clears the cache. For the next 10 minutes, every `GET /courses` returns the old data. Users see stale information. Ops can't explain why the database shows the new value but the API doesn't.

The fix: every write path must invalidate.

```csharp
// WRITE: Update the course
course.Title = request.Title;
await context.SaveChangesAsync(ct);

// IMMEDIATELY invalidate so next read is fresh
await cachedService.InvalidateCourseCacheAsync(ct);
```

The log line `"Invalidating cache tag courses"` is your proof in production that this happened. The next `GET` will show `"Cache MISS"` followed by `"Cache HIT"` for all subsequent requests — confirming the write was immediately visible.

---

### 5. The Observable Proof — What the Logs Look Like

**Cold cache — 50 concurrent requests:**
```
Cache MISS for v2:courses:all fetching from DB   ← exactly 1
Cache HIT for v2:courses:all                     ← exactly 49
```
And in EF Core SQL logs: **exactly 1 SELECT statement** for all 50 requests.

**After a write (cache invalidation):**
```
Invalidating cache tag courses
Cache MISS for v2:courses:all fetching from DB   ← 1 (fresh data)
Cache HIT for v2:courses:all                     ← all subsequent
```

**Warm cache — 50 concurrent requests:**
```
Cache HIT for v2:courses:all   ← 50 times, no DB queries at all
```

---

---

## Exercise 4: Tier-Aware Rate Limiting

---

### 1. The Real-World Problem Being Solved

The outage report is clear: one misconfigured script at a training centre sent 100 requests/second to the course search endpoint. This caused 5 seconds of latency for everyone else. This is a **resource starvation** problem — one bad actor consumed all available server capacity.

**Why not just rate limit by IP address?**

Corporate networks, university campuses, and many enterprise training centres route all outbound traffic through a NAT gateway. Thousands of different users share a single public IP address. If you rate limit by IP, you either:
- Set the limit high enough for all users → the bad actor still causes harm
- Set the limit low enough to stop the bad actor → you block all 1,000 legitimate users on the same network

**The correct solution:** Rate limit by **API key** (caller identity), not by IP. Every caller gets their own independent bucket. The bad actor's script can exhaust its own bucket without affecting anyone else.

**Why tiers?**

Not all callers are equal:
- **Anonymous users** (no API key): low limits, they're explorers or bots
- **Free tier** (registered, unpaid): moderate limits, enough for development
- **Paid partners** (integration clients paying for API access): high limits, their business depends on this

Paid customers should never be throttled at the same rate as anonymous bots. Tiers solve this cleanly.

**Why a separate concurrency limiter for transcripts?**

Rate limiting (tokens per time window) answers "how often can you call this?". But for the transcript endpoint, the problem is different: generating a transcript takes 5–15 seconds and is CPU/DB intensive. If 30 people request transcripts simultaneously, 30 heavy jobs run at once and the database connection pool is exhausted.

The question is not "how often?" but "**how many at the same time?**" — that's a **concurrency** problem, not a rate problem.

---

### 2. How Token Bucket Rate Limiting Works

The token bucket algorithm is a classic networking concept. Here's the mental model:

- You have a bucket with a maximum capacity (e.g., `TokenLimit = 10`).
- The bucket starts full.
- Every request consumes 1 token from the bucket.
- Tokens are added back at a fixed rate (`TokensPerPeriod = 5` every `ReplenishmentPeriod = 10s`).
- If the bucket is empty when a request arrives, the request is **rejected with 429**.

**Why token bucket over simpler fixed window?**

Fixed window (e.g., "10 requests per 10 seconds, counter resets every 10 seconds") has a timing problem: if you send 10 requests at second 9 and 10 more at second 11, you've sent 20 requests in 2 seconds while technically staying within both windows. Token bucket smooths this — you can only burst up to the token limit and must wait for replenishment.

**Your three tiers compared:**

| Tier | TokenLimit (burst) | TokensPerPeriod / 10s | Sustained rate |
|---|---|---|---|
| Anonymous | 10 | 5 | 5 req/10s |
| Free | 30 | 10 | 10 req/10s |
| Paid | 200 | 100 | 100 req/10s |

A paid partner can burst 200 requests instantly (useful for batch operations at startup) and sustain 100 requests every 10 seconds. An anonymous caller can only burst 10 and gets 5 more every 10 seconds — enough for normal browsing, not enough for scripted abuse.

---

### 3. File-by-File Deep Dive

#### 3.1 `ApiKeyTier.cs` — The Caller Identity System

**File:** [ApiKeyTier.cs](file:///d:/ab/C#/TmsApi/TmsApi.Api/RateLimiting/ApiKeyTier.cs)

```csharp
public enum ApiKeyTier { Anonymous, Free, Paid }

public static class ApiKeyResolver
{
    private static readonly Dictionary<string, ApiKeyTier> Keys =
        new(StringComparer.Ordinal)
        {
            ["tms-free-demo-001"] = ApiKeyTier.Free,
            ["tms-paid-001"]      = ApiKeyTier.Paid
        };

    public static (string PartitionKey, ApiKeyTier Tier) Resolve(HttpContext ctx)
    {
        var key = ctx.Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrEmpty(key))
            return (ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    ApiKeyTier.Anonymous);

        return Keys.TryGetValue(key, out var tier)
            ? (key, tier)
            : (key, ApiKeyTier.Anonymous);
    }
}
```

**Why `StringComparer.Ordinal`?**

API keys are machine-generated identifiers, not human text. Ordinal comparison is:
- **Faster** — no Unicode normalization or culture rules applied
- **More correct** — `"tms-paid-001"` and `"TMS-PAID-001"` are intentionally different keys
- **Safer** — culture-sensitive comparisons can produce surprising bugs ("İ" vs "I" in Turkish locale)

**What the `Resolve` method returns:**

A tuple of `(PartitionKey, Tier)`. The partition key is:
- The **API key string itself** if one is provided — this means every unique API key gets its own isolated bucket
- The **client's IP address** if no key is provided — anonymous users are partitioned by IP (imperfect but better than nothing)
- The string `"anonymous"` as a fallback if the IP is also unavailable

**Why return both partition key and tier?**

The rate limiter needs both pieces of information:
- `PartitionKey` → determines *which bucket* to use (isolation between callers)
- `Tier` → determines *what limits* to apply to that bucket

A paid customer with key `"tms-paid-001"` gets their own bucket with paid-tier limits. A free customer with key `"tms-free-demo-001"` gets their own separate bucket with free-tier limits.

**Future-proofing note (from the exercise text):**

In Module 12, this lookup table will be replaced with JWT authentication. The `Resolve` method will parse the JWT's `sub` claim and a `tier` claim instead of looking up a hardcoded dictionary. The rate limiter code in `Program.cs` doesn't change at all — only `ApiKeyResolver` changes. That's the value of isolating this logic.

---

#### 3.2 `Program.cs` — The Rate Limiter Registration

**The global partitioned limiter:**

```csharp
options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
{
    var (partitionKey, tier) = ApiKeyResolver.Resolve(httpContext);

    return tier switch
    {
        ApiKeyTier.Paid => RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: $"paid:{partitionKey}", ...),
        ApiKeyTier.Free => RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: $"free:{partitionKey}", ...),
        _ => RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: $"anon:{partitionKey}", ...)
    };
});
```

**Why `GlobalLimiter`?**

`GlobalLimiter` applies to **every single request** that goes through the middleware, automatically. You don't need to add any attributes to your controllers. This is the correct design for a tier-based system — you don't want to remember to add `[EnableRateLimiting]` to every new endpoint.

**Why prefix the partition key with `"paid:"`, `"free:"`, `"anon:"`?**

Partition keys are just strings. Without the prefix, two keys could collide: what if a paid customer's API key string happened to match an anonymous client's IP address? Prefixing the tier name into the key guarantees uniqueness across tiers.

**The `OnRejected` handler — why Retry-After matters:**

```csharp
options.OnRejected = async (context, ct) =>
{
    var retryAfter = "10";
    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ts))
        retryAfter = ((int)ts.TotalSeconds).ToString();

    context.HttpContext.Response.Headers.RetryAfter = retryAfter;
    // ... return ProblemDetails JSON ...
};
```

The `Lease` is the result of the rate limiter's acquisition attempt. Token bucket limiters expose `MetadataName.RetryAfter` — the exact time until the next token is available. This is calculated from the actual replenishment schedule, not a guess.

**Why does this matter?**

If you hard-code `Retry-After: 10` (a common shortcut), every client will retry after 10 seconds — even if the next token is available in 2 seconds. That's 8 wasted seconds of delay per rejection. For a script making thousands of requests, this multiplies into minutes of unnecessary waiting.

With the lease metadata approach, the client gets told the exact wait time. A well-behaved client library can back off precisely the right amount. This is especially important for paid partners whose scripts need to self-throttle gracefully.

The response body is a `ProblemDetails` JSON (RFC 7807 standard):
```json
{
  "type": "https://tms.local/errors/rate_limit_exceeded",
  "title": "Rate limit exceeded",
  "status": 429,
  "detail": "Too many requests. Retry after 7 seconds."
}
```

This is parseable by any HTTP client and tells the developer exactly what happened and what to do.

---

#### 3.3 The Concurrency Limiter for Transcripts

```csharp
options.AddConcurrencyLimiter("transcripts", opt =>
{
    opt.PermitLimit = 5;              // max simultaneous transcript jobs
    opt.QueueLimit = 20;              // hold up to 20 more waiting
    opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
});
```

**How it works:**

The concurrency limiter tracks *currently executing* requests, not requests per second:

1. Requests 1–5 arrive: all 5 are permitted. 5 jobs start running.
2. Requests 6–25 arrive while jobs 1–5 are still running: all 20 are queued.
3. Request 26 arrives: queue is full (20). Returns **429 immediately**.
4. When job 1 finishes, request 6 is dequeued and starts running.

**Why `OldestFirst`?**

`OldestFirst` is fair queue behavior. The first person to ask gets served first when capacity opens. The alternative, `NewestFirst`, would be a LIFO stack — unfair to early arrivals who would wait forever while new arrivals jump the queue.

**Why `PermitLimit = 5` and not 30 (one per request)?**

Each transcript job uses significant resources: database connections for reading student records, CPU for PDF generation (Exercise 5), potentially S3/storage writes. Running 30 simultaneously would:
- Exhaust the EF Core connection pool (default: 20 connections)
- Saturate all CPU cores with PDF work
- Cause cascading timeouts across all jobs

5 jobs at a time (with 20 queued) means you're always making forward progress, resources stay within bounds, and most clients eventually get their transcript — they just wait their turn.

**How the two limiters stack:**

When a request hits `POST /api/v2/transcripts`, TWO limiters are evaluated:
1. **Global token bucket** (from `GlobalLimiter`): Is this caller within their rate limit?
2. **Named concurrency limiter** (`[EnableRateLimiting("transcripts")]`): Is there capacity to run another transcript job right now?

Both must pass. A paid caller with a full token bucket but whose 6th simultaneous request hits a full concurrency queue still gets queued. This is correct — the concurrency limit exists to protect server resources, not to punish the caller.

---

#### 3.4 The Named "search" Token Bucket (Step 5)

```csharp
options.AddTokenBucketLimiter("search", opt =>
{
    opt.TokenLimit = 10;
    opt.TokensPerPeriod = 5;
    opt.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
    opt.QueueLimit = 2;
});
```

**Why a separate policy for search?**

The fuzzy course search endpoint hits an external search index (Elasticsearch, Azure Cognitive Search, etc.). External API calls are far more expensive than database queries:
- They go over the network
- They count against your external API rate limits and billing
- They are slower and more variable in latency

A paid partner's global token bucket allows 200 burst requests. If all 200 hit the search endpoint, you've made 200 external API calls and potentially received a bill or a ban from your provider.

The named "search" policy imposes a tighter cap *specifically* on search calls, regardless of the caller's tier. Even a paid customer can only make 10 search calls in a burst. `QueueLimit = 2` allows 2 more to wait — more than that is rejected.

---

#### 3.5 `TranscriptsController.cs` — The Stub Endpoint

**File:** [TranscriptsController.cs](file:///d:/ab/C#/TmsApi/TmsApi.Api/Controllers/V2/TranscriptsController.cs)

```csharp
[HttpPost]
[EnableRateLimiting("transcripts")]
public IActionResult RequestTranscript([FromBody] object? _)
{
    // Stub: Exercise 5 swaps this for enqueue + 202 + Location.
    return Ok(new { message = "Transcript request received." });
}
```

**Why `[EnableRateLimiting("transcripts")]` specifically?**

This attribute tells ASP.NET Core's rate limiting middleware to apply the **named** "transcripts" concurrency limiter to this endpoint. Without it, only the global limiter (token bucket) applies. The concurrency limiter is opt-in via the named policy — you deliberately apply it only where needed.

**The `object? _` parameter:**

The underscore `_` is a discard — a C# convention for "this parameter is required by the signature but not used." The stub accepts any JSON body. Exercise 5 will replace this with a strongly-typed `TranscriptRequest` record.

**Why `return Ok()` in the stub and not `202 Accepted`?**

The exercise text explicitly notes that Exercise 5 upgrades this. The correct pattern for a long-running job is `202 Accepted` with a `Location` header pointing to a status polling endpoint. Using `200 OK` in the stub avoids confusion — it's obviously temporary.

---

#### 3.6 Health Checks — The Overlooked Detail

```csharp
builder.Services.AddHealthChecks();

app.MapHealthChecks("/health/live").DisableRateLimiting();
app.MapHealthChecks("/health/ready").DisableRateLimiting();
```

**Why are health checks a rate limiting concern?**

Kubernetes, Azure Load Balancer, AWS ALB, and nginx all send health probes to your service every few seconds. These come from the load balancer's infrastructure IPs, often anonymously. If your global anonymous limiter allows 10 requests per 10 seconds, and the load balancer sends a probe every 2 seconds, you'll exhaust the anonymous bucket within 20 seconds.

Result: the load balancer receives `429 Too Many Requests` from the health endpoint, concludes the service is unhealthy, and removes the pod from the rotation. You've rate-limited your own service out of existence.

`.DisableRateLimiting()` is a single call that completely bypasses all rate limiters for that specific route. Health check probes are always answered, no matter what load the system is under.

**`/health/live` vs `/health/ready`:**

- **`/health/live`** (Liveness probe): "Is this process alive?" If this fails, Kubernetes restarts the container.
- **`/health/ready`** (Readiness probe): "Is this service ready to handle requests?" If this fails, Kubernetes removes the pod from the load balancer until it recovers (e.g., while database migrations are running at startup).

Both need rate limiting disabled because they serve fundamentally different purposes than API endpoints.

---

### 4. The Middleware Pipeline Order — Why Order Matters

```csharp
app.UseRouting();
app.UseRateLimiter();      // ← MUST be after UseRouting
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

**Why must `UseRateLimiter()` come after `UseRouting()`?**

The rate limiter needs to know *which endpoint* is being accessed so it can apply endpoint-specific policies (like `[EnableRateLimiting("transcripts")]`). `UseRouting()` is what figures out which endpoint a request maps to. Without routing having run first, the rate limiter can't look up endpoint metadata.

**Why before `MapControllers()`?**

The controller code should only execute if the request is allowed through. Putting rate limiting after `MapControllers()` would mean the controller runs first, does work, and then the limit check rejects it — wasting the work done.

**Why before `UseAuthentication()`?**

This is a deliberate design choice. Rejecting overloaded anonymous requests before authentication means you don't spend CPU on token validation for requests you're going to reject anyway. It saves processing time on the most likely source of abuse (unauthenticated requests at scale).

---

### 5. Testing the Three Layers (The Proof)

**Test 1 — Anonymous tier exhaustion:**
```powershell
1..15 | ForEach-Object {
    $r = Invoke-WebRequest https://localhost:5001/api/v2/courses -SkipHttpErrorCheck
    "$_ : $($r.StatusCode) Retry-After=$($r.Headers.'Retry-After')"
}
```
Expected: requests 1–10 return `200 OK`. Requests 11–15 return `429 Too Many Requests` with a `Retry-After` header showing the real wait time (not hardcoded).

**Test 2 — Paid tier sails through:**
```powershell
1..15 | ForEach-Object {
    $r = Invoke-WebRequest https://localhost:5001/api/v2/courses `
         -Headers @{ "X-Api-Key" = "tms-paid-001" } -SkipHttpErrorCheck
    "$_ : $($r.StatusCode)"
}
```
Expected: all 15 return `200 OK`. The paid bucket has `TokenLimit = 200` — 15 requests don't even dent it.

**Test 3 — Transcript concurrency limiter:**
```powershell
1..30 | ForEach-Object -Parallel {
    Invoke-WebRequest https://localhost:5001/api/v2/transcripts `
        -Method POST -Body '{"studentId":1}' `
        -ContentType 'application/json' -SkipHttpErrorCheck | Out-Null
} -ThrottleLimit 30
```
Expected: 5 requests run simultaneously, 20 queue, the 26th+ get `429`. Because the stub `return Ok()` responds instantly, in practice all 25 (5 + 20 queued) succeed very quickly. When Exercise 5 replaces the stub with a real slow job, the queue behavior becomes visible.

---

### 6. The Session Integration Challenge — Cache + Rate Limiter Together

This is the most powerful test because it proves both systems cooperate:

1. **Anonymous burst** (cold cache): first 10 succeed, next 5 get 429. The anonymous caller ran out of tokens before the cache could warm up. This is by design — anonymous callers have tight limits.

2. **Paid burst** (cold cache): all 15 succeed. One of these 15 requests caused a `"Cache MISS"` and populated the cache. Now `v2:courses:all` is warm in both L1 and L2.

3. **Anonymous burst again** (warm cache): The anonymous caller's token bucket has partially refilled. When requests do succeed, they all return `"Cache HIT"` — the cache that the paid caller warmed is now serving the anonymous callers. **Zero additional database queries.**

This is the real proof: the cache isn't just a synthetic benchmark. It's actively reducing database load for all callers, including those being rate-limited. The combination of the two systems means even a misconfigured script can't cause database load — it either hits the rate limit or gets a cache hit.

---

### 7. Summary Table — What Each File Does

| File | Layer | Purpose |
|---|---|---|
| [CacheKeys.cs](file:///d:/ab/C#/TmsApi/TmsApi.Infrastructure/Caching/CacheKeys.cs) | Infrastructure | Defines versioned cache keys and the shared tag constant |
| [CourseDto.cs](file:///d:/ab/C#/TmsApi/TmsApi.Application/DTOs/CourseDto.cs) | Application | Immutable, cache-safe DTO record |
| [ICachedCourseService.cs](file:///d:/ab/C#/TmsApi/TmsApi.Application/Interfaces/ICachedCourseService.cs) | Application | Interface defining the cache contract |
| [NotFoundException.cs](file:///d:/ab/C#/TmsApi/TmsApi.Application/Exceptions/NotFoundException.cs) | Application | Domain exception for missing resources |
| [CachedCourseService.cs](file:///d:/ab/C#/TmsApi/TmsApi.Infrastructure/Services/CachedCourseService.cs) | Infrastructure | Decorator: stampede protection + hit/miss logging + tag invalidation |
| [ICourseService.cs](file:///d:/ab/C#/TmsApi/TmsApi.Application/Interfaces/ICourseService.cs) | Application | Added `GetAllAsync` method to support cache population |
| [CourseService.cs](file:///d:/ab/C#/TmsApi/TmsApi.Infrastructure/Services/CourseService.cs) | Infrastructure | Implemented `GetAllAsync` with eager-loading `Include` |
| [V2/CoursesController.cs](file:///d:/ab/C#/TmsApi/TmsApi.Api/Controllers/V2/CoursesController.cs) | API | GET uses cached service; PUT triggers cache invalidation |
| [ApiKeyTier.cs](file:///d:/ab/C#/TmsApi/TmsApi.Api/RateLimiting/ApiKeyTier.cs) | API | Resolves caller identity and tier from HTTP headers |
| [TranscriptsController.cs](file:///d:/ab/C#/TmsApi/TmsApi.Api/Controllers/V2/TranscriptsController.cs) | API | Stub endpoint with concurrency limiter attribute |
| [Program.cs](file:///d:/ab/C#/TmsApi/TmsApi.Api/Program.cs) | API | Wires everything: HybridCache, Rate Limiter, Health Checks |
