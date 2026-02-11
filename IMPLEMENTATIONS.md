# Implementations

This document summarizes all features implemented in UFS2-Tool.

## Overview

UFS2-Tool is a complete implementation of FreeBSD's `newfs(8)` and `makefs(8)` commands written in C#, targeting .NET 8.0. It creates and manages UFS1/UFS2 filesystems on Windows, supporting both image files and raw disk devices. Filesystem images created by this tool are compatible with FreeBSD's `mount` and `fsck_ffs`.

## Project Structure

| Path | Description |
|------|-------------|
| `UFS2-Tool.sln` | Visual Studio solution file |
| `UFS2Tool.csproj` | Main project (console application, .NET 8.0) |
| `UFS2Tool.Tests/` | xUnit test project |
| `Program.cs` | CLI entry point and argument parsing |
| `Ufs2ImageCreator.cs` | Filesystem creation (`newfs`/`makefs` implementation) |
| `Ufs2Image.cs` | Filesystem reading and inspection |
| `Ufs2Superblock.cs` | Superblock structure (binary serialization) |
| `Ufs2Inode.cs` | UFS2 inode structure (256 bytes) |
| `Ufs1Inode.cs` | UFS1 inode structure (128 bytes) |
| `Ufs2DirectoryEntry.cs` | Directory entry structure |
| `Ufs2Constants.cs` | UFS filesystem constants |
| `DriveIO.cs` | Windows raw device I/O (Win32 P/Invoke) |
| `AlignedStream.cs` | Sector-aligned buffered write wrapper |
| `app.manifest` | Windows application manifest (admin elevation) |

## Commands

### `newfs` — Create a new UFS1/UFS2 filesystem

Full implementation of FreeBSD's `newfs(8)` command for creating UFS1 and UFS2 filesystems on image files or raw Windows devices. Supports all standard flags except `-T` (disktype), `-k` (held-for-metadata-blocks), and `-r` (reserved).

**Boolean flags:**

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

**Options with values:**

| Option | Description |
|--------|-------------|
| `-D directory` | Populate image from directory contents with auto-sizing (`dir_size × 1.2 + 10 MB`) |
| `-L volname` | Volume label (max 32 characters) |
| `-O format` | Filesystem format: `1` (UFS1) or `2` (UFS2, default) |
| `-S sector-size` | Sector size in bytes (default: 512) |
| `-a maxcontig` | Maximum contiguous blocks |
| `-b block-size` | Block size, 4096–65536, power of 2 (default: 32768) |
| `-c blocks-per-cg` | Blocks per cylinder group |
| `-d max-extent` | Maximum extent size |
| `-e maxbpg` | Maximum blocks per file in a cylinder group |
| `-f frag-size` | Fragment size, ≥512, power of 2 (default: 4096) |
| `-g avgfilesize` | Expected average file size (default: 16384) |
| `-h avgfpdir` | Expected average files per directory (default: 64) |
| `-i bytes/inode` | Inode density (bytes per inode) |
| `-m free-space` | Minimum free space percentage (default: 8) |
| `-o optimization` | Optimization preference: `time` or `space` (default: `time`) |
| `-p partition` | Partition label (informational) |
| `-s size` | Filesystem size in 512-byte sectors |

### `makefs` — Create filesystem image from directory tree

FreeBSD `makefs(8)` compatible interface for creating filesystem images from directory trees without requiring special devices or privileges.

**General options:**

| Option | Description |
|--------|-------------|
| `-Z` | Zero-fill free space |
| `-B endian` | Byte order |
| `-b free-blocks` | Free blocks to leave |
| `-f free-files` | Free file entries to leave |
| `-M minimum-size` | Minimum image size |
| `-m maximum-size` | Maximum image size |
| `-S sector-size` | Sector size |
| `-s image-size` | Fixed image size |
| `-T timestamp` | Timestamp for filesystem |
| `-t fs-type` | Filesystem type (ffs) |

**FFS-specific options** (`-o key=value,...`):

| Option | Description |
|--------|-------------|
| `version` | UFS version: `1` (FFS/UFS1) or `2` (UFS2) |
| `bsize` | Block size |
| `fsize` | Fragment size |
| `label` | Volume label (max 32 characters) |
| `softupdates` | Enable/disable soft updates (`0` or `1`) |
| `density` | Bytes per inode |
| `minfree` | Minimum free space percentage |
| `optimization` | `time` or `space` |
| `avgfilesize` | Expected average file size |
| `avgfpdir` | Expected files per directory |
| `maxbpg` | Maximum blocks per file in cylinder group |
| `extent` | Maximum extent size |
| `maxbpcg` | Maximum total blocks in cylinder group |

