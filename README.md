<div align="center">

# 🗄️ UFS2Tool

**FreeBSD UFS1/UFS2 Filesystem Manager for Windows**

[![License: BSD-2-Clause](https://img.shields.io/badge/License-BSD_2--Clause-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6.svg)](#)

A complete implementation of FreeBSD's `newfs(8)` command for creating UFS1 and UFS2 filesystems on Windows, targeting both image files and raw disk devices.

</div>

---

## 📋 Table of Contents

- [Features](#-features)
- [Commands](#-commands)
  - [newfs](#newfs--create-a-new-ufs1ufs2-filesystem)
  - [info](#info--show-filesystem-information)
  - [makefs](#makefs--create-filesystem-image-from-directory-tree)
  - [ls](#ls--list-directory-contents)
  - [devinfo](#devinfo--show-device-information)
- [Examples](#-examples)
- [Building](#-building)
- [Testing](#-testing)
- [Implementation Details](#-implementation-details)
- [Notes](#-notes)
- [License](#-license)

---

## ✨ Features

- **Create UFS1 and UFS2 filesystems** on image files or raw Windows devices
- **Full newfs(8) compatibility** — supports all standard FreeBSD newfs flags (except `-T`, `-k`, `-r`)
- **Populate from directory** — create images from directory contents with auto-sizing (`-D`)
- **`makefs` command** — FreeBSD `makefs(8)` compatible interface for creating filesystem images from directory trees
- **Read and inspect** existing UFS1/UFS2 filesystem images
- **List directory contents** from UFS1/UFS2 images
- **Device I/O** — direct writing to physical drives and volumes on Windows

---

## 🛠 Commands

### `newfs` — Create a new UFS1/UFS2 filesystem

```
ufs2tool newfs [-EJNUjlnt] [-D input-directory] [-L volname] [-O format] [-S sector-size]
               [-a maxcontig] [-b block-size] [-c blocks-per-cg]
               [-d max-extent-size] [-e maxbpg] [-f frag-size]
               [-g avgfilesize] [-h avgfpdir] [-i bytes-per-inode]
               [-m free-space%] [-o optimization] [-p partition]
               [-s size] <target> [size-MB] [volume-name]
```

<details>
<summary><strong>Boolean flags</strong></summary>

| Flag | Description |
|------|-------------|
| `-E` | Erase (zero) device before creating filesystem |
| `-J` | Enable gjournal provider |
| `-N` | Dry run — display parameters without creating filesystem |
| `-U` | Enable soft updates |
| `-j` | Enable soft updates journaling (implies `-U`) |
| `-l` | Enable multilabel MAC support |
| `-n` | Do not create `.snap` directory |
| `-t` | Enable TRIM/DISCARD flag in superblock |

</details>

<details>
<summary><strong>Options with values</strong></summary>

| Option | Description | Default |
|--------|-------------|---------|
| `-D directory` | Input directory — populate image with directory contents. Size auto-calculated as `dir_size × 1.2 + 10 MB`. All files including hidden files are copied. | |
| `-L volname` | Volume label (max 32 chars) | |
| `-O format` | Filesystem format: `1` (UFS1) or `2` (UFS2) | `2` |
| `-S sector-size` | Sector size in bytes | `512` |
| `-a maxcontig` | Max contiguous blocks | auto |
| `-b block-size` | Block size (4096–65536, power of 2) | `32768` |
| `-c blocks-per-cg` | Blocks per cylinder group | auto |
| `-d max-extent` | Maximum extent size | auto |
| `-e maxbpg` | Max blocks per file in a cylinder group | auto |
| `-f frag-size` | Fragment size (≥512, power of 2) | `4096` |
| `-g avgfilesize` | Expected average file size | `16384` |
| `-h avgfpdir` | Expected average files per directory | `64` |
| `-i bytes/inode` | Inode density (bytes per inode) | auto |
| `-m free-space` | Minimum free space percentage | `8` |
| `-o optimization` | `time` or `space` | `time` |
| `-p partition` | Partition label (informational) | |
| `-s size` | Filesystem size in 512-byte sectors | auto |

</details>

### `info` — Show filesystem information

```
ufs2tool info <image-path>
```

### `makefs` — Create filesystem image from directory tree

> FreeBSD [`makefs(8)`](https://man.freebsd.org/cgi/man.cgi?query=makefs) compatible — creates a filesystem image from a directory tree without requiring special devices or privileges.

```
ufs2tool makefs [-DxZ] [-B endian] [-b free-blocks] [-f free-files]
                [-M minimum-size] [-m maximum-size] [-o fs-options]
                [-S sector-size] [-s image-size] [-T timestamp]
                [-t fs-type] image-file directory
```

<details>
<summary><strong>FFS-specific options</strong> (<code>-o key=value,...</code>)</summary>

| Option | Description | Default |
|--------|-------------|---------|
| `version` | UFS version: `1` for FFS, `2` for UFS2 | `1` |
| `bsize` | Block size | `32768` |
| `fsize` | Fragment size | `4096` |
| `label` | Volume label (max 32 chars) | |
| `softupdates` | `0` for disable, `1` for enable | `0` |
| `density` | Bytes per inode | auto |
| `minfree` | Minimum % free | `8` |
| `optimization` | `time` or `space` | `time` |
| `avgfilesize` | Expected average file size | `16384` |
| `avgfpdir` | Expected files per directory | `64` |
| `maxbpg` | Maximum blocks per file in CG | auto |
| `extent` | Maximum extent size | auto |
| `maxbpcg` | Maximum total blocks in CG | auto |

</details>

**Size suffixes:** `b` (×512), `k` (×1024), `m` (×1M), `g` (×1G), `t` (×1T), `w` (×4).
Products with `x`: e.g., `512x1024` = 524288.

### `ls` — List directory contents

```
ufs2tool ls <image-path> [path]
```

### `devinfo` — Show device information

```
ufs2tool devinfo <device-path>
```

---

## 💡 Examples

<details>
<summary><strong>Create filesystem images</strong></summary>

```bash
# Create a 256 MB UFS2 image with default settings
ufs2tool newfs myimage.img 256

# Create a UFS1 image with custom block/fragment sizes
ufs2tool newfs -O 1 -b 16384 -f 2048 myimage.img 128

# Create with soft updates, journaling, and TRIM enabled
ufs2tool newfs -Ujt -L MYVOLUME myimage.img 512

# Auto-sized image from directory contents
ufs2tool newfs -D /path/to/my/files output.img

# With explicit options and directory population
ufs2tool newfs -O 1 -D /path/to/my/files output.img MyVolume

# Dry run — show what would be created without writing
ufs2tool newfs -N myimage.img 256
```

</details>

<details>
<summary><strong>makefs — FreeBSD-compatible image creation</strong></summary>

```bash
# Create distribution image (soft updates disabled by default)
ufs2tool makefs output.img /path/to/my/files

# Create UFS2 image with makefs (FreeBSD-compatible syntax)
ufs2tool makefs -t ffs -o version=2 output.img /path/to/my/files

# makefs with label and custom block size
ufs2tool makefs -o version=2,bsize=32768,label=MYVOLUME output.img /path/to/my/files

# makefs with size constraints
ufs2tool makefs -s 256m output.img /path/to/my/files
ufs2tool makefs -b 10% -f 10% output.img /path/to/my/files

# makefs with soft updates explicitly enabled
ufs2tool makefs -o softupdates=1,version=2 output.img /path/to/my/files
```

</details>

<details>
<summary><strong>Device operations & inspection</strong></summary>

```bash
# Create on a Windows device (requires Administrator)
ufs2tool newfs \\.\PhysicalDrive2
ufs2tool newfs -O 1 -b 16384 -f 2048 \\.\E:

# Inspect a filesystem image
ufs2tool info myimage.img
ufs2tool ls myimage.img
```

</details>

---

## 🔨 Building

Requires [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.

**Using the solution file (recommended for Visual Studio):**

```bash
dotnet build UFS2-Tool.sln
```

**Using the project file directly:**

```bash
dotnet build UFS2Tool.csproj
dotnet run -- newfs myimage.img 256
```

---

## 🧪 Testing

The project includes a comprehensive [xUnit](https://xunit.net/) test suite in the `UFS2Tool.Tests` directory:

```bash
dotnet test
```

| Test Class | Purpose |
|------------|---------|
| `NewfsComplianceTests` | Validates superblock fields and directory structure compliance with FreeBSD specifications |
| `NewfsDOptionTests` | Tests directory population (`-D` option) including file content verification |
| `CylinderGroupTests` | Validates cylinder group layout, bitmaps, and metadata |
| `BitmapAndLinkCountTests` | Tests block allocation bitmaps and inode link count tracking |
| `LargeComplexTreeTests` | Validates large file and deep directory tree creation with indirect blocks |

---

## 📖 Implementation Details

For a detailed breakdown of the core library components, filesystem structures, and all implemented features, see [IMPLEMENTATIONS.md](IMPLEMENTATIONS.md).

---

## 📝 Notes

- Device operations (`newfs` on physical drives, `devinfo`) require **Administrator privileges** on Windows.
- The tool targets `net8.0` and uses conditional Windows-specific features for device I/O support.
- Image file operations work on any platform supported by .NET 8.0.
- Filesystem images created by this tool are compatible with FreeBSD's `mount` and `fsck_ffs`.
- When creating images with `makefs`, soft updates are disabled by default (`softupdates=0`), matching FreeBSD `makefs(8)` behavior. Use `-o softupdates=1` to enable them explicitly.

---

## 📄 License

This project is licensed under the **BSD 2-Clause License**. See the [LICENSE](LICENSE) file for details.
