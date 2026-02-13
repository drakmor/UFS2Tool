# Implementations

This document summarizes all features implemented in UFS2-Tool.

## Overview

UFS2-Tool is a complete implementation of FreeBSD's `newfs(8)`, `makefs(8)`, `tunefs(8)`, `growfs(8)`, and `fsck_ufs(8)` commands written in C#, targeting .NET 8.0. It creates, manages, and checks UFS1/UFS2 filesystems on Windows, supporting both image files and raw disk devices. Filesystem images created by this tool are compatible with FreeBSD's `mount` and `fsck_ffs`.

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
| `Ufs2DokanOperations.cs` | Dokan virtual filesystem for mounting UFS images |
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

### `tunefs` — Tune existing filesystem parameters

Full implementation of FreeBSD's `tunefs(8)` command for modifying layout parameters on an existing UFS1/UFS2 filesystem image. Reads the superblock, applies requested changes, and writes it back.

**Options:**

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

**Behavior notes:**

- POSIX.1e ACLs (`-a`) and NFSv4 ACLs (`-N`) are mutually exclusive, matching FreeBSD behavior.
- Enabling soft updates journaling (`-j enable`) also sets the soft updates flag.
- Optimization warnings are issued when minfree and optimization preference are mismatched (minfree ≥ 8% with space optimization, or minfree < 8% with time optimization).
- Volume label validation matches FreeBSD: only alphanumerics, dashes, and underscores allowed.
- Metadata space (`-k`) is rounded down to a block boundary and capped at half the cylinder group size.

### `info` — Show filesystem information

Reads and displays detailed filesystem metadata from an existing UFS1/UFS2 image, including format, block/fragment sizes, cylinder groups, free resources, volume name, flags, and optimization settings.

### `growfs` — Expand an existing filesystem

Full implementation of FreeBSD's `growfs(8)` command for expanding UFS1/UFS2 filesystem images. Grows an existing filesystem to use additional space by adding new cylinder groups and extending the last cylinder group.

**Options:**

| Option | Description |
|--------|-------------|
| `-N` | Test mode — print parameters without modifying the filesystem |
| `-y` | Assume yes to all prompts |
| `-s size` | New filesystem size. Without suffix: 512-byte sectors. Suffixes: `b` (bytes), `k` (KB), `m` (MB), `g` (GB), `t` (TB). Defaults to image file size |

**Implementation details:**

- Extends the last (joining) cylinder group if it was not previously full.
- Initializes new cylinder groups with proper CG headers, fragment/inode bitmaps, and cluster summaries.
- Writes inode tables for new CGs with randomized `di_gen` values per FreeBSD convention.
- Relocates the CG summary area (`fs_csaddr`) to a new cylinder group when the summary grows.
- Updates all superblock fields (`fs_size`, `fs_dsize`, `fs_ncg`, `fs_cssize`, free counts).
- Writes backup superblocks to all cylinder groups.
- Supports both UFS1 and UFS2 filesystem formats.
- Dry-run mode (`-N`) prints parameters without modifying the filesystem.

### `ls` — List directory contents

Lists directory entries from a UFS1/UFS2 filesystem image, with optional path argument to list subdirectories. Supports traversal through direct and indirect block pointers.

### `fsck_ufs` — Filesystem consistency check

Full implementation of FreeBSD's `fsck_ffs(8)`/`fsck_ufs(8)` command for checking UFS1/UFS2 filesystem image consistency. Also available as `fsck_ffs`. Performs the five standard phases of filesystem checking as defined by FreeBSD.

**Options:**

| Option | Description |
|--------|-------------|
| `-b block` | Use the specified block number as an alternate superblock |
| `-d` | Enable debugging messages |
| `-f` | Force check even if filesystem is marked clean |
| `-n` | Assume no to all questions; read-only mode |
| `-p` | Preen mode: only fix safe inconsistencies |
| `-y` | Assume yes to all questions |

**Five-phase checking:**

1. **Phase 1 — Check Blocks and Sizes**: Reads all inodes and validates direct/indirect block pointers are within range, detects duplicate block claims, verifies directory sizes are multiples of DIRBLKSIZ, and identifies allocated inodes with zero link counts.
2. **Phase 2 — Check Pathnames**: Walks the directory tree from root, validates that directory entries point to allocated inodes with valid inode numbers, verifies `.` and `..` entries are present and correct.
3. **Phase 3 — Check Connectivity**: Identifies orphaned inodes (allocated but unreferenced from any directory). In preen mode, reports candidates for reconnection to `lost+found`.
4. **Phase 4 — Check Reference Counts**: Compares observed link counts (from directory traversal) against stored link counts in each inode.
5. **Phase 5 — Check Cylinder Groups**: Validates CG magic numbers and indices, verifies inode bitmap consistency, and checks that superblock free block/inode/directory counts match CG totals.

