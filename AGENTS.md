Context: We are building "Rig Audit Pro" V1 Spike: a trustworthy, read-only Windows desktop app for gamers that audits key performance configuration and outputs a simple report. This is NOT an optimizer/booster and must not modify system settings. Scope is locked; do not add features beyond the spec.

## 1) Hard guardrails (non-negotiable)

- Read-only only: no driver installs, no "latest driver" web checks, no BIOS actions, no tweaks, no registry writes (except reading).
- No background service: the app runs only when opened; no scheduled scans.
- No overlays / no injection: nothing that touches games.
- No telemetry: no analytics, no network calls.
- Fail gracefully: missing sensors/WMI fields must not crash the app.
- Fast: scan should finish in ~15 seconds on a typical gaming PC.
- Professional tone: no scareware ("CRITICAL!! 1000 issues found").

## 2) Spike goal & outputs

### Goal

Prove we can reliably:

- Collect core audit data
- Interpret it into a small set of findings
- Display results cleanly
- Export a JSON snapshot + log

### Outputs

Write to:
`%LOCALAPPDATA%\RigAuditPro\Outputs\<timestamp>\`

Files:

- `RigSnapshot.json` (valid JSON)
- `scan.log` (human-readable)

## 3) Tech stack (fixed for spike)

- C# .NET 8
- WPF
- System.Management (WMI)
- LibreHardwareMonitorLib (sensors)
- System.Text.Json (serialization)
- No heavy MVVM frameworks required (simple MVVM ok)

## 4) Repo structure (fixed)

```text
RigAuditPro.sln
src/
  RigAudit.App/          (WPF UI)
  RigAudit.Core/         (models + findings + rules)
  RigAudit.Collectors/   (collectors + scan runner + logging)
  RigAudit.Export/       (output folder helper + json writer)
```

## 5) Required data contract (RigSnapshot)

### RigSnapshot

- `TimestampUtc` (`DateTime`)
- `Machine`
  - `ComputerName` (`string`)
  - `WindowsUser` (`string`)
- `OS`
  - `WindowsEdition` (`string`, best effort)
  - `WindowsVersion` (`string`)
  - `WindowsBuildNumber` (`string` or `int`)
- `CPU`
  - `Name` (`string`)
  - `PhysicalCores` (`int?`)
  - `LogicalCores` (`int?`)
- `GPU`
  - `Name` (`string`)
  - `Vendor` (enum: `Nvidia`/`Amd`/`Intel`/`Unknown`)
  - `DriverVersion` (`string`)
- `Memory`
  - `TotalPhysicalGb` (`double?`)
  - `ConfiguredClockMhz` (`int?`) (document whether this is MHz or MT/s; be consistent)
- `Power`
  - `ActivePowerPlanName` (`string`)
  - `ActivePowerPlanGuid` (`string`)
- `Sensors` (best-effort)
  - `CpuPackageTempC` (`double?`)
  - `GpuTempC` (`double?`)
  - `CpuLoadPercent` (`double?`)
  - `GpuLoadPercent` (`double?`)

## 6) Collectors to implement (V1 spike)

Implement collectors with per-collector try/catch so no single collector kills the scan:

- `OsCollector`
  - best effort Windows version/build/edition
- `CpuCollector`
  - WMI `Win32_Processor`
- `GpuCollector`
  - WMI `Win32_VideoController` for name/driver version
  - Vendor derived from name (contains NVIDIA / AMD / Radeon / Intel)
- `MemoryCollector`
  - Total physical memory from `Win32_ComputerSystem.TotalPhysicalMemory`
  - Speed from `Win32_PhysicalMemory.Speed` if available
  - If multiple modules: choose max (or average) and document choice
- `PowerPlanCollector`
  - Read active power plan via `powercfg /getactivescheme` (read-only) OR WMI if easier
- `SensorCollector`
  - LibreHardwareMonitor snapshot
  - No crash if sensors missing

## 7) Rules engine (exactly 4 rules)

Create `Finding { Severity(Info|Warning), Title, Summary, SuggestedAction }`

Rules:

1. Low memory speed -> `Warning`
   - If `ConfiguredClockMhz` exists and:
   - DDR4 heuristic: if speed < 2666
   - DDR5 heuristic: if speed < 4800
   - DDR generation heuristic: if speed >= 4000 assume DDR5 else DDR4
   - Title: "Memory speed appears low; XMP/EXPO may be disabled."
2. Balanced power plan -> `Info`
   - If plan name contains "Balanced"
3. High CPU temp -> `Warning`
   - If `CpuPackageTempC >= 95`
4. Unknown required fields -> `Info`
   - If any required field is null/empty -> "Some data could not be detected; running as admin may improve detection."

## 8) UI requirements (minimal but clean)

WPF screens:

- Home
  - Run Scan button
  - status: idle / scanning / done / error
- Results
  - show all fields (`Unknown` if missing)
  - show findings list
  - buttons: "Open output folder", "Export JSON"

Scan must run in background task (don't freeze UI).

## 9) Acceptance criteria (definition of "done")

- `dotnet build` succeeds on Windows
- "Run Scan" completes in <15 seconds on typical PC
- Results show values or "Unknown"
- `RigSnapshot.json` created and valid
- `scan.log` created with per-collector success/failure
- App does not crash if LibreHardwareMonitor returns no sensors
