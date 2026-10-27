# VELO
## Product Requirements Document (PRD) · Revision 2.0

**Product name / code:** Velo  
**Document status:** Final build-ready specification, Windows 10 x64 only  
**Prepared:** 27 October 2026  
**PRD revision:** 2.0 (complete Velo rebrand and exclusive Windows 10 x64 scope)  
**Only supported OS:** Windows 10 version 22H2, 64-bit (x86-64 / AMD64). Windows 11, Windows 10 x86/ARM, Windows 8.1, macOS and Linux are explicitly unsupported. No multi-platform deliverables.  
**Project workspace:** `F:\Velo\` (intended local path, not modified)  
**Public source repository:** https://github.com/ahmed-el4enawy/Velo  
**Repository branch:** `main` (verified)  
**Distribution artifact:** `Velo-win10-x64-v{version}-setup.exe` and optional `Velo-win10-x64-v{version}-portable.zip`  
**Pricing:** $0 to build, run, and distribute the core software using the selected stack  
**Release model:** Free desktop software; no paid tiers, ads, tracking, or mandatory accounts

> **Product vision:** A trustworthy, fast, polished download manager that makes large file downloads efficient, controllable, recoverable, and understandable—without subscriptions or proprietary runtime costs.

---

# 1. Executive decision

**Recommended build:** **C# / .NET 10 LTS + Avalonia UI + an in-house segmented HTTP engine + SQLite**. Use a genuinely free **NSIS** installer, and develop browser support as a separate WebExtension connected through native messaging. Add `yt-dlp` / `FFmpeg` as *optional* user-enabled integrations only after the core engine is stable.

**Why this stack:** The developer already works with C#, the runtime is supported through November 2028, asynchronous HTTP and file I/O are first-class, Avalonia's core framework is MIT-licensed, and no commercial Visual Studio edition, paid Avalonia tools, Rust learning curve, or paid API is needed. Deliver an installable, self-contained application, not just a design mock-up.

**Important price boundary:** The *core app* can have a $0 software/tooling/license budget. Optional publication in the Chrome Web Store requires a one-time developer registration fee, and trusted commercial Windows code-signing may cost money. Neither is necessary to build, use, or directly distribute Velo. Edge Add-ons developer registration currently has no fee. Public downloads on unsigned Windows builds may trigger reputation warnings.

# 1A. Binding platform and repository policy

**NON-NEGOTIABLE RELEASE CONTRACT — Windows 10 x64 only.** Velo will be developed, documented, verified, packaged and released exclusively for **Windows 10 version 22H2 x64 (AMD64 / x86-64)**. Neither Windows 11 nor any other OS edition/architecture is an accepted release target. Although some binaries could run on other systems by coincidence, such execution must never be claimed as supported. Do not produce `win-x86`, `win-arm64`, `linux-*`, `osx-*`, or Windows 11 release artifacts.

**Windows 10 security lifecycle:** Standard Windows 10 support ended on **14 October 2025**. Velo must not imply Microsoft still provides standard Windows 10 security updates. Development and testing should use Windows 10 systems receiving applicable extended security coverage, and the release documentation must transparently explain OS lifecycle risks. The app can continue to be built for Windows 10; this is distinct from OS vendor support. Revalidate minimum .NET/Avalonia/installer compatibility on real Windows 10 22H2 x64 hardware and pin last-compatible versions if dependencies change their support matrices.

**Repository source of truth:** `https://github.com/ahmed-el4enawy/Velo` (`main` as the baseline branch). Repository verified public and initially empty on 27 October 2026. This PRD does **not** claim source files or workflows already exist, and does **not** commit or push any files. Suggested canonical repo document path: `docs/PRD.md`; README should summarize scope and point to this document. Adopt `Velo` branding consistently in namespaces, app user-data folders, installer registry entries, log prefixes, CLI identifiers and release artifact filenames.