**Implementation details:**

- Supports both UFS1 and UFS2 filesystem formats.
- Checks superblock validity (magic number, block/fragment sizes, CG parameters).
- Uses sparse block ownership tracking for memory efficiency on large images.
- Combined flag parsing supports FreeBSD-style combined flags (e.g., `-pf`, `-nyd`).
- Clean filesystem detection: skips checks when `FS_UNCLEAN` and `FS_NEEDSFSCK` flags are not set (unless `-f` is specified).
- Exit codes follow FreeBSD convention: 0 on success, 8 on general error.

### `extract` — Extract files from a filesystem image

Extracts files from a UFS1/UFS2 filesystem image to the local filesystem.

### `replace` — Replace files in a filesystem image

Replaces a file or directory in a UFS1/UFS2 filesystem image with content from the local filesystem. If the target is a file, its content is replaced with the source file. If the target is a directory, matching files in the source directory replace their counterparts in the target directory recursively.

**Synopsis:**
```
ufs2tool replace <image-path> <fs-path> <source-path>
```

**Features:**
- Replace a single file with same-size, smaller, or larger content
- Replace matching files in a directory tree recursively
- Supports both UFS1 and UFS2 filesystem formats
- Allocates additional blocks from free space when the replacement file is larger
- Updates inode metadata (size, timestamps, block counts)
- Validates filesystem consistency after replacement

### `add` — Add files to a filesystem image

Adds a file or directory to a UFS1/UFS2 filesystem image from the local filesystem. If the source is a file, it is added at the specified path. If the source is a directory, it is created at the specified path and its contents are added recursively.

**Synopsis:**
```
ufs2tool add <image-path> <fs-path> <source-path>
```

**Features:**
- Add a single file to any directory in the filesystem
- Add a directory with all contents recursively
- Inode allocation from cylinder group free inode bitmap
- Block allocation and data writing with direct, single-indirect, and double-indirect block support
- Directory entry creation with proper DIRBLKSIZ boundary handling and slack space reuse
- Supports both UFS1 and UFS2 filesystem formats
- Updates all filesystem metadata (inode bitmaps, fragment bitmaps, CG counters, superblock)
- Parent directory link count updates for subdirectory creation

### `delete` — Delete files from a filesystem image

Deletes a file or directory from a UFS1/UFS2 filesystem image. If the target is a directory, all contents are deleted recursively before the directory itself is removed.

**Synopsis:**
```
ufs2tool delete <image-path> <fs-path>
```

**Features:**
- Delete a single file, freeing all data blocks and inode
- Delete a directory and all contents recursively
- Block deallocation with fragment bitmap and cluster bitmap updates
- Inode deallocation with inode bitmap updates
- Directory entry removal with proper record length merging
- Supports both UFS1 and UFS2 filesystem formats
- Updates all filesystem metadata (CG counters, superblock free counts)
- Parent directory link count updates for subdirectory deletion

### `rename` — Rename files or directories

Renames a file or directory inside a UFS1/UFS2 filesystem image. The entry stays in the same parent directory; only its name is changed.

**Synopsis:**
```
ufs2tool rename <image-path> <fs-path> <new-name>
```

**Features:**
- Rename a single file or directory by path
- Preserves file content and inode metadata
- Directory contents are preserved after rename
- Updates inode change time (`ctime`)
- Supports both UFS1 and UFS2 filesystem formats
- Also available through the Dokan mount interface (`MoveFile`)

**Examples:**
```
ufs2tool rename myimage.img /path/to/old.txt newname.txt
ufs2tool rename myimage.img /path/to/olddir newdir
```

### `chmod` — Change file permissions

Changes the permission mode of a file or directory inside a UFS1/UFS2 filesystem image, or applies permissions to the entire image recursively.

**Synopsis:**
```
ufs2tool chmod [-R] <image-path> <mode> [fs-path | dir-mode]
```

**Features:**
- Change permissions of a single file or directory by path
- Recursively change permissions of all files and directories in the entire image (`-R`)
- Separate file and directory mode support for recursive operation
- Automatic directory execute bit derivation from file mode when not specified
- Preserves file type bits (regular file, directory, symlink)
- Updates inode change time (`ctime`) on permission changes
- Supports both UFS1 and UFS2 filesystem formats
- Octal mode parsing (e.g., `755`, `644`, `0777`)

**Options:**
| Option | Description |
|--------|-------------|
| `-R` | Apply recursively to entire image |

**Examples:**
```
ufs2tool chmod myimage.img 755 /path/to/dir
ufs2tool chmod myimage.img 644 /path/to/file.txt
ufs2tool chmod -R myimage.img 644
ufs2tool chmod -R myimage.img 644 755
```

### `devinfo` — Show device information

