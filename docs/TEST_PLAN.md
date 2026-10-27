# Velo Comprehensive Test Plan & Verification Gate

## 1. Quality Objectives

Every PR and release candidate must pass all **Phase 0 & Phase 1 Exit Gates** before merging or packaging:
1. **100% Byte-for-Byte Correctness**: Deterministic verification using SHA-256 / MD5 hashes against source files.
2. **Resilience to Crash & Network Drop**: Simulated process termination, socket reset, and server 5xx errors must resume without byte corruption or data loss.
3. **Graceful Fallback**: Servers rejecting HTTP Range headers or HEAD requests must safely transition to single-stream sequential download.

---

## 2. Test Fixture Matrix

The integration test suite (`Velo.IntegrationTests`) utilizes an in-memory / local Kestrel HTTP mock server covering:

| Fixture ID | Scenario Description | Expected Engine Behavior |
| :--- | :--- | :--- |
| **FIX-01** | Empty File (0 bytes) | Completed cleanly, 0-byte file created. |
| **FIX-02** | 1-Byte File | Completed cleanly, no chunk math errors. |
| **FIX-03** | 1 KB Small File | Single chunk assigned, completed immediately. |
| **FIX-04** | 50 MB Standard File with Range Support | Split into 4 parallel chunks, 206 Partial Content, exact SHA-256 match. |
| **FIX-05** | HEAD = 405 Method Not Allowed | Fallback to `GET Range: bytes=0-0` probe. |
| **FIX-06** | Chunked / Unknown `Content-Length` | Single continuous stream, sequential write until socket EOF. |
| **FIX-07** | Range Ignored (Server returns 200 OK) | Reject chunk write; restart as single continuous sequential file. |
| **FIX-08** | Dynamic / Expiring URL Redirects | Follow redirects up to 5 hops; update effective URL. |
| **FIX-09** | Transient 503 / 429 with `Retry-After` | Exponential backoff respecting server `Retry-After` duration. |
| **FIX-10** | Abrupt Socket Disconnect mid-chunk | Detect read timeout, reconnect with `Range: bytes={nextByte}-{endByte}`. |
| **FIX-11** | ETag / Content-Length mismatch on Resume | Detect stale resource, invalidate partial chunks, restart fresh. |
| **FIX-12** | Disk Full / Insufficient Space | Detect preflight error, set state to `Failed` with `InsufficientDiskSpace` code. |
| **FIX-13** | Process Terminated (Crash Simulation) | Reload SQLite state on boot, re-verify chunk offsets, resume safely. |

---

## 3. Test Suites

### 3.1 Unit Tests (`Velo.UnitTests`)
- **`SafePathValidatorTests`**: Path traversal prevention, invalid Windows character stripping, DOS device name sanitization.
- **`ByteRangePlannerTests`**: Arithmetic chunk division, boundary conditions, remainder byte allocation.
- **`TokenBucketThrottlerTests`**: Accurate rate limiting under variable concurrency.
- **`StateTransitionTests`**: Enforcing valid state transitions (e.g. `Created -> Probing -> Queued -> Downloading -> Completed`).

### 3.2 Integration Tests (`Velo.IntegrationTests`)
- **`HttpProbeIntegrationTests`**: End-to-end probing against local HTTP test server.
- **`SegmentedDownloadTests`**: Multi-threaded range execution and byte assembly verification.
- **`SQLitePersistenceTests`**: Database migrations, transactional chunk checkpoint commits, crash recovery.

### 3.3 End-to-End Tests (`Velo.EndToEndTests`)
- **`UiViewModelTests`**: Reactive commands, queue state updates, pause/resume orchestrator feedback.