**Build contract:** Use Windows 10 x64 as the QA/release runtime. Publish self-contained `win-x64` builds with explicit x64 architecture, avoiding `AnyCPU`/x86 packaging surprises. Validate the installer architecture and Windows version prior to installation; clean per-user install/uninstall must be tested without admin elevation. Produce SHA-256 release checksums and publish source-linked release notes. No cost-bearing release activity may become mandatory.

**Acceptance cases:** On Windows 10 22H2 x64, clean install, launch, download, resume, uninstall and portable startup all pass. Windows 10 x86 and ARM installations are rejected with clear messages. Other operating systems receive no published binaries or QA guarantee. Windows 11 is not a compatibility acceptance target, even though kernel version detection may require careful differentiation by build number.

---

# 2. Product definition

## 2.1 Problem

Ordinary browser downloads provide limited control over parallel connections, reliable segmented recovery, queue priorities, per-download settings, scheduling, and organized history. Existing solutions may be proprietary, cluttered, platform-bound, or unnecessarily complex.

## 2.2 Target users

- **Everyday user:** Downloads documents, applications, archives and wants clear progress and one-click pause/resume.
- **Power user:** Manages multi-gigabyte files, flaky connections, custom headers, many parallel downloads and schedules.
- **Creator/researcher:** Downloads multiple permitted media or datasets with accessible source URLs and wants consistent naming and categories.

## 2.3 Core value proposition

1. **Reliability first:** Progress survives app crashes, machine restarts and network drops whenever the remote server supports safe resumption.
2. **High throughput:** Parallel ranges can increase utilization for servers that throttle individual connections, subject to actual network/server limits.
3. **Control:** Pause, resume, cancel, prioritize, schedule, throttle, and reattempt individual downloads.
4. **Trust:** Clean installer, minimal permissions, local-only history, no telemetry by default, never silently execute a downloaded file.
5. **Professional experience:** Fast, accessible Fluent-inspired interface with useful details rather than distracting effects.

## 2.4 Explicit non-goals for v1.0

- Guaranteed faster-than-browser speeds or bypassing network/server bandwidth limits.
- Defeating DRM, paywalls, authentication, copyright restrictions, or site technical protections.
- BitTorrent, FTP, SFTP, Metalink, cloud synchronization, remote web dashboard, and peer-to-peer transfer.
- Seamless automatic interception of *every* browser download on *every* website.
- Windows kernel drivers, packet interception, privileged services, or mandatory administrator permissions.
- Accounts, backend servers, paid APIs, subscriptions, ads, or analytics SDKs.

# 3. Scope and priorities

**Priority definitions:** P0 = required for a trustworthy v1.0; P1 = expected after core release / v1.1; P2 = optional long-term enhancement.

| ID | Requirement | Priority | Objective acceptance criterion |
|---|---|---|---|
| FR-01 | Add HTTP/HTTPS download by URL | P0 | Valid direct URL creates download, malformed URL receives clear error |
| FR-02 | Inspect redirect target and remote metadata | P0 | HEAD attempted; safe GET fallback supports servers that reject HEAD |
| FR-03 | Safe segmented range downloading | P0 | Uses 206 + valid Content-Range; never corrupts destination on 200 fallback |
| FR-04 | Sequential fallback | P0 | Works when ranges, content length or reliable validators are unavailable |
| FR-05 | Pause, resume and cancel | P0 | Paused jobs make no new requests; completed bytes persist, when resumable |
| FR-06 | Persistent job/segment state | P0 | App crash/relaunch restores queue state and safely resumes/restarts jobs |
| FR-07 | Adaptive retry with backoff | P0 | Transient failures retry within policy; persistent failure is visible |
| FR-08 | Disk preflight + safe finalization | P0 | Free-space check, temporary file, collision policy, atomic rename where possible |
| FR-09 | Progress, speed, ETA and errors | P0 | Accurate persisted bytes, smoothed speed, ETA only when calculable |
| FR-10 | Queue + per-host/global concurrency | P0 | Configurable active jobs, segment count, and fair per-host ceiling |
| FR-11 | Download history + search/filter | P0 | Completed/failed/paused entries remain after restart and are searchable |
| FR-12 | Modern desktop UI + dark/light | P0 | Keyboard-accessible responsive layout, clear state, system theme supported |
| FR-13 | Installer, uninstall, portable build | P0 | Fresh Windows 10 22H2 x64 machine runs Velo without requiring a separately installed .NET runtime; installer rejects unsupported architectures and OS versions |
| FR-14 | Structured logging + local diagnostics | P0 | Errors carry user-friendly message and technical code; no secret headers in logs |
| FR-15 | Speed limit and queue scheduling | P1 | Throttle respects configured aggregate cap under benchmark conditions |
| FR-16 | Categories and default folders | P1 | Configurable, editable rules; categories never depend on filename alone for safety |
| FR-17 | Clipboard detection | P1 | Explicit opt-in; passive detection does not launch requests automatically |
| FR-18 | Browser extension for Chrome/Edge/Firefox | P1 | Sends deliberate download requests through authenticated/allowlisted native host |
| FR-19 | Download handoff UX | P1 | Clear option to pass supported browser links; no promise of universal interception |
| FR-20 | Optional media URL handling | P2 | Permitted non-DRM media via externally managed yt-dlp / FFmpeg components |
| FR-21 | Automatic updates | P2 | Manifest authenticity and asset integrity verified before user-approved update |
| FR-22 | Localization | P2 | All strings externalized; English first, Arabic RTL only after UI testing |

