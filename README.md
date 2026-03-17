<div align="center">

# 🗄️ UFS2Tool

**FreeBSD UFS1/UFS2 Filesystem Manager for Windows, macOS & Linux.**

[![License: BSD-2-Clause](https://img.shields.io/badge/License-BSD_2--Clause-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D6.svg)](#)

A complete implementation of FreeBSD's `newfs(8)`, `makefs(8)`, `tunefs(8)`, `growfs(8)`, `fsck_ufs(8)`, `du(1)`, and `chmod` commands for creating, managing, and checking UFS1 and UFS2 filesystems, targeting both image files and raw disk devices (Windows). Image file operations work on any platform supported by .NET 8.0.

</div>

---

## 📋 Table of Contents

- [Features](#-features)
- [Commands](#-commands)
  - [newfs](#newfs--create-a-new-ufs1ufs2-filesystem)
  - [info](#info--show-filesystem-information)
  - [makefs](#makefs--create-filesystem-image-from-directory-tree)
  - [tunefs](#tunefs--tune-existing-filesystem-parameters)
  - [growfs](#growfs--expand-an-existing-filesystem)
  - [fsck_ufs](#fsck_ufs--filesystem-consistency-check)
  - [ls](#ls--list-directory-contents)
  - [extract](#extract--extract-files-from-filesystem)
  - [replace](#replace--replace-files-in-filesystem)
  - [add](#add--add-files-to-filesystem)
  - [delete](#delete--delete-files-from-filesystem)
  - [rename](#rename--rename-files-or-directories)
  - [chmod](#chmod--change-file-permissions)
  - [stat](#stat--show-detailed-file-information)
  - [find](#find--search-for-files-by-name-pattern)
  - [du](#du--show-disk-usage)
  - [mount_udf](#mount_udf--mount-ufs-image-as-windows-drive)
  - [umount_udf](#umount_udf--unmount-a-ufs-drive)
  - [devinfo](#devinfo--show-device-information)
- [Examples](#-examples)
- [PS5 Quick Start](#-ps5-quick-start)
- [GUI Application](#-gui-application)
- [Building](#-building)
- [Testing](#-testing)
- [Implementation Details](#-implementation-details)
- [Platform-Specific Guides](#-platform-specific-guides)
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
- **Rename files** or directories inside UFS1/UFS2 filesystem images
- **Search for files** by name pattern with glob wildcards (`*`, `?`) and type filtering
- **Disk usage analysis** similar to FreeBSD `du(1)` with human-readable sizes, depth limiting, and summary mode
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

### `tunefs` — Tune existing filesystem parameters

```
ufs2tool tunefs [-Ap] [-a enable|disable] [-e maxbpg] [-f avgfilesize]
               [-J enable|disable] [-j enable|disable] [-k metaspace]
               [-L volname] [-l enable|disable] [-m minfree]
               [-N enable|disable] [-n enable|disable] [-o space|time]
               [-s avgfpdir] [-t enable|disable] <image-path>
```

Modify layout parameters on an existing UFS1/UFS2 filesystem image. Equivalent to FreeBSD's `tunefs(8)`.

<details>
<summary><strong>Options</strong></summary>

| Option | Description |
|--------|-------------|
| `-A` | Write the updated superblock to all backup superblock locations |
| `-a enable\|disable` | Enable or disable POSIX.1e ACL support |
| `-e maxbpg` | Set maximum blocks per file in a cylinder group |
| `-f avgfilesize` | Set expected average file size |
| `-J enable\|disable` | Enable or disable gjournal |
| `-j enable\|disable` | Enable or disable soft updates journaling (enabling also enables soft updates) |
| `-k metaspace` | Set space (in frags) to hold for metadata blocks |
| `-L volname` | Set volume label (alphanumerics, dashes, underscores; max 31 chars) |
| `-l enable\|disable` | Enable or disable multilabel MAC support |
| `-m minfree` | Set minimum percentage of free space (0–99) |
| `-N enable\|disable` | Enable or disable NFSv4 ACL support |
| `-n enable\|disable` | Enable or disable soft updates |
| `-o space\|time` | Set optimization preference |
| `-p` | Print current tuneable values and exit (read-only) |
| `-s avgfpdir` | Set expected number of files per directory |
| `-t enable\|disable` | Enable or disable TRIM/DISCARD support |

</details>

```bash
# Print current tuneable values
ufs2tool tunefs -p myimage.img

# Enable soft updates journaling
ufs2tool tunefs -j enable myimage.img

# Set volume label and minfree
ufs2tool tunefs -L MYVOLUME -m 5 myimage.img

# Write updated superblock to all backup locations
ufs2tool tunefs -A -n enable myimage.img
```

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

### `rename` — Rename files or directories

```
ufs2tool rename <image-path> <fs-path> <new-name>
```

Rename a file or directory inside a UFS1/UFS2 filesystem image. The entry stays in the same parent directory; only its name is changed.

```bash
# Rename a file
ufs2tool rename myimage.img /path/to/old.txt newname.txt

# Rename a directory
ufs2tool rename myimage.img /path/to/olddir newdir
```

### `chmod` — Change file permissions

```
ufs2tool chmod [-R] <image-path> <mode> [fs-path | dir-mode]
```

Change the permission mode of a file or directory inside a UFS1/UFS2 filesystem image, or apply permissions to the entire image recursively.

| Option | Description |
|--------|-------------|
| `-R` | Apply recursively to entire image |

```bash
# Change permissions on a single file
ufs2tool chmod myimage.img 644 /path/to/file.txt

# Change permissions on a directory
ufs2tool chmod myimage.img 755 /path/to/dir

# Recursively set file permissions for the entire image
ufs2tool chmod -R myimage.img 644

# Recursively set file and directory permissions separately
ufs2tool chmod -R myimage.img 644 755
```

### `stat` — Show detailed file information

```
ufs2tool stat <image-path> <fs-path>
```

Displays detailed inode information for a file, directory, or symbolic link inside a UFS1/UFS2 filesystem image. Output includes file type, inode number, permissions (octal and symbolic), link count, owner UID/GID, size, block count, timestamps (access, modify, change, birth), and block pointer usage.

**Examples:**
```bash
# Show info for root directory
ufs2tool stat myimage.img /

# Show info for a specific file
ufs2tool stat myimage.img /path/to/file.txt

# Show info for a subdirectory
ufs2tool stat myimage.img /path/to/dir
```

### `find` — Search for files by name pattern

```
ufs2tool find <image-path> <name-pattern> [-type f|d|l] [-path start-path]
```

Recursively search for files and directories matching a name pattern within a UFS1/UFS2 filesystem image. Supports glob-style wildcards (`*` and `?`) and optional type filtering.

| Option | Description |
|--------|-------------|
| `-type f` | Match regular files only |
| `-type d` | Match directories only |
| `-type l` | Match symbolic links only |
| `-path start-path` | Start searching from the specified directory (default: `/`) |

```bash
# Find all .txt files
ufs2tool find myimage.img "*.txt"

# Find files matching a prefix
ufs2tool find myimage.img "game*" -type f

# Find all directories
ufs2tool find myimage.img "*" -type d

# Find files in a specific subdirectory
ufs2tool find myimage.img "*.cfg" -path /etc
```

### `du` — Show disk usage

```
ufs2tool du [-h] [-s] [-d depth] <image-path> [fs-path]
```

Display disk usage for files and directories in a UFS1/UFS2 filesystem image, similar to FreeBSD's `du(1)`.

| Option | Description |
|--------|-------------|
| `-h` | Human-readable sizes (e.g., 1K, 234M, 2G) |
| `-s` | Summary only (total for the specified path) |
| `-d depth` | Maximum directory depth to report |

```bash
# Show disk usage for the entire filesystem
ufs2tool du myimage.img

# Show disk usage with human-readable sizes
ufs2tool du -h myimage.img

# Show summary only
ufs2tool du -s myimage.img

# Show disk usage for a specific directory
ufs2tool du myimage.img /subdir

# Limit depth
ufs2tool du -d 1 myimage.img
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

### `umount_udf` — Unmount a UFS drive

```
ufs2tool umount_udf <drive-letter>
```

Unmount a previously mounted UFS filesystem drive. Alternatively, pressing Ctrl+C in the `mount_udf` terminal also cleanly unmounts.

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

The GUI's **PS5** tab also supports **batch processing** — add multiple input folders and generate all `.ffpkg` images in one operation. Output filenames are automatically derived from folder names, with collision detection to prevent overwrites when multiple folders share the same name.

---

## 🖥️ GUI Application

The project includes a modern cross-platform GUI built with [Avalonia UI](https://avaloniaui.net/) in the `UFS2Tool.GUI` directory. The GUI provides a graphical interface for all major UFS2Tool operations:

- **Create Filesystem** — Create UFS1/UFS2 images with configurable parameters
- **Filesystem Operations** — List, extract, add, delete, replace files, and change permissions
- **Content Browser** — Browse filesystem contents visually with tree navigation
- **Maintenance** — TuneFS, GrowFS, and FsckUFS operations
- **Write Filesystem** — Write UFS images to USB drives and physical devices with progress tracking and optional post-write verification (Windows, Linux, macOS)
- **Device Mount** — Mount/unmount UFS images as Windows drives (Windows only, requires Dokan)
- **PS5 Quick Create** — Preset templates for PS5-compatible filesystem creation with single-folder and batch processing modes
- **Settings** — Language selection with automatic detection from OS locale (supports 26 languages)

**Building the GUI:**

```bash
dotnet build UFS2Tool.GUI/UFS2Tool.GUI.csproj
```

**Running the GUI:**

```bash
dotnet run --project UFS2Tool.GUI/UFS2Tool.GUI.csproj
```

> **Note:** The GUI runs on Windows, Linux, and macOS. See the [Platform-Specific Guides](#-platform-specific-guides) section for setup instructions on each platform.

---

## 🔨 Building

Requires [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.

**Using the solution file (builds CLI, GUI, and tests):**

```bash
dotnet build UFS2-Tool.sln
```

**Using the project file directly (CLI only):**

```bash
dotnet build UFS2Tool.csproj
dotnet run -- newfs myimage.img 256
```

**GUI only:**

```bash
dotnet build UFS2Tool.GUI/UFS2Tool.GUI.csproj
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
| `RenameTests` | Tests renaming files and directories in UFS1/UFS2 filesystem images |
| `TuneFsTests` | Tests tunefs command and filesystem tuning |
| `GrowFsTests` | Tests growfs command and filesystem expansion |
| `FsckUfsTests` | Tests fsck_ufs command and filesystem consistency checking |
| `ChmodTests` | Tests chmod functionality: permission changes, recursive operations, and file type bit preservation |
| `FindTests` | Tests find functionality: pattern matching, type filtering, subdirectory searching, and UFS1/UFS2 support |
| `DuTests` | Tests du functionality: disk usage calculation, depth limiting, summary mode, and UFS1/UFS2 support |
| `ProgressOutputTests` | Tests progress output formatting during file addition operations |

---

## 📖 Implementation Details

For a detailed breakdown of the core library components, filesystem structures, and all implemented features, see [IMPLEMENTATIONS.md](IMPLEMENTATIONS.md).

---

## 🌐 Platform-Specific Guides

- **[Linux Guide](LINUX.md)** — Required packages and step-by-step instructions for running UFS2Tool and the GUI on Linux
- **[macOS Guide](macOS.md)** — Required packages and step-by-step instructions for running UFS2Tool and the GUI on macOS

---

## 📝 Notes

- Device operations (`newfs` on physical drives, `devinfo`, `mount_udf`) require **Administrator privileges** on Windows.
- The tool targets `net8.0` and uses conditional Windows-specific features for device I/O and Dokan mounting.
- Image file operations (create, inspect, extract, add, delete, replace, chmod, growfs, tunefs, fsck) work on any platform supported by .NET 8.0.
- Filesystem images created by this tool are compatible with FreeBSD's `mount` and `fsck_ffs`.
- When creating images with `makefs`, soft updates are disabled by default (`softupdates=0`), matching FreeBSD `makefs(8)` behavior. Use `-o softupdates=1` to enable them explicitly.

---

## 📄 License

This project is licensed under the **BSD 2-Clause License**. See the [LICENSE](LICENSE) file for details.