**Size suffixes:** `b` (×512), `k` (×1024), `m` (×1M), `g` (×1G), `t` (×1T), `w` (×4), and products with `x` (e.g., `512x1024`).

### `info` — Show filesystem information

Reads and displays detailed filesystem metadata from an existing UFS1/UFS2 image, including format, block/fragment sizes, cylinder groups, free resources, volume name, flags, and optimization settings.

### `ls` — List directory contents

Lists directory entries from a UFS1/UFS2 filesystem image, with optional path argument to list subdirectories. Supports traversal through direct and indirect block pointers.

### `devinfo` — Show device information

Displays Windows device information including total size and sector size for physical drives and volumes. Requires Administrator privileges.

## Core Library (`UFS2Tool`)

### Filesystem Creation (`Ufs2ImageCreator`)

- Full `newfs(8)` implementation for UFS1 and UFS2 formats
- Configurable block size, fragment size, sector size, and inode density
- Automatic cylinder group layout computation
- Superblock initialization with proper field offsets matching FreeBSD's `struct fs` (1376 bytes)
- Cylinder group descriptor writing with block/fragment/inode bitmaps and cluster summaries
- Root directory creation with `.` and `..` entries
- Optional `.snap` directory creation
- Recursive directory population from host filesystem (including hidden files)
- File data writing with direct, single-indirect, double-indirect, and triple-indirect block support
- Block and fragment allocation with bitmap tracking
- Free space and inode summary (`cs_summary`) tracking across cylinder groups
- Support for writing to both image files and raw Windows devices
- Dry run mode (`-N`) for parameter display without writing

### Filesystem Reading (`Ufs2Image`)

- Superblock parsing and validation for both UFS1 and UFS2 formats
- Inode reading by inode number with automatic cylinder group resolution
- UFS1-to-UFS2 inode conversion for unified API access
- Directory entry listing through direct and indirect blocks
- File content reading through direct, single-indirect, double-indirect, and triple-indirect blocks
- Filesystem summary information display

### Superblock (`Ufs2Superblock`)

- Complete superblock structure matching FreeBSD's `struct fs`
- Binary serialization and deserialization (read/write)
- Support for both UFS1 (`0x00011954`) and UFS2 (`0x19540119`) magic numbers
- All standard fields: block/fragment sizes, cylinder group parameters, free resource counts, timestamps, volume name, flags, and optimization settings

### Inodes

- **UFS2 Inode** (`Ufs2Inode`): 256-byte structure with 64-bit block pointers, 64-bit timestamps, and extended attributes (flags, generation, directory depth, external size)
- **UFS1 Inode** (`Ufs1Inode`): 128-byte structure with 32-bit block pointers and 32-bit timestamps
- File type detection (directory, regular file, symbolic link)
- Binary serialization and deserialization for both formats

### Directory Entries (`Ufs2DirectoryEntry`)

- FreeBSD `struct direct` compatible directory entry format
- 4-byte aligned record lengths matching FreeBSD's `DIRECTSIZ` macro
- Directory block size boundary (`DIRBLKSIZ` = 512 bytes) enforcement
- File type constants: unknown, FIFO, character device, directory, block device, regular file, symbolic link, socket, whiteout

### Constants (`Ufs2Constants`)

- All FreeBSD UFS magic numbers and offsets
- Default filesystem parameters matching FreeBSD defaults
- Filesystem flags: soft updates, journaling, gjournal, multilabel MAC, TRIM, ACLs
- Inode mode flags and permission constants
- Directory entry constants and file type values

### Device I/O (`DriveIO`)

- Windows-specific raw device access via Win32 P/Invoke
- Physical drive (`\\.\PhysicalDriveN`) and volume (`\\.\X:`) support
- Device geometry detection (total size, sector size) via `IOCTL_DISK_GET_DRIVE_GEOMETRY_EX`
- Volume locking and dismounting via `FSCTL_LOCK_VOLUME` and `FSCTL_DISMOUNT_VOLUME`
- `SafeFileHandle`-based device handle management

### Aligned Stream (`AlignedStream`)

- Sector-aligned buffered write wrapper for raw device I/O
- Automatic zero-padding for writes not aligned to sector boundaries
- Position alignment enforcement

## Test Suite

The project includes a comprehensive test suite (`UFS2Tool.Tests`) with the following test classes:

| Test Class | Purpose |
|------------|---------|
| `NewfsComplianceTests` | Validates superblock fields and directory structure compliance with FreeBSD specifications |
| `NewfsDOptionTests` | Tests directory population (`-D` option) including file content verification |
| `CylinderGroupTests` | Validates cylinder group layout, bitmaps, and metadata |
| `BitmapAndLinkCountTests` | Tests block allocation bitmaps and inode link count tracking |
| `LargeComplexTreeTests` | Validates large file and deep directory tree creation with indirect blocks |