# 4. User experiences and screen requirements

## 4.1 Primary navigation

**Left rail:** All Downloads, Downloading, Queued, Paused, Completed, Failed, Categories, History, Settings.  
**Top action bar:** Add URL, Paste, Start/Resume, Pause, Cancel, Search, Add to Queue.  
**Download table/cards:** Name, source host, total size, progress, current speed, estimated time, status, priority, action menu.  
**Details panel:** Sanitized source URL, target folder, HTTP response, range support, segment count, retries, start/end timestamps, checksum state, error details.

## 4.2 Essential flows

**Add download:** User pastes URL → metadata probe → sanitized suggested filename, save folder, overwrite policy and concurrency shown → Start or Queue → live progress → successful final rename → optional notification.

**Pause / resume:** User pauses → worker cancellation requested and file buffers flushed → segment offsets committed → status = Paused. On resume, Velo validates current resource identity/range semantics and continues only when safe; otherwise it prompts to restart.

**Unexpected shutdown:** On startup, database recovery detects Interrupted jobs → re-checks file and resource invariants → offers Resume or Restart with intelligible reason. It never labels an unverified partial download Completed.

**Browser handoff:** Extension receives explicit user action / eligible browser event → warns when authentication context cannot safely be transferred → Native Messaging host authenticates extension origin/validates URL → queues link in desktop app → extension shows success/failure. It never silently reads browser passwords or exports complete cookie databases.

## 4.3 Visual specifications

Use a restrained Fluent-inspired system: dark mode as polished default, light mode, consistent 8px spacing grid, soft 10–14px corner radius, readable typography, subtle animations, meaningful status colors, no heavy glass-blur on large lists. Avoid decorative CPU/RAM dashboards unless they give actionable information. Display speed in **MB/s** or **MiB/s** with explicit settings; show bytes and bits consistently. Respect reduced-motion settings; keyboard focus and screen-reader labels are mandatory.

# 5. Download engine: functional contract

## 5.1 Remote probe

1. Validate and normalize user-supplied HTTP/HTTPS URL; reject dangerous schemes and invalid hostnames. Default to disallowing local/private/link-local destinations when handed off from untrusted extensions; provide explicit local-download override in the desktop UI.
2. Follow bounded redirects; enforce TLS validation and redirect policies. Do not forward auth/cookie headers to a different origin without informed, explicit authorization.
3. Try HEAD to inspect `Content-Length`, `Content-Disposition`, `ETag`, `Last-Modified` and response headers. HEAD is not guaranteed to work.
4. If uncertain, issue `GET` with `Range: bytes=0-0` and `Accept-Encoding: identity`; inspect `206 Partial Content` plus valid `Content-Range` before enabling segmentation.
5. `Accept-Ranges: bytes` is a useful hint **not** a guarantee; absence does not prove ranges are unsupported. A 200 response to a range request must trigger sequential fallback, not be written at a segment offset.
6. If the server returns compressed or transformed content, disable unsafe byte-range arithmetic; require identity representation to map HTTP offsets to disk offsets.

