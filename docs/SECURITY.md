# Velo Security & Privacy Specification

## 1. Zero-Trust Security Posture

Velo is built on a strict zero-trust model for all external inputs (URLs, HTTP headers, browser extension messages, and file payloads).

### Core Tenets:
1. **Never Automatically Execute Downloaded Files**: Velo will NEVER automatically launch an executable or trigger shell execution upon download completion.
2. **Zero Telemetry / Offline-First**: No background telemetry, usage analytics, crash-reporting SDKs, or cloud pings exist in the codebase.
3. **Secret Redaction**: URLs containing query tokens (`?token=...`, `?key=...`), `Authorization` headers, and `Cookie` headers are automatically redacted in structured logs and SQLite events.

---

## 2. Path Traversal & Filename Sanitization

Malicious or poorly configured servers can send dangerous `Content-Disposition` headers containing traversal characters (`../../`), reserved Windows device names (`CON`, `PRN`, `AUX`, `NUL`, `COM1-9`, `LPT1-9`), or control characters.

### Enforcement Rules (`SafePathValidator`):
- Strip relative path segments (`.`, `..`, `/`, `\`).
- Strip invalid Windows characters (`<`, `>`, `:`, `"`, `/`, `\`, `|`, `?`, `*`).
- Intercept and prefix reserved DOS device names (e.g. `CON.txt` -> `_CON.txt`).
- Limit filename length to 255 characters.
- Restrict destinations to user-permitted directories; verify target path does not resolve outside target root via `Path.GetFullPath()`.
- Filename collision policies:
  - **Auto-rename**: Append ` (1)`, ` (2)` if file exists.
  - **Overwrite**: Only upon explicit user confirmation.
  - **Skip**: Abort safely without modifying existing file.

---

## 3. Safe Resume & Invariant Checking

When resuming an interrupted download, Velo ensures data integrity before appending or continuing byte transfers:
- **`If-Range` Header Validation**: Uses Strong ETag (or Last-Modified) when issuing resume requests.
- **206 Validation**: If a server returns `200 OK` instead of `206 Partial Content`, Velo aborts the segmented write immediately to prevent corrupting the partial file.
- **Resource Fingerprint**: If ETag or Content-Length changes between sessions, Velo marks the segments invalid and prompts the user to start a clean sequential download.

---

## 4. Browser Native Messaging Security

The companion browser extension communicates with `Velo.NativeMessaging.exe` using standard standard I/O (stdin/stdout) Native Messaging:
- **Origin Whitelisting**: The host verifies the extension ID matches the permitted manifest ID (`allowed_origins` for Chrome/Edge and `allowed_extensions` for Firefox).
- **JSON Schema Validation**: Inbound messages are strictly parsed against a typed C# model. Disallowed commands or malformed payloads terminate the process.
- **SSRF Prevention**: Requests targeting localhost (`127.0.0.1`, `::1`), private ranges (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`), or link-local (`169.254.0.0/16`) originated from the extension are blocked unless the user explicitly overrides in the desktop UI.
- **No Password / Cookie Harvesting**: The extension does not dump browser credential stores or cookies.

---

## 5. Memory & Resource Safety

- Pre-allocates disk files with `SetLength()` where possible to minimize fragmentation and ensure adequate disk space before initiating transfers.
- Enforces an absolute maximum limit on concurrently active TCP sockets (default: 24 global, 8 per host).
- Enforces timeout budgets on DNS resolution, initial connection, and socket reads.
