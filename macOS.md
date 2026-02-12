# macOS Guide

This guide describes the required packages and step-by-step instructions for running UFS2Tool and the UFS2Tool GUI on macOS.

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Required Packages](#required-packages)
- [Running from a Zipped Release](#running-from-a-zipped-release)
  - [Step 1 — Download the Release](#step-1--download-the-release)
  - [Step 2 — Extract the Archive](#step-2--extract-the-archive)
  - [Step 3 — Install the .NET Runtime (Framework-Dependent Only)](#step-3--install-the-net-runtime-framework-dependent-only)
  - [Step 4 — Remove the Quarantine Attribute](#step-4--remove-the-quarantine-attribute)
  - [Step 5 — Set the Execute Permission](#step-5--set-the-execute-permission)
  - [Step 6 — Run UFS2Tool CLI](#step-6--run-ufs2tool-cli)
  - [Step 7 — Run UFS2Tool GUI](#step-7--run-ufs2tool-gui)
- [Running as a .app Bundle](#running-as-a-app-bundle)
- [Troubleshooting](#troubleshooting)
- [Notes](#notes)

---

## Prerequisites

- **macOS version:** 10.14 (Mojave) or later
- **Architecture:** Intel (`osx-x64`) or Apple Silicon (`osx-arm64`)
- **.NET 8.0 Runtime** — required for framework-dependent builds. Self-contained builds include the runtime.

---

## Required Packages

macOS includes all native libraries required by [Avalonia UI](https://avaloniaui.net/) out of the box. No additional system packages need to be installed for the GUI to run.

The only external dependency is the **.NET 8.0 Runtime** if you are using a framework-dependent build.

---

## Running from a Zipped Release

Follow these steps after downloading a zipped release from the [GitHub Releases](https://github.com/SvenGDK/UFS2-Tool/releases) page.

### Step 1 — Download the Release

Download the appropriate release archive for your platform from the releases page:

- `osx-x64` for Intel-based Macs
- `osx-arm64` for Apple Silicon Macs (M1, M2, M3, M4)

### Step 2 — Extract the Archive

```bash
# Create a directory for UFS2Tool
mkdir -p ~/UFS2Tool

# Extract the archive
unzip UFS2Tool-osx-arm64.zip -d ~/UFS2Tool
# or for .tar.gz archives:
tar -xzf UFS2Tool-osx-arm64.tar.gz -C ~/UFS2Tool
```

### Step 3 — Install the .NET Runtime (Framework-Dependent Only)

If you downloaded a **framework-dependent** build (smaller file size, no runtime bundled), you need to install the .NET 8.0 runtime.

**Using the official installer (recommended):**

Download the .NET 8.0 Runtime installer from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) and run it.

**Using Homebrew:**

```bash
brew install dotnet@8
```

**Using the install script:**

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0 --runtime dotnet
```

> **Note:** If you downloaded a **self-contained** build, the .NET runtime is included and this step can be skipped.

### Step 4 — Remove the Quarantine Attribute

macOS Gatekeeper quarantines files downloaded from the internet. You need to remove this attribute before running the application:

```bash
cd ~/UFS2Tool

# Remove quarantine from all files in the directory
xattr -r -d com.apple.quarantine .
```

Without this step, macOS may display a warning such as _"UFS2Tool can't be opened because Apple cannot check it for malicious software"_ or silently refuse to run the application.

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

## Running as a .app Bundle

For a more native macOS experience, you can wrap the application in a `.app` bundle. This allows you to launch it from Finder and the Dock.

### Create the Bundle Structure

```bash
# Create the .app bundle structure
mkdir -p ~/Applications/UFS2Tool.app/Contents/MacOS
mkdir -p ~/Applications/UFS2Tool.app/Contents/Resources

# Copy the published files into the bundle
cp -R ~/UFS2Tool/* ~/Applications/UFS2Tool.app/Contents/MacOS/
```

### Create the Info.plist

Create a file at `~/Applications/UFS2Tool.app/Contents/Info.plist` with the following content:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>UFS2Tool</string>
    <key>CFBundleDisplayName</key>
    <string>UFS2Tool GUI</string>
    <key>CFBundleIdentifier</key>
    <string>com.svengdk.ufs2tool</string>
    <key>CFBundleVersion</key>
    <string>2.5.0</string>
    <key>CFBundleShortVersionString</key>
    <string>2.5.0</string>
    <key>CFBundleExecutable</key>
    <string>UFS2Tool.GUI</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSMinimumSystemVersion</key>
    <string>10.14</string>
</dict>
</plist>
```

### Launch from Finder

After creating the bundle, you can double-click `UFS2Tool.app` in Finder or drag it to the Dock.

> **Note:** You may need to right-click → Open the first time to bypass Gatekeeper, or remove the quarantine attribute as described in [Step 4](#step-4--remove-the-quarantine-attribute).

---

## Troubleshooting

### "UFS2Tool can't be opened because Apple cannot check it for malicious software"

Remove the quarantine attribute:

```bash
cd ~/UFS2Tool
xattr -r -d com.apple.quarantine .
```

Alternatively, right-click the application and choose **Open** from the context menu.

### "Permission denied" when running the executable

Make sure the execute permission is set:

```bash
chmod +x UFS2Tool UFS2Tool.GUI
```

### GUI does not render correctly on Retina displays

Avalonia supports HiDPI/Retina displays natively. If you see rendering issues, ensure `NSHighResolutionCapable` is set to `true` in your `Info.plist` (if using a `.app` bundle).

### .NET runtime not found

If the application reports that the .NET runtime is not found:

1. Verify the runtime is installed: `dotnet --list-runtimes`
2. Ensure the `DOTNET_ROOT` environment variable is set if you installed .NET to a non-standard location:

```bash
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$DOTNET_ROOT
```

---

## Notes

- The CLI tool works on any macOS version supported by .NET 8.0 (macOS 10.14+).
- Device operations (`devinfo`, `mount_udf`) are **Windows-only** and will not be available on macOS.
- All image file operations (create, inspect, extract, add, delete, replace, chmod, growfs, tunefs, fsck) work fully on macOS.
- Both Intel and Apple Silicon Macs are supported. Use the appropriate runtime identifier (`osx-x64` or `osx-arm64`) when downloading releases.
- The GUI application does not require any additional native packages beyond what macOS provides.