## 5.2 Segmentation and I/O

- Default **4** connections per download; configurable **1–16** (advanced settings may expand only after benchmarks). Adaptive chunk sizing avoids splitting tiny files into many requests.
- Each segment stores inclusive `[start, end]` and `nextByteOffset`; ranges never overlap or leave holes. File writes go to fixed offsets in a *single* partial file using positional/random-access I/O with bounded buffers.
- Limit total live connections across downloads and per remote host; use shared `HttpClient` instances/connection pools, respecting per-host connection limits.
- Prefer medium-size buffered sequential writes per segment rather than flushing every network packet. Persist checkpoints periodically and at pause/shutdown; on recovery, reconcile on-disk checkpoint guarantees to avoid falsely declaring bytes complete.
- Keep partial data in a same-volume staging path ending in `.velo.part` (and metadata in SQLite), enabling safe final move when possible.
- Never infer success only because every worker ended. Confirm expected byte count / coverage, response validity, and optional user-supplied checksum before moving into the final name.

## 5.3 Resource change and safe resume

- Save effective final URL, total length, strong ETag where provided (or Last-Modified as weaker validator), transfer encoding, and segment offsets.
- On restart, use `If-Range` with a suitable validator for partial requests; verify `206`, exact range and entity identity. Re-probe after expiry / redirects / token refresh.
- If identity changed, ranges are ignored, or response is inconsistent, **invalidate stale segments** and request a fresh download. Never append a different version of the file to existing bytes.
- Unknown-length/chunked downloads fall back to one continuous stream; persistence of a sequential partial file does not imply resumability without server support.

## 5.4 Error policy

- Retry timeouts, resets and retryable 5xx with jittered exponential backoff; honor a reasonable `Retry-After` for 429/503.
- Authentication failures (401/403), missing resources (404/410), permanent certificate problems and insufficient disk space stop with actionable messages; no unbounded retries.
- Enforce URL/request timeouts, maximum redirects, bounded memory, global sockets, safe filenames, filename collision policy (`rename`, `overwrite with confirmation`, `skip`).
- Validate size mismatches; free-space failure pauses gracefully; cancellation of a job stops workers and optionally asks whether to keep partial data.

# 6. Queue and bandwidth behavior

| Setting | Initial default | User-adjustable |
|---|---:|---|
| Simultaneous active downloads | 3 | 1–10 |
| Segments per supported download | 4 | 1–16 |
| Total active HTTP connections | 24 | Yes, within safe bounds |
| Connections per host | 8 | Yes, within safe bounds |
| Retries for transient failures | 5 | Yes |
| Aggregate speed limit | Unlimited | P1, bytes/s token bucket |
| Retry backoff | Exponential + jitter | Advanced |
| Scheduling / off-peak queue | Disabled | P1 |

**Fairness:** A large download cannot starve small jobs. Priority changes apply at queue boundaries and rebalance only when doing so will not discard transferred data. UI controls must never block the transfer worker threads.

# 7. Architecture and module boundaries

```text
Avalonia Desktop (Views / ViewModels)
                |
Application Services (commands, state transitions, settings)
                |
+-----------------------+---------------------------+
| Transfer Engine       | Persistence & Files       |
| Probe & validators    | SQLite jobs / segments    |
| Scheduler / throttler | Safe paths & checkpoints  |
| Segment workers       | Partial file finalizer    |
| Retries / integrity   | History & settings        |
+-----------------------+---------------------------+
                |
Optional Integration Boundaries
Browser Native Messaging Host  |  Media Adapter (yt-dlp/FFmpeg)
```

