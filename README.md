<div align="center">

# 🗄️ UFS2Tool

**FreeBSD UFS1/UFS2 Filesystem Manager for Windows**

[![License: BSD-2-Clause](https://img.shields.io/badge/License-BSD_2--Clause-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6.svg)](#)

A complete implementation of FreeBSD's `newfs(8)`, `makefs(8)`, `tunefs(8)`, `growfs(8)`, and `fsck_ufs(8)` commands for creating, managing, and checking UFS1 and UFS2 filesystems on Windows, targeting both image files and raw disk devices.

</div>

---

## 📋 Table of Contents

- [Features](#-features)
- [Commands](#-commands)
  - [newfs](#newfs--create-a-new-ufs1ufs2-filesystem)
  - [info](#info--show-filesystem-information)
  - [makefs](#makefs--create-filesystem-image-from-directory-tree)
  - [growfs](#growfs--expand-an-existing-filesystem)
  - [fsck_ufs](#fsck_ufs--filesystem-consistency-check)
  - [ls](#ls--list-directory-contents)
  - [extract](#extract--extract-files-from-filesystem)
  - [replace](#replace--replace-files-in-filesystem)
  - [add](#add--add-files-to-filesystem)
  - [delete](#delete--delete-files-from-filesystem)
  - [mount_udf](#mount_udf--mount-ufs-image-as-windows-drive)
  - [devinfo](#devinfo--show-device-information)
- [Examples](#-examples)
- [PS5 Quick Start](#-ps5-quick-start)
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
- **`growfs` command** — FreeBSD `growfs(8)` compatible interface for expanding existing filesystem images
- **`fsck_ufs` command** — FreeBSD `fsck_ffs(8)`/`fsck_ufs(8)` compatible filesystem consistency checker
- **Extract files** from existing UFS1/UFS2 filesystem images
- **Replace files** in existing UFS1/UFS2 filesystem images (single file or directory tree)
- **Add files** to existing UFS1/UFS2 filesystem images (single file or directory tree, recursive)
- **Delete files** from existing UFS1/UFS2 filesystem images (single file or directory tree, recursive)
- **Mount UFS images** as Windows drives with read-write support via Dokan
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

### `growfs` — Expand an existing filesystem

```
ufs2tool growfs [-Ny] [-s size] <image-path>
```

Expand an existing UFS1/UFS2 filesystem image. Equivalent to FreeBSD's `growfs(8)`.

| Option | Description |
|--------|-------------|
| `-N` | Test mode — print parameters without modifying the filesystem |
| `-y` | Assume yes to all prompts |
| `-s size` | New filesystem size (default: image file size). Suffixes: `b` (bytes), `k` (KB), `m` (MB), `g` (GB), `t` (TB). Without suffix: 512-byte sectors |

**Examples:**

```bash
# Grow filesystem to fill expanded image
truncate -s 512M myimage.img
ufs2tool growfs -y myimage.img

# Grow to specific size
ufs2tool growfs -y -s 256m myimage.img

# Dry-run: see what would happen
ufs2tool growfs -N -s 1g myimage.img
```

### `fsck_ufs` — Filesystem consistency check

```
ufs2tool fsck_ufs [-dfnpy] [-b block] <filesystem>
```

File system consistency check and interactive repair. Equivalent to FreeBSD's `fsck_ffs(8)`/`fsck_ufs(8)`. Also available as `fsck_ffs`.

Performs a five-phase check:
1. **Phase 1** — Check Blocks and Sizes (validate block pointers, detect duplicates, verify sizes)
2. **Phase 2** — Check Pathnames (walk directory tree, validate entries)
3. **Phase 3** — Check Connectivity (find orphaned inodes)
4. **Phase 4** — Check Reference Counts (verify link counts)
5. **Phase 5** — Check Cylinder Groups (verify CG summaries, free counts)

| Option | Description |
|--------|-------------|
| `-b block` | Use the specified block number as an alternate superblock |
| `-d` | Enable debugging messages |
| `-f` | Force check even if filesystem is marked clean |
| `-n` | Assume no to all questions; read-only mode |
| `-p` | Preen mode: only fix safe inconsistencies |
| `-y` | Assume yes to all questions |

```bash
# Check a filesystem image
ufs2tool fsck_ufs myimage.img

# Force check a clean filesystem
ufs2tool fsck_ufs -f myimage.img

# Preen mode (auto-fix safe issues)
ufs2tool fsck_ufs -p myimage.img

# Check with debug output
ufs2tool fsck_ufs -df myimage.img

# Read-only check (same as -n)
ufs2tool fsck_ufs -n myimage.img
```

### `ls` — List directory contents

```
ufs2tool ls <image-path> [path]
```

### `extract` — Extract files from filesystem

```
ufs2tool extract <image-path> <output-directory> [fs-path]
```

Extract files from a UFS1/UFS2 filesystem image. If `fs-path` is omitted, extracts the entire filesystem. If it points to a directory, extracts recursively. If it points to a file, extracts that single file.

### `replace` — Replace files in filesystem

```
ufs2tool replace <image-path> <fs-path> <source-path>
```

Replace a file or directory in a UFS1/UFS2 filesystem image. If `fs-path` points to a file, its content is replaced with `source-path`. If it points to a directory, matching files from `source-path` are replaced recursively.

```bash
# Replace a single file
ufs2tool replace myimage.img /path/to/file.txt ./local/file.txt

# Replace matching files in a directory
ufs2tool replace myimage.img /path/to/dir ./local/dir
```

### `add` — Add files to filesystem

```
ufs2tool add <image-path> <fs-path> <source-path>
```

Add a file or directory to a UFS1/UFS2 filesystem image. If `source-path` is a file, it is added at `fs-path`. If `source-path` is a directory, it is created at `fs-path` and its contents are added recursively.

```bash
# Add a single file
ufs2tool add myimage.img /newfile.txt ./local/file.txt

# Add a file into a subdirectory
ufs2tool add myimage.img /subdir/newfile.txt ./local/file.txt

# Add a directory recursively
ufs2tool add myimage.img /newdir ./local/dir
```

### `delete` — Delete files from filesystem

```
ufs2tool delete <image-path> <fs-path>
```

Delete a file or directory from a UFS1/UFS2 filesystem image. If `fs-path` points to a directory, all contents are deleted recursively.

```bash
# Delete a single file
ufs2tool delete myimage.img /path/to/file.txt

# Delete a directory recursively
ufs2tool delete myimage.img /path/to/dir
```

### `mount_udf` — Mount UFS image as Windows drive

```
ufs2tool mount_udf [-o options] [-v] <image-path> <drive-letter>
```

Mount a UFS1/UFS2 filesystem image as a Windows drive letter using the Dokan driver. Supports read-only (default) and read-write modes.

| Option | Description |
|--------|-------------|
| `-o ro` | Mount read-only (default) |
| `-o rw` | Mount read-write (modify existing files) |
| `-v` | Verbose output |

```bash
# Mount read-only
ufs2tool mount_udf myimage.img X:

# Mount read-write
ufs2tool mount_udf -o rw myimage.img X:

# Unmount
ufs2tool umount_udf X:
```

Requires the [Dokan driver](https://github.com/dokan-dev/dokany/releases) to be installed.

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
<summary><strong>Add and delete files</strong></summary>

```bash
# Add a single file to the filesystem
ufs2tool add myimage.img /newfile.txt ./local/file.txt

# Add a directory recursively
ufs2tool add myimage.img /newdir ./local/dir

# Delete a single file
ufs2tool delete myimage.img /path/to/file.txt

# Delete a directory recursively
ufs2tool delete myimage.img /path/to/dir
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

## 📄 PS5 Quick Start

Use this command to quickly create a UFS2 image compatible to be mounted on the PS5 with ShadowMount:

```powershell
UFS2Tool.exe newfs -D <folder> <PPSAxxxx.ffpkg>
```

Alternatively, you can use this command to quickly create a UFS2 image with FreeBSD-compatible FFS options that can be mounted on the PS5 with ShadowMount:

```powershell
UFS2Tool.exe makefs -S 4096 -t ffs -o version=2,minfree=0,softupdates=0,optimization=space <PPSAxxxx.ffpkg> <folder>
```

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
| `ExtractTests` | Tests file extraction from UFS1/UFS2 filesystem images |
| `ReplaceTests` | Tests file and directory replacement in UFS1/UFS2 filesystem images |
| `AddDeleteTests` | Tests adding and deleting files and directories in UFS1/UFS2 filesystem images |
| `TuneFsTests` | Tests tunefs command and filesystem tuning |
| `GrowFsTests` | Tests growfs command and filesystem expansion |
| `FsckUfsTests` | Tests fsck_ufs command and filesystem consistency checking |

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
