# Linux Guide

This guide describes the required packages and step-by-step instructions for running UFS2Tool and the UFS2Tool GUI on Linux.

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Required Packages](#required-packages)
  - [Ubuntu / Debian](#ubuntu--debian)
  - [Fedora / RHEL](#fedora--rhel)
  - [openSUSE](#opensuse)
  - [Arch Linux](#arch-linux)
- [Running from a Zipped Release](#running-from-a-zipped-release)
  - [Step 1 — Download the Release](#step-1--download-the-release)
  - [Step 2 — Extract the Archive](#step-2--extract-the-archive)
  - [Step 3 — Install Required Packages](#step-3--install-required-packages)
  - [Step 4 — Install the .NET Runtime (Framework-Dependent Only)](#step-4--install-the-net-runtime-framework-dependent-only)
  - [Step 5 — Set the Execute Permission](#step-5--set-the-execute-permission)
  - [Step 6 — Run UFS2Tool CLI](#step-6--run-ufs2tool-cli)
  - [Step 7 — Run UFS2Tool GUI](#step-7--run-ufs2tool-gui)
- [Troubleshooting](#troubleshooting)
- [Notes](#notes)

---

## Prerequisites

- **Architecture:** x64 (`linux-x64`) or ARM64 (`linux-arm64`)
- **.NET 8.0 Runtime** — required for framework-dependent builds. Self-contained builds include the runtime.

---

## Required Packages

The UFS2Tool GUI is built with [Avalonia UI](https://avaloniaui.net/) and requires the following native libraries to be installed on your system. The CLI tool has no additional native dependencies beyond the .NET runtime.

### GUI Dependencies

| Library | Purpose |
|---------|---------|
| `libx11` | X11 client library for windowing |
| `libice` | ICE (Inter-Client Exchange) protocol library |
| `libsm` | X Session Management library |
| `libfontconfig` | Font configuration and discovery |

### Ubuntu / Debian

```bash
sudo apt update
sudo apt install libx11-6 libice6 libsm6 libfontconfig1
```

### Fedora / RHEL

```bash
sudo dnf install libX11 libICE libSM fontconfig
```

### openSUSE

```bash
sudo zypper install libX11-6 libICE6 libSM6 fontconfig
```

### Arch Linux

```bash
sudo pacman -Syu libx11 libice libsm fontconfig
```

---

## Running from a Zipped Release

Follow these steps after downloading a zipped release from the [GitHub Releases](https://github.com/SvenGDK/UFS2-Tool/releases) page.

### Step 1 — Download the Release

Download the appropriate release archive for your platform from the releases page:

- `linux-x64` for Intel/AMD 64-bit systems
- `linux-arm64` for ARM 64-bit systems (e.g., Raspberry Pi 4/5, Apple Silicon VMs)

### Step 2 — Extract the Archive

```bash
# Create a directory for UFS2Tool
mkdir -p ~/UFS2Tool

# Extract the archive
unzip UFS2Tool-linux-x64.zip -d ~/UFS2Tool
# or for .tar.gz archives:
tar -xzf UFS2Tool-linux-x64.tar.gz -C ~/UFS2Tool
```

### Step 3 — Install Required Packages

Install the native libraries required by the GUI application. Choose the command for your distribution from the [Required Packages](#required-packages) section above.

For example, on Ubuntu/Debian:

```bash
sudo apt update
sudo apt install libx11-6 libice6 libsm6 libfontconfig1
```

### Step 4 — Install the .NET Runtime (Framework-Dependent Only)

If you downloaded a **framework-dependent** build (smaller file size, no runtime bundled), you need to install the .NET 8.0 runtime:

```bash
# Ubuntu/Debian (using Microsoft packages)
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0 --runtime dotnet

# Or install via your package manager (Ubuntu 22.04+)
sudo apt install dotnet-runtime-8.0

# Fedora
sudo dnf install dotnet-runtime-8.0

# Arch Linux
sudo pacman -Syu dotnet-runtime-8.0
```

> **Note:** If you downloaded a **self-contained** build, the .NET runtime is included and this step can be skipped.

### Step 5 — Set the Execute Permission

```bash
cd ~/UFS2Tool

# For the CLI tool
chmod +x UFS2Tool

# For the GUI application
chmod +x UFS2Tool.GUI
```

### Step 6 — Run UFS2Tool CLI

```bash
# Show help or create a filesystem
./UFS2Tool newfs myimage.img 256

# Inspect an existing image
./UFS2Tool info myimage.img

# List directory contents
./UFS2Tool ls myimage.img
```

### Step 7 — Run UFS2Tool GUI

```bash
./UFS2Tool.GUI
```

The GUI window should appear with tabs for Create Filesystem, Filesystem Operations, Maintenance, and more.

---

## Troubleshooting

### "libX11.so not found" or similar library errors

Install the missing library using your package manager. See the [Required Packages](#required-packages) section.

### Application crashes with font-related errors

Ensure `fontconfig` is installed and that at least one font is available:

```bash
# Ubuntu/Debian
sudo apt install libfontconfig1 fonts-dejavu-core

# Fedora
sudo dnf install fontconfig dejavu-sans-fonts

# Arch
sudo pacman -Syu fontconfig ttf-dejavu
```

### "Permission denied" when running the executable

Make sure the execute permission is set:

```bash
chmod +x UFS2Tool UFS2Tool.GUI
```

### GUI does not start on Wayland

Avalonia supports both X11 and Wayland. If the GUI does not start under Wayland, try forcing X11:

```bash
AVALONIA_SCREEN_SCALE_FACTORS="" ./UFS2Tool.GUI
```

Or set the `DISPLAY` environment variable if using XWayland:

```bash
export DISPLAY=:0
./UFS2Tool.GUI
```

---

## Notes

- The CLI tool works on any Linux distribution supported by .NET 8.0.
- Device operations (`devinfo`, `mount_udf`) are **Windows-only** and will not be available on Linux.
- All image file operations (create, inspect, extract, add, delete, replace, chmod, growfs, tunefs, fsck) work fully on Linux.
- The GUI application requires a graphical environment (X11 or Wayland).