**Application principles:** Dependency inversion for protocol/filesystem/database, testable state machine, no network logic inside view models, versioned database migrations, cancellation passed through all async operations, no UI-thread blocking, strongly typed errors, structured logs without sensitive tokens.

## 7.1 Proposed folder structure

```text
F:\Velo\
├── Velo.sln
├── global.json
├── Directory.Build.props
├── LICENSE
├── README.md
├── THIRD_PARTY_NOTICES.md
├── docs\
│   ├── PRD.md
│   ├── ARCHITECTURE.md
│   ├── SECURITY.md
│   └── TEST_PLAN.md
├── src\
│   ├── Velo.Desktop\              # Avalonia Views / ViewModels
│   ├── Velo.Application\          # use cases / coordination
│   ├── Velo.Domain\               # entities / state machine
│   ├── Velo.DownloadEngine\       # HTTP probe, segments, retry
│   ├── Velo.Infrastructure\       # SQLite, disk, settings, logs
│   ├── Velo.NativeMessaging\      # isolated browser bridge (P1)
│   └── Velo.MediaIntegration\     # optional adapter (P2)
├── extensions\
│   ├── chromium\
│   └── firefox\
├── tests\
│   ├── Velo.UnitTests\
│   ├── Velo.IntegrationTests\
│   └── Velo.EndToEndTests\
├── packaging\
│   └── nsis\
├── .github\
│   └── workflows\
└── scripts\
```

## 7.2 Initial persistence schema

**downloads:** `id`, `original_url`, `effective_url`, `target_path`, `temp_path`, `display_name`, `host`, `expected_bytes`, `downloaded_bytes`, `strong_etag`, `last_modified`, `range_supported`, `state`, `priority`, `created_at`, `updated_at`, `finished_at`, `failure_code`.  
**segments:** `id`, `download_id`, `start_byte`, `end_byte`, `next_byte`, `attempts`, `state`, `last_checkpoint_at`.  
**settings:** keyed validated user preferences; versioned migrations.  
**events:** capped local diagnostic history (no access tokens, cookies, full secret-bearing signed URLs, or raw auth headers).

**State machine:** `Created → Probing → Queued → Downloading → Verifying → Completed`; allowed alternatives: `Paused`, `Retrying`, `Failed`, `Canceled`, `Interrupted`. All transitions must be explicit, validated and persisted.

# 8. Security, legal boundaries and privacy

1. **No forced privilege:** Standard user install; use user-space app data and download folders. Privileged operations only when explicitly necessary and prompted by Windows.
2. **No remote code execution:** Downloaded executables are not automatically opened. Do not use `Process.Start` with untrusted shell commands or unsanitized arguments.
3. **Safe paths:** Sanitize `Content-Disposition` filenames against traversal, absolute paths, reserved device names, invalid characters, path-length limits, symlink surprises, and case-insensitive collisions.
4. **Secrets:** Never log `Authorization`/`Cookie` values or signed-query tokens. Avoid persistent cookie storage; if later essential, use OS-protected secret storage.
5. **Browser bridge:** Native Messaging per browser, `allowed_origins` / extension IDs, source validation, strict request schema and per-request user-consent model. It is preferable to an unauthenticated local HTTP/WebSocket server.
6. **Download URL trust:** Warn for suspicious protocols/hosts, prevent SSRF from extension-originated data, limit redirects to private networks, enforce TLS certificate validation, and do not silently degrade HTTPS to HTTP.
7. **External integrations:** Fetch optional executables only from verified release sources with checkable hashes/signatures; inspect licenses for *actual binary builds*, not just upstream source licenses.
8. **Privacy:** Offline-first; no account, telemetry, tracking pixels, analytics SDK, or mandatory cloud. Local diagnostics can be exported manually with secret redaction.
9. **User rights:** Only fetch media/content that the user is entitled and permitted to download; no DRM circumvention or promise of universal streaming capture.
10. **Dependency hygiene:** Pin package versions, use dependency/security scanning, produce a Software Bill of Materials for releases and ship required license notices.

# 9. Browser integration: realistic specification (P1)

