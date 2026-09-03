# Signum code signing sample

A minimal .NET sample whose only purpose is to exercise a GitHub Actions workflow
that code-signs build artifacts with **Keyfactor Signum**.

The build produces two signing targets:

| Artifact                    | Kind             |
| --------------------------- | ---------------- |
| `SignumSample.App.exe`      | executable       |
| `SignumSample.Library.dll`  | library loaded by the executable |

At startup the executable loads the library and prints the Authenticode signature
state of **both** files, so one run tells you whether signing worked.

By default it only reports and exits `0`, which makes it usable as a smoke test on
an unsigned build. Pass `--require-signatures` to turn a missing signature into a
non-zero exit; the signing job uses that to gate its own output.

## Layout

```
.github/workflows/build-and-sign.yml   build + sign pipeline
src/SignumSample.App/                  console executable
src/SignumSample.Library/              class library (loaded by the exe)
Directory.Build.props                  shared assembly metadata, deterministic builds
```

## Build locally

```powershell
dotnet build -c Release
dotnet publish src/SignumSample.App/SignumSample.App.csproj -c Release -o artifacts/publish

.\artifacts\publish\SignumSample.App.exe                        # reports, exits 0
.\artifacts\publish\SignumSample.App.exe --require-signatures   # exits 1 until signed
```

Requires the .NET 8 SDK or newer. Both projects target `net8.0-windows` because
Authenticode inspection is a Windows-only API.

## How the signing job works

The `sign` job performs the sequence a developer would run by hand on a build
machine:

1. **Install the Signum Windows Agent** from its `.msi`, in unattended mode with
   `AGENTMODE=SERVER` (headless, CLI-driven).
2. **Authenticate the agent** against the Signum server with
   `C:\Program Files\KeyFactor\rtsetup.exe`. Once logged in, the certificates the
   account is entitled to — via its Signum policy — appear in the Windows
   certificate store, backed by the remote HSM.
3. **Sign** with `signtool sign /fd SHA256 /sha1 <thumbprint> /tr <tsa> /td SHA256`.
   The agent intercepts the private-key operation and forwards it to Signum; the
   key never reaches the runner.
4. **Verify** with `signtool verify /pa /all`, then run the signed executable.
5. **Log out** of the agent, always, even when a previous step failed.

## Required configuration

Set these under **Settings → Secrets and variables → Actions**.

### Secrets

| Secret                    | Required | Purpose |
| ------------------------- | -------- | ------- |
| `SIGNUM_PRIMARY_SERVER`   | yes      | Signum deployment URL (`RTPRIMARY`) |
| `SIGNUM_SECONDARY_SERVER` | no       | Failover deployment URL (`RTSECONDARY`) |
| `SIGNUM_CLIENT_ID`        | yes      | Client ID from the Signum SaaS portal |
| `SIGNUM_USERNAME`         | yes      | Signum service account |
| `SIGNUM_PASSWORD`         | yes      | Password for that account |
| `SIGNUM_CERT_THUMBPRINT`  | yes      | SHA-1 thumbprint of the code-signing certificate |
| `SIGNUM_AGENT_MSI_URL`    | see below | URL to the agent `.msi` |

### Variables

| Variable               | Default                          | Purpose |
| ---------------------- | -------------------------------- | ------- |
| `SIGNING_RUNNER`       | `windows-latest`                 | Runner label for the `sign` job |
| `SIGNUM_TARGET_STORE`  | `LocalMachine`                   | `LocalMachine` (adds `signtool /sm`) or `My` |
| `SIGNUM_SERVICE_PORT`  | `443`                            | Agent → server port |
| `TIMESTAMP_URL`        | `http://timestamp.digicert.com`  | RFC 3161 timestamp authority |

## Things to sort out before the first run

**Getting the agent MSI onto the runner.** The installer is only distributed
through the Signum SaaS portal, which needs authentication, so the workflow
cannot download it directly. Two options:

- Republish the `.msi` somewhere the runner can reach (blob storage with a SAS
  token, an internal artifact repository) and put that URL in
  `SIGNUM_AGENT_MSI_URL`.
- Pre-install the agent on a self-hosted runner. The workflow detects an existing
  `rtsetup.exe` and skips installation, and `SIGNUM_AGENT_MSI_URL` becomes
  unnecessary.

Pin the agent version rather than tracking "latest" — an installer that changes
under you turns a signing pipeline into a flaky one.

**Network reachability.** The agent needs outbound `443` to the Signum server. On
GitHub-hosted runners that means the server must be internet-reachable *and* its
network policy must accept GitHub's runner IP ranges, which are broad and change.
For an on-prem or private Signum deployment, use a self-hosted runner and set the
`SIGNING_RUNNER` variable.

**Authentication mode.** This workflow uses `authMode=LocalUsers` (username +
password) because it is the simplest to get running. `rtsetup.exe` also supports
`Ldap`, `ActivationCode` (`-code=...`), and `Certificate` (`-thumbprint=...`).
Certificate mode removes the stored password and is the better fit for a permanent
self-hosted runner, at the cost of provisioning a client certificate on it.

**Timestamping.** The default TSA is DigiCert's public endpoint, fine for testing.
Point `TIMESTAMP_URL` at whichever authority your signing policy mandates before
this goes anywhere near a release.

## Security notes

- Pull requests build but never sign. The `sign` job is gated on
  `github.event_name == 'push'`, so unreviewed code never gets near the signing
  credentials.
- Credentials are passed to `rtsetup.exe` through environment variables rather
  than interpolated into the step script, keeping them out of the temporary
  script file on the runner.
- For real release signing, uncomment `environment: code-signing` in the `sign`
  job. That scopes the secrets to a protected environment and lets you require a
  manual approval before anything is signed.
- On a shared self-hosted runner, note that the agent session lives on the machine
  between steps. The workflow always logs out at the end, but a runner that other
  jobs can use is a broader trust boundary than an ephemeral one.

## Reference

- [Signum agents](https://docs.keyfactor.com/signum/latest/signum-agents)
- [Signum CLI interface — Server Mode](https://docs.keyfactor.com/Signum-SaaS/latest/cli-interface-server-mode)
- [Using Signum with signtool](https://docs.keyfactor.com/signum/latest/using-signum-with-signtool)
- [Signum architecture and concepts](https://docs.keyfactor.com/signum/latest/signum-architecture-concepts)