Displays Windows device information including total size and sector size for physical drives and volumes. Requires Administrator privileges.

### `mount_udf` — Mount UFS image as Windows drive

Mounts a UFS1/UFS2 filesystem image as a Windows drive letter using the Dokan user-mode filesystem driver. Modeled after FreeBSD's `mount_udf(8)` and `mount(8)` commands.

**Synopsis:**
```
ufs2tool mount_udf [-o options] [-v] <image-path> <drive-letter>
```

**Features:**
- Mounts UFS1 and UFS2 images as a Windows drive letter (e.g., `X:`)
- Browse files and directories through Windows Explorer
- Read file contents and copy files to the local filesystem
- Supports both UFS1 and UFS2 filesystem formats
- Reports volume label, filesystem type, and free space to Windows
- Thread-safe: handles concurrent file access from Windows
- Read-write support: modify existing files on the mounted filesystem

**Options:**
| Option | Description |
|--------|-------------|
| `-o ro` | Mount read-only (default) |
| `-o rw` | Mount read-write (modify existing files) |
| `-v` | Verbose output with Dokan debug logging |

**Prerequisites:**
- Requires the [Dokan driver](https://github.com/dokan-dev/dokany/releases) to be installed on Windows
- Requires Administrator privileges

**Limitations:**
- Symlinks are presented as regular files containing the link target

### `umount_udf` — Unmount a UFS drive

Unmounts a previously mounted UFS filesystem drive. Alternatively, pressing Ctrl+C in the `mount_udf` terminal also cleanly unmounts.

**Synopsis:**
```
ufs2tool umount_udf <drive-letter>
```

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

### Filesystem Reading and Writing (`Ufs2Image`)

- Superblock parsing and validation for both UFS1 and UFS2 formats
- Inode reading by inode number with automatic cylinder group resolution
- Inode writing back to disk for both UFS1 and UFS2 formats
- UFS1-to-UFS2 inode conversion (and reverse) for unified API access
- Directory entry listing through direct and indirect blocks
- File content reading through direct, single-indirect, double-indirect, and triple-indirect blocks
- File content replacement with in-place block overwriting
- Block allocation from cylinder group free space for larger replacement files
- File and directory addition with inode allocation, block allocation, and directory entry creation
- File and directory deletion with recursive content removal, block deallocation, and inode freeing
- Permission modification (chmod) for individual files/directories and recursive whole-image operations
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

### Dokan Virtual Filesystem (`Ufs2DokanOperations`)

- Implements `IDokanOperations` interface to present UFS image as a Windows drive
- Thread-safe access to `Ufs2Image` via locking
- Maps UFS directory entries to Windows `FileInformation` structures
- Converts Unix timestamps to Windows `DateTime`
- Reports filesystem metadata (volume label, free space, filesystem type)
- Handles path normalization between Windows backslash and UFS forward slash conventions
- Read-write support: modify existing file contents through standard Windows file operations
- File attributes reflect mount mode (read-only vs. read-write)
- Delete files and directories in read-write mode
- Set file timestamps in read-write mode
- Pattern-based file search (`FindFilesWithPattern`)
- File security descriptor queries (returns `NotImplemented` for Unix-based permissions)

## Test Suite

The project includes a comprehensive test suite (`UFS2Tool.Tests`) with the following test classes:

| Test Class | Purpose |
|------------|---------|
| `NewfsComplianceTests` | Validates superblock fields and directory structure compliance with FreeBSD specifications |
| `NewfsDOptionTests` | Tests directory population (`-D` option) including file content verification |
| `CylinderGroupTests` | Validates cylinder group layout, bitmaps, and metadata |
| `BitmapAndLinkCountTests` | Tests block allocation bitmaps and inode link count tracking |
| `LargeComplexTreeTests` | Validates large file and deep directory tree creation with indirect blocks |
| `TuneFsTests` | Tests tunefs command: flag toggling, value changes, mutual exclusions, backup superblock writes, and print mode |
| `GrowFsTests` | Tests growfs command: filesystem expansion, new CG creation, and CG summary relocation |
| `ExtractTests` | Tests file extraction from UFS1/UFS2 filesystem images |
| `ReplaceTests` | Tests file and directory replacement in UFS1/UFS2 filesystem images |
| `AddDeleteTests` | Tests adding and deleting files and directories in UFS1/UFS2 filesystem images, including recursive operations, binary content preservation, and fsck validation |
| `ChmodTests` | Tests chmod functionality: single file/directory permission changes, recursive whole-image chmod, file type bit preservation, read-only rejection, and fsck validation for both UFS1 and UFS2 |
| `FsckUfsTests` | Tests fsck_ufs command: clean filesystem detection, CG magic/count validation, superblock count mismatches, populated filesystem checking, multi-CG images, and post-growfs/tunefs consistency |