Build an MV3-compatible Chromium extension and a separately tested Firefox extension using shared WebExtensions logic where compatible. Give users a **Download with Velo** context-menu entry, a toolbar button, and a handoff prompt for eligible downloads. Chrome's `downloads` API observes/controls many browser downloads, but universal seamless interception is not guaranteed and cancellation may race with the browser initiating a download. Do **not** promise that the extension can intercept arbitrary streaming media, authenticated resources, protected tabs, blob URLs, or all application downloads.

Use Native Messaging with a dedicated minimal host executable. The installer registers manifests for each browser only when the user enables integration; uninstall unregisters them. Extension requests should contain only needed URL/filename/referrer metadata, never bulk browsing history or credentials. Validate extension identity, URL type, size, redirect target and user interaction; return explicit acknowledgements to the extension.

**Release path keeping a strict $0 budget:** Ship the desktop app as a direct download plus Edge Add-ons submission (currently no registration fee), and provide a clearly documented Chrome extension developer/unpacked workflow for testing. Publishing through the Chrome Web Store requires a one-time developer registration fee; treat it as an optional later distribution expense, not part of the $0 baseline.

# 10. Optional video/media subsystem (P2)

Make media downloading **plug-in-like**, disabled by default and not required for normal HTTP/HTTPS downloads. For openly accessible or otherwise permitted media, allow an explicit user action to pass supported URLs to separately installed `yt-dlp`; use `FFmpeg` for remuxing/merging only when needed. Present available formats and output filename before proceeding.

**Licensing caution:** yt-dlp repository/source is Unlicense, but some distributed standalone executables include GPLv3+ code. FFmpeg is typically LGPL unless GPL-only build options are enabled. Distributing binaries must be reviewed against the exact build and bundled components. Keep optional dependencies separately installable initially and provide their licenses/notices. Never bundle a random executable fetched from a third-party mirror.

# 11. Performance, quality and reliability targets

The targets below are **engineering acceptance thresholds, not promises of achieved performance**. Measure on a documented Windows 10 22H2 x64 machine, current .NET runtime, SSD, a local reproducible HTTP test server and a separately controlled WAN test endpoint.

| Dimension | Acceptance target / measurement |
|---|---|
| Correctness | 100% byte-for-byte equality with source for all deterministic fixture files |
| Segmented safety | Range-denying, incorrect 206, changing ETag and bad Content-Range cases never yield a falsely Completed job |
| Crash recovery | 100 forced-stop/restart cycles across segmented/sequential jobs without accepted corruption |
| Network recovery | Artificial timeouts/disconnects lead to controlled retries with no duplicated bytes |
| Throughput | ≥90% of measured local single-download best-case link throughput when host/disk allow it |
| Speed improvement | Demonstrate benefit only on controlled per-connection-throttled server; no universal multiplier claim |
| UI responsiveness | No frozen UI during I/O; target median command feedback under 100 ms on reference PC |
| Progress display | Refresh 4–10 times/sec; smooth speed over sliding sampling window |
| Resource stability | Bounded worker count and memory; establish quantitative budgets after prototype profiling |
| Installer | Fresh test VM install, launch, download, uninstall; no separate .NET install required |
| Accessibility | Full keyboard navigation, visible focus, usable contrast, reduced-motion support |
| Security | Zero unreviewed high/critical dependency findings in release gate |

**Test fixture set:** Empty file; 1-byte file; 1KB; 5MB; 1GB; >4GB sparse test file; redirect chains; HEAD=405; no length; ignored range=200; valid range=206; malformed Content-Range; 416; gzip transformed body; dropped sockets; 429/503 with Retry-After; expiring signed links; switched content mid-transfer; disk full; read-only target; Unicode/RTL filenames; duplicate filename; proxy; TLS error; slow servers; 1000 queued jobs; app killed mid-write.

# 12. Development and release phases

## Phase 0 — Foundation and proof of correctness

