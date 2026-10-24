# WinPowerTools Suite ⚡🛠️

A native, lightweight, zero-bloat Windows power-tools utility built with **C# / .NET 8 WPF** and low-level **Win32 APIs**.

![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-blue)
![Framework](https://img.shields.io/badge/Framework-.NET%208%20WPF-purple)
![Size](https://img.shields.io/badge/Binary%20Size-~210%20KB-success)
![Memory](https://img.shields.io/badge/RAM%20Usage-~25%20MB-brightgreen)

---

## 🚀 5 Built-in Power Utilities

### 1. 🔓 File Lock & Process Unblocker
- Identifies which active processes (`explorer.exe`, Word, background services) are locking a file or folder.
- Uses Windows' native **Restart Manager API** (`rstrtmgr.dll`) without requiring 3rd-party kernel drivers.
- Drag-and-drop any locked file to identify the PID and terminate the lock with one click.

### 2. 🧹 Ghost Junk & Developer Cache Cleaner
- Cleans deep gigabyte-hogging hidden caches that standard Windows Disk Cleanup ignores:
  - iOS / iTunes local backups (`MobileSync/Backup`)
  - Python `pip cache` & .NET `nuget cache`
  - DirectX & GPU Shader caches (`D3DSCache`, `NVIDIA/DXCache`)
  - User `%TEMP%` files

### 3. 🌐 Bandwidth & Disk Hog Watchdog
- Live process I/O monitor using Win32 `kernel32!GetProcessIoCounters`.
- Isolates exact per-process disk and socket transfer rates (B/s, KB/s, MB/s) to catch hidden network/disk hogs.

### 4. 🔋 Power & Sleep Controller
- Queries `GetSystemPowerStatus` for real-time AC/battery percentage and estimated runtime.
- One-click forced sleep (`SetSuspendState`) to bypass Modern Standby sleep blockers.

### 5. 📌 Universal Window Pin (Always-on-Top)
- Global keyboard shortcut: **`Ctrl + Space`**.
- Pins any active foreground application (Notepad, browser, calculator, PDF) always on top.

---

## 📦 Building from Source

```powershell
dotnet publish ./WinPowerTools.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./dist
```

---

## 📄 License
MIT License.