- Repository, solution projects, lint/build/test workflows, package inventory, MIT-licensed core source (if chosen), threat model.
- Create deterministic local HTTP server fixtures with range/no-range/redirect/error modes.
- Deliver a headless CLI proof of concept: metadata probe, one direct transfer, one segmented transfer, equality verification.
- **Exit gate:** all deterministic segmented and fallback fixtures pass.

## Phase 1 — Dependable transfer engine (P0)

- Implement segment map, bounded concurrency, positional disk writes, SQLite persistence and resume validation.
- Add error handling, redirects, safe filenames, retry policy, cancellation and verification.
- **Exit gate:** all forced-restart, invalid-range, response-change and disk-failure integration tests pass.

## Phase 2 — Complete desktop experience (P0)

- Avalonia MVVM shell, downloads views, dialogs, search, status, system themes, settings and diagnostics.
- Add NSIS installer and portable self-contained build, versioning, clean uninstall and release checksums.
- **Exit gate:** clean machine can install and finish/pause/resume/recover a download with no command-line tools.

## Phase 3 — Power features (P1)

- Queue scheduling, fair priorities, global bandwidth cap, category rules, notifications, optional clipboard listener.
- **Exit gate:** no starvation, rate-limit benchmark, persistent queue tests and usability walkthrough.

## Phase 4 — Browser integration (P1)

- Native messaging bridge + Chromium and Firefox extensions, explicit user consent and permission-minimizing UX.
- **Exit gate:** supported handoffs work, unsupported handoffs fail safely, browser policies respected.

## Phase 5 — Optional media, updater and polish (P2)

- Separate yt-dlp/FFmpeg adapter after license review; optional localization; security-reviewed update channel.
- **Exit gate:** every optional feature works with no impact on core user journeys when uninstalled.

# 13. Launch definition and release checklist

A release may be labeled **Velo v1.0** only when all P0 requirements and gates pass:

- [ ] Stable direct HTTP/HTTPS downloads, redirects, no-range fallback and validated segmentation.
- [ ] Durable pause/resume, safe restart after interruption and mismatch detection.
- [ ] Accurate complete/failed/paused statuses with clear diagnostics.
- [ ] Per-download/global connection limits, bounded memory and transparent retries.
- [ ] Clean dark/light GUI, keyboard operation and functional details panel.
- [ ] Safe filename handling, checksum option and non-destructive file replacement policy.
- [ ] SQLite migrations, corrupted-state handling and retained history.
- [ ] Reproducible build, fresh-machine installer, portable package, uninstall.
- [ ] Dependency notice file, license audit, SBOM, signed-source provenance or documented release checksum process.
- [ ] No mandatory network telemetry, account, browser extension or media binary.
- [ ] Automated unit/integration suites green; exploratory bug verification and security checklist complete.

**Explicitly not needed for Velo v1.0:** browser interception, video grabbing, torrent support, automatic updater, user accounts, and cloud services.

# 14. Cost and licensing audit

| Component or activity | $0 baseline? | Scope / caveat |
|---|---|---|
| C# / .NET SDK and runtime | Yes | Open source; choose supported .NET 10 LTS |
| Avalonia UI core | Yes | MIT framework; optional paid professional tooling **not** required |
| CommunityToolkit.Mvvm | Yes | Open-source MVVM toolkit |
| Microsoft.Data.Sqlite / SQLite | Yes | Review transitive package notices |
| Git / GitHub public source and release workflow | Yes | Within provider's free-plan limits and fair-use policies |
| NSIS installer | Yes | zlib/libpng family licenses; review bundled plug-in licenses |
| Browser extension code + local testing | Yes | Use native messaging; unpacked Chrome testing is not public store publication |
| Edge Add-ons registration | Yes | Official Microsoft developer documentation states no registration fee |
| Chrome Web Store publication | **No** | One-time developer registration fee; optional, avoid in $0 path |
| yt-dlp | Conditionally | Source free; standalone executable may include GPLv3+ components |
| FFmpeg | Conditionally | Build-specific LGPL/GPL requirements; optional module |
| Windows commercial code signing | **Not guaranteed** | Optional paid certificate / reputation handling outside $0 baseline |
| Web hosting/domain/CI beyond included quotas | Optional | Direct release assets can avoid a paid domain/hosting account |

**Hard rule:** Do not add a dependency or distribution channel to the required build until its actual license, build artifact, transitive dependencies and recurring costs have been checked. "Free download" alone is not proof of open-source or zero-cost redistribution.

# 15. Product risks and mitigations

| Risk | Severity | Mitigation |
|---|---|---|
| Server ignores ranges or changes content | Critical | Strict 206/Content-Range/ETag validation, safe sequential restart |
| Wrong checkpoint leads to corrupted resume | Critical | Consistent offset commits, recovery reconciliation, fixture-based forced-stop tests |
| Excessive connections trigger bans or slower downloads | High | Conservative defaults, per-host limits, rate policy, adaptive tuning |
| Extension leaks secrets or downloads localhost targets | High | Explicit permissions, native-origin validation, restricted hosts, no cookie harvesting |
| Misleading performance claims | Medium | Benchmarks versus real browser under identical conditions |
| Installer or third-party binary license conflict | High | SBOM, curated dependency policy, GPL/LGPL exact-binary review |
| Unsigned installer trust warnings | Medium | Transparent distribution, checksums, optional signing as later expense |
| Over-scoping v1.0 with video and browser features | High | Enforce P0 gate and separate integration releases |

# 16. Success metrics

**Reliability:** Percentage of deterministic downloads with byte-exact correct output; crash recovery test pass rate; failed-job classification accuracy.  
**Performance:** Throughput versus single-connection baseline on throttled and unthrottled test servers; disk/memory/network efficiency.  
**Usability:** User can add, pause, resume and locate a completed download without documentation; no confusing unlabelled status.  
**Product trust:** Zero required subscription charges, no mandatory telemetry, predictable uninstall and no unrelated browser privileges.  
**Engineering health:** Reproducible builds, passing CI, well-defined boundaries and meaningful diagnostics.

# 17. Assumptions and open decisions

**Fixed:** exclusively Windows 10 22H2 x64; completely free core stack; C#/.NET 10; Avalonia; local SQLite; custom HTTP download engine; no DRM circumvention; no accounts.

**Configurable decisions for implementation:** Product icon and visual identity; exact default storage path (recommended user's Downloads folder); installer per-user vs system-wide (prefer per-user); explicit OSS LICENSE file and third-party notice audit (public GitHub does not itself grant reuse rights); initial localization; opt-in startup behavior; baseline benchmark hardware and performance budgets.

**Release recommendation:** Build and ship a serious, thoroughly tested **v1.0 P0 download manager first**. Treat the more fragile browser handoff and site-specific media extraction as separate integrations that cannot destabilize downloading itself.

---

# 18. Primary technical references

1. Microsoft .NET releases/support — https://learn.microsoft.com/en-us/dotnet/core/releases-and-support
2. Avalonia framework licensing FAQ — https://docs.avaloniaui.net/tools/faq
3. NSIS license — https://nsis.sourceforge.io/Docs/AppendixI.html
4. Chrome Native Messaging — https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging
5. Chrome Web Store registration — https://developer.chrome.com/docs/webstore/register/
6. Edge Add-ons registration — https://learn.microsoft.com/en-us/microsoft-edge/extensions-chromium/publish/create-dev-account
7. yt-dlp distribution / third-party license notes — https://github.com/yt-dlp/yt-dlp
8. FFmpeg license notes — https://ffmpeg.org/legal.html
9. aria2 repository and GPL-2.0 — https://github.com/aria2/aria2
10. Mozilla downloads extension API — https://developer.mozilla.org/en-US/docs/Mozilla/Add-ons/WebExtensions/API/downloads
11. Microsoft Windows 10 lifecycle — https://learn.microsoft.com/en-us/lifecycle/products/windows-10-home-and-pro
12. Repository — https://github.com/ahmed-el4enawy/Velo

**End of PRD — Velo PRD v2.0 / planned application release v1.0**
