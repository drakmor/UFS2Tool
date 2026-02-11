// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UFS2Tool;

namespace Ufs2Tool
{
    class Program
    {
        // Recognized single-character boolean flags for newfs (can be combined, e.g., -Ujt)
        private const string BooleanFlags = "EJNUjlnt";

        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            try
            {
                return args[0].ToLower() switch
                {
                    "newfs" => HandleNewFs(args),
                    "makefs" => HandleMakeFs(args),
                    "info" => HandleInfo(args),
                    "ls" => HandleLs(args),
                    "devinfo" => HandleDevInfo(args),
                    _ => PrintUsageAndReturn()
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                    Console.Error.WriteLine($"  Detail: {ex.InnerException.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Parse newfs arguments following FreeBSD newfs(8) conventions:
        ///   newfs [-EJNUjlnt] [-L volname] [-O filesystem-format] [-S sector-size]
        ///         [-a maxcontig] [-b block-size] [-c blocks-per-cylinder-group]
        ///         [-d max-extent-size] [-e maxbpg] [-f frag-size] [-g avgfilesize]
        ///         [-h avgfpdir] [-i bytes-per-inode] [-m free-space] [-o optimization]
        ///         [-p partition] [-s size] special
        ///
        /// Target can be:
        ///   - A file path:     myimage.img  (requires size-in-MB)
        ///   - A device path:   \\.\PhysicalDrive2  (size auto-detected)
        ///   - A volume path:   \\.\E:  (size auto-detected)
        /// </summary>
        static int HandleNewFs(string[] args)
        {
            var creator = new Ufs2ImageCreator();

            // Parse optional flags, collect positional args
            var positional = new List<string>();
            int i = 1;
            while (i < args.Length)
            {
                switch (args[i])
                {
                    // --- Flags with values ---
                    case "-O":
                        if (!TryParseIntArg(args, ref i, "filesystem format", out int format))
                            return 1;
                        creator.FilesystemFormat = format;
                        break;

                    case "-b":
                        if (!TryParseIntArg(args, ref i, "block size", out int blockSize))
                            return 1;
                        creator.BlockSize = blockSize;
                        break;

                    case "-f":
                        if (!TryParseIntArg(args, ref i, "fragment size", out int fragSize))
                            return 1;
                        creator.FragmentSize = fragSize;
                        break;

                    case "-S":
                        if (!TryParseIntArg(args, ref i, "sector size", out int sectorSize))
                            return 1;
                        creator.SectorSize = sectorSize;
                        break;

                    case "-L":
                        if (!TryParseStringArg(args, ref i, "volume name", out string? volName))
                            return 1;
                        creator.VolumeName = volName!;
                        break;

                    case "-a":
                        if (!TryParseIntArg(args, ref i, "maxcontig", out int maxContig))
                            return 1;
                        creator.MaxContig = maxContig;
                        break;

                    case "-c":
                        if (!TryParseIntArg(args, ref i, "blocks per cylinder group", out int bpcg))
                            return 1;
                        creator.BlocksPerCylGroup = bpcg;
                        break;

                    case "-d":
                        if (!TryParseIntArg(args, ref i, "max extent size", out int maxExtent))
                            return 1;
                        creator.MaxExtentSize = maxExtent;
                        break;

                    case "-e":
                        if (!TryParseIntArg(args, ref i, "maxbpg", out int maxBpg))
                            return 1;
                        creator.MaxBpg = maxBpg;
                        break;

                    case "-g":
                        if (!TryParseIntArg(args, ref i, "average file size", out int avgFile))
                            return 1;
                        creator.AvgFileSize = avgFile;
                        break;

                    case "-h":
                        if (!TryParseIntArg(args, ref i, "average files per directory", out int avgFpd))
                            return 1;
                        creator.AvgFilesPerDir = avgFpd;
                        break;

                    case "-i":
                        if (!TryParseIntArg(args, ref i, "bytes per inode", out int bpi))
                            return 1;
                        creator.BytesPerInode = bpi;
                        break;

                    case "-m":
                        if (!TryParseIntArg(args, ref i, "minimum free space %", out int minfree))
                            return 1;
                        creator.MinFreePercent = minfree;
                        break;

                    case "-o":
                        if (!TryParseStringArg(args, ref i, "optimization", out string? optim))
                            return 1;
                        if (optim != "time" && optim != "space")
                        {
                            Console.Error.WriteLine($"Error: -o must be 'time' or 'space'. Got: '{optim}'");
                            return 1;
                        }
                        creator.OptimizationPreference = optim!;
                        break;

                    case "-p":
                        if (!TryParseStringArg(args, ref i, "partition", out string? part))
                            return 1;
                        creator.Partition = part!;
                        break;

                    case "-D":
                        if (!TryParseStringArg(args, ref i, "input directory", out string? inputDir))
                            return 1;
                        creator.InputDirectory = inputDir!;
                        break;

                    case "-s":
                        if (!TryParseLongArg(args, ref i, "size", out long sizeVal))
                            return 1;
                        creator.SizeOverride = sizeVal;
                        break;

                    // --- Boolean flags ---
                    case "-E":
                        creator.EraseContents = true;
                        i++;
                        break;
                    case "-J":
                        creator.Gjournal = true;
                        i++;
                        break;
                    case "-N":
                        creator.DryRun = true;
                        i++;
                        break;
                    case "-U":
                        creator.SoftUpdates = true;
                        i++;
                        break;
                    case "-j":
                        creator.SoftUpdatesJournal = true;
                        i++;
                        break;
                    case "-l":
                        creator.MultilabelMac = true;
                        i++;
                        break;
                    case "-n":
                        creator.NoSnapDir = true;
                        i++;
                        break;
                    case "-t":
                        creator.TrimEnabled = true;
                        i++;
                        break;

                    default:
                        // Check for combined boolean flags (e.g., -EUjt)
                        if (args[i].StartsWith("-") && args[i].Length > 1 &&
                            IsBooleanFlagString(args[i]))
                        {
                            ParseCombinedFlags(creator, args[i]);
                            i++;
                        }
                        else
                        {
                            positional.Add(args[i]);
                            i++;
                        }
                        break;
                }
            }

            if (positional.Count < 1)
            {
                Console.Error.WriteLine("Error: Missing target (image path or device path).");
                PrintNewFsUsage();
                return 1;
            }

            string target = positional[0];
            bool isDevice = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && DriveIO.IsDevicePath(target);

            // Volume name from positional arg (legacy compatibility)
            // With -D and no explicit size, positional layout is: <image-path> [volume-name]
            // Without -D, positional layout is: <image-path> <size-MB> [volume-name]
            if (string.IsNullOrEmpty(creator.VolumeName))
            {
                bool hasInputDir = !string.IsNullOrEmpty(creator.InputDirectory);
                int volIdx;
                if (isDevice)
                {
                    volIdx = 1;
                }
                else if (hasInputDir && (positional.Count < 2 || !long.TryParse(positional[1], out _)))
                {
                    // With -D and no numeric size arg, volume name is at index 1
                    volIdx = 1;
                }
                else
                {
                    volIdx = 2;
                }
                if (positional.Count > volIdx)
                    creator.VolumeName = positional[volIdx];
            }

            if (isDevice)
            {
                return HandleNewFsDevice(creator, target);
            }
            else
            {
                return HandleNewFsImage(creator, target, positional);
            }
        }

        /// <summary>
        /// Handle makefs command — replicates FreeBSD makefs(8) behavior exactly.
        ///
        /// Synopsis:
        ///   makefs [-DxZ] [-B endian] [-b free-blocks] [-d debug-mask]
        ///          [-F mtree-specfile] [-f free-files] [-M minimum-size]
        ///          [-m maximum-size] [-N userdb-dir] [-O offset] [-o fs-options]
        ///          [-R roundup-size] [-S sector-size] [-s image-size]
        ///          [-T timestamp] [-t fs-type] image-file directory
        ///
        /// Only -t ffs is supported (the default).
        /// FFS-specific -o options: version, bsize, fsize, label, softupdates,
        ///   density, minfree, optimization, avgfilesize, avgfpdir, maxbpg,
        ///   extent, maxbpcg
        /// </summary>
        static int HandleMakeFs(string[] args)
        {
            var creator = new Ufs2ImageCreator
            {
                // makefs defaults: version=1 (UFS1/FFS) per FreeBSD convention
                FilesystemFormat = 1
            };

            var positional = new List<string>();
            long imageSize = 0;
            long minimumSize = 0;
            long maximumSize = 0;
            long roundupSize = 0;
            string freeBlocksStr = "";
            string freeFilesStr = "";
            long makeFsTimestamp = -1;
            int i = 1;

            while (i < args.Length)
            {
                switch (args[i])
                {
                    case "-B":
                        // Byte order — accepted but UFS uses native little-endian
                        if (!TryParseStringArg(args, ref i, "endian", out _))
                            return 1;
                        break;

                    case "-b":
                        if (!TryParseStringArg(args, ref i, "free-blocks", out string? fbStr))
                            return 1;
                        freeBlocksStr = fbStr!;
                        break;

                    case "-d":
                        // Debug mask — accepted but not used
                        if (!TryParseStringArg(args, ref i, "debug-mask", out _))
                            return 1;
                        break;

                    case "-f":
                        if (!TryParseStringArg(args, ref i, "free-files", out string? ffStr))
                            return 1;
                        freeFilesStr = ffStr!;
                        break;

                    case "-M":
                        if (!TryParseStringArg(args, ref i, "minimum-size", out string? minStr))
                            return 1;
                        if (!TryParseMakeFsSize(minStr!, out minimumSize))
                        {
                            Console.Error.WriteLine($"Error: Invalid minimum size: '{minStr}'");
                            return 1;
                        }
                        break;

                    case "-m":
                        if (!TryParseStringArg(args, ref i, "maximum-size", out string? maxStr))
                            return 1;
                        if (!TryParseMakeFsSize(maxStr!, out maximumSize))
                        {
                            Console.Error.WriteLine($"Error: Invalid maximum size: '{maxStr}'");
                            return 1;
                        }
                        break;

                    case "-N":
                        // In FreeBSD makefs, -N is userdb-dir — accepted but not used
                        if (!TryParseStringArg(args, ref i, "userdb-dir", out _))
                            return 1;
                        break;

                    case "-o":
                        if (!TryParseStringArg(args, ref i, "fs-options", out string? fsOpts))
                            return 1;
                        if (!ParseFfsOptions(creator, fsOpts!))
                            return 1;
                        break;

                    case "-R":
                        if (!TryParseStringArg(args, ref i, "roundup-size", out string? roundupStr))
                            return 1;
                        if (!TryParseMakeFsSize(roundupStr!, out roundupSize))
                        {
                            Console.Error.WriteLine($"Error: Invalid roundup size: '{roundupStr}'");
                            return 1;
                        }
                        break;

                    case "-S":
                        if (!TryParseStringArg(args, ref i, "sector-size", out string? secStr))
                            return 1;
                        if (!TryParseMakeFsSize(secStr!, out long secSize))
                        {
                            Console.Error.WriteLine($"Error: Invalid sector size: '{secStr}'");
                            return 1;
                        }
                        creator.SectorSize = (int)secSize;
                        break;

                    case "-s":
                        if (!TryParseStringArg(args, ref i, "image-size", out string? imgSizeStr))
                            return 1;
                        if (!TryParseMakeFsSize(imgSizeStr!, out imageSize))
                        {
                            Console.Error.WriteLine($"Error: Invalid image size: '{imgSizeStr}'");
                            return 1;
                        }
                        break;

                    case "-T":
                        if (!TryParseStringArg(args, ref i, "timestamp", out string? tsStr))
                            return 1;
                        if (!long.TryParse(tsStr!, out makeFsTimestamp))
                        {
                            Console.Error.WriteLine($"Error: Invalid timestamp: '{tsStr}'. Expected seconds from epoch.");
                            return 1;
                        }
                        break;

                    case "-t":
                        if (!TryParseStringArg(args, ref i, "fs-type", out string? fsType))
                            return 1;
                        if (fsType != "ffs")
                        {
                            Console.Error.WriteLine($"Error: Only 'ffs' filesystem type is supported. Got: '{fsType}'");
                            return 1;
                        }
                        break;

                    case "-Z":
                    case "-p":
                    case "-D":
                    case "-x":
                        // Accepted flags (-Z/-p sparse, -D mtree duplicates, -x exclude)
                        i++;
                        break;

                    case "-F":
                    case "-O":
                        // Flags with values that are accepted but not used
                        if (!TryParseStringArg(args, ref i, args[i].TrimStart('-'), out _))
                            return 1;
                        break;

                    default:
                        positional.Add(args[i]);
                        i++;
                        break;
                }
            }

            if (positional.Count < 2)
            {
                Console.Error.WriteLine("Error: makefs requires an image path and a source directory.");
                PrintMakeFsUsage();
                return 1;
            }

            string imagePath = positional[0];
            string sourceDir = positional[1];

            if (!Directory.Exists(sourceDir))
            {
                Console.Error.WriteLine($"Error: Source directory not found: {sourceDir}");
                return 1;
            }

            // Parse free blocks: absolute count or percentage
            long freeBlocksAbs = 0;
            int freeBlocksPc = 0;
            if (!string.IsNullOrEmpty(freeBlocksStr))
            {
                if (freeBlocksStr.EndsWith('%'))
                {
                    if (!int.TryParse(freeBlocksStr[..^1], out freeBlocksPc))
                    {
                        Console.Error.WriteLine($"Error: Invalid free block percentage: '{freeBlocksStr}'");
                        return 1;
                    }
                }
                else
                {
                    if (!long.TryParse(freeBlocksStr, out freeBlocksAbs))
                    {
                        Console.Error.WriteLine($"Error: Invalid free blocks: '{freeBlocksStr}'");
                        return 1;
                    }
                }
            }

            // Parse free files: absolute count or percentage
            long freeFilesAbs = 0;
            int freeFilesPc = 0;
            if (!string.IsNullOrEmpty(freeFilesStr))
            {
                if (freeFilesStr.EndsWith('%'))
                {
                    if (!int.TryParse(freeFilesStr[..^1], out freeFilesPc))
                    {
                        Console.Error.WriteLine($"Error: Invalid free file percentage: '{freeFilesStr}'");
                        return 1;
                    }
                }
                else
                {
                    if (!long.TryParse(freeFilesStr, out freeFilesAbs))
                    {
                        Console.Error.WriteLine($"Error: Invalid free files: '{freeFilesStr}'");
                        return 1;
                    }
                }
            }

            creator.InputDirectory = sourceDir;

            string formatStr = (creator.FilesystemFormat == 1) ? "UFS1" : "UFS2";
            Console.WriteLine($"=== {formatStr} makefs — Image from Directory ===");
            Console.WriteLine();

            long totalSizeBytes = creator.MakeFsImage(imagePath, sourceDir,
                imageSize: imageSize,
                freeblocks: freeBlocksAbs, freeblockpc: freeBlocksPc,
                freefiles: freeFilesAbs, freefilepc: freeFilesPc,
                minimumSize: minimumSize, maximumSize: maximumSize,
                roundup: roundupSize, makeFsTimestamp: makeFsTimestamp);

            if (!creator.DryRun)
            {
                Console.WriteLine();
                Console.WriteLine("Image created successfully.");
                Console.WriteLine();

                using var image = new Ufs2Image(imagePath, readOnly: true);
                Console.WriteLine(image.GetInfo());
            }

            return 0;
        }

        /// <summary>
        /// Parse FFS-specific options from -o flag (comma-separated key=value pairs).
        /// Matches FreeBSD makefs(8) FFS-specific options.
        /// </summary>
        static bool ParseFfsOptions(Ufs2ImageCreator creator, string optionsStr)
        {
            foreach (string option in optionsStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = option.Split('=', 2);
                string key = parts[0].Trim();
                string value = parts.Length > 1 ? parts[1].Trim() : "";

                switch (key)
                {
                    case "version":
                        if (!int.TryParse(value, out int ver) || (ver != 1 && ver != 2))
                        {
                            Console.Error.WriteLine($"Error: Invalid version: '{value}'. Must be 1 or 2.");
                            return false;
                        }
                        creator.FilesystemFormat = ver;
                        break;

                    case "bsize":
                        if (!int.TryParse(value, out int bsize))
                        {
                            Console.Error.WriteLine($"Error: Invalid bsize: '{value}'");
                            return false;
                        }
                        creator.BlockSize = bsize;
                        break;

                    case "fsize":
                        if (!int.TryParse(value, out int fsize))
                        {
                            Console.Error.WriteLine($"Error: Invalid fsize: '{value}'");
                            return false;
                        }
                        creator.FragmentSize = fsize;
                        break;

                    case "label":
                        creator.VolumeName = value;
                        break;

                    case "softupdates":
                        if (!int.TryParse(value, out int su) || (su != 0 && su != 1))
                        {
                            Console.Error.WriteLine($"Error: Invalid softupdates: '{value}'. Must be 0 or 1.");
                            return false;
                        }
                        creator.SoftUpdates = (su == 1);
                        break;

                    case "density":
                        if (!int.TryParse(value, out int density))
                        {
                            Console.Error.WriteLine($"Error: Invalid density: '{value}'");
                            return false;
                        }
                        creator.BytesPerInode = density;
                        break;

                    case "minfree":
                        if (!int.TryParse(value, out int minfree))
                        {
                            Console.Error.WriteLine($"Error: Invalid minfree: '{value}'");
                            return false;
                        }
                        creator.MinFreePercent = minfree;
                        break;

                    case "optimization":
                        if (value != "time" && value != "space")
                        {
                            Console.Error.WriteLine($"Error: Invalid optimization: '{value}'. Must be 'time' or 'space'.");
                            return false;
                        }
                        creator.OptimizationPreference = value;
                        break;

                    case "avgfilesize":
                        if (!int.TryParse(value, out int avgfs))
                        {
                            Console.Error.WriteLine($"Error: Invalid avgfilesize: '{value}'");
                            return false;
                        }
                        creator.AvgFileSize = avgfs;
                        break;

                    case "avgfpdir":
                        if (!int.TryParse(value, out int avgfpd))
                        {
                            Console.Error.WriteLine($"Error: Invalid avgfpdir: '{value}'");
                            return false;
                        }
                        creator.AvgFilesPerDir = avgfpd;
                        break;

                    case "maxbpg":
                        if (!int.TryParse(value, out int maxbpg))
                        {
                            Console.Error.WriteLine($"Error: Invalid maxbpg: '{value}'");
                            return false;
                        }
                        creator.MaxBpg = maxbpg;
                        break;

                    case "extent":
                        if (!int.TryParse(value, out int extent))
                        {
                            Console.Error.WriteLine($"Error: Invalid extent: '{value}'");
                            return false;
                        }
                        creator.MaxExtentSize = extent;
                        break;

                    case "maxbpcg":
                        if (!int.TryParse(value, out int maxbpcg))
                        {
                            Console.Error.WriteLine($"Error: Invalid maxbpcg: '{value}'");
                            return false;
                        }
                        creator.BlocksPerCylGroup = maxbpcg;
                        break;

                    default:
                        Console.Error.WriteLine($"Warning: Unknown FFS option: '{key}'");
                        break;
                }
            }
            return true;
        }

        /// <summary>
        /// Parse a size value following FreeBSD makefs(8) conventions:
        /// - Decimal number of bytes
        /// - Optional suffix: b (×512), k (×1024), m (×1048576), g (×1073741824), t (×1099511627776), w (×4)
        /// - Products separated by 'x': e.g., "512x1024"
        /// </summary>
        static bool TryParseMakeFsSize(string input, out long result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Handle product notation (e.g., "512x1024")
            string[] factors = input.Split('x', StringSplitOptions.RemoveEmptyEntries);
            long product = 1;

            foreach (string factor in factors)
            {
                string trimmed = factor.Trim();
                if (trimmed.Length == 0)
                    return false;

                long multiplier = 1;
                string numPart = trimmed;

                // Check for suffix
                char lastChar = char.ToLower(trimmed[^1]);
                if (char.IsLetter(lastChar))
                {
                    numPart = trimmed[..^1];
                    multiplier = lastChar switch
                    {
                        'b' => 512,
                        'k' => 1024,
                        'm' => 1024 * 1024,
                        'g' => 1024L * 1024 * 1024,
                        't' => 1024L * 1024 * 1024 * 1024,
                        'w' => 4,  // word = sizeof(int)
                        _ => -1
                    };
                    if (multiplier < 0)
                        return false;
                }

                if (!long.TryParse(numPart, out long val))
                    return false;

                try
                {
                    checked { product *= val * multiplier; }
                }
                catch (OverflowException)
                {
                    return false;
                }
            }

            result = product;
            return result > 0;
        }

        /// <summary>
        /// Check if a flag string contains only recognized boolean flag characters.
        /// </summary>
        static bool IsBooleanFlagString(string flag)
        {
            for (int i = 1; i < flag.Length; i++)
            {
                if (!BooleanFlags.Contains(flag[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Parse combined single-character boolean flags (e.g., -EUjt).
        /// </summary>
        static void ParseCombinedFlags(Ufs2ImageCreator creator, string flags)
        {
            for (int i = 1; i < flags.Length; i++)
            {
                switch (flags[i])
                {
                    case 'E': creator.EraseContents = true; break;
                    case 'J': creator.Gjournal = true; break;
                    case 'N': creator.DryRun = true; break;
                    case 'U': creator.SoftUpdates = true; break;
                    case 'j': creator.SoftUpdatesJournal = true; break;
                    case 'l': creator.MultilabelMac = true; break;
                    case 'n': creator.NoSnapDir = true; break;
                    case 't': creator.TrimEnabled = true; break;
                }
            }
        }

        // --- Argument parsing helpers ---

        static bool TryParseIntArg(string[] args, ref int i, string name, out int value)
        {
            value = 0;
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine($"Error: {args[i]} requires a {name} value.");
                PrintNewFsUsage();
                return false;
            }
            if (!int.TryParse(args[i + 1], out value))
            {
                Console.Error.WriteLine($"Error: Invalid {name}: '{args[i + 1]}'");
                return false;
            }
            i += 2;
            return true;
        }

        static bool TryParseLongArg(string[] args, ref int i, string name, out long value)
        {
            value = 0;
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine($"Error: {args[i]} requires a {name} value.");
                PrintNewFsUsage();
                return false;
            }
            if (!long.TryParse(args[i + 1], out value))
            {
                Console.Error.WriteLine($"Error: Invalid {name}: '{args[i + 1]}'");
                return false;
            }
            i += 2;
            return true;
        }

        static bool TryParseStringArg(string[] args, ref int i, string name, out string? value)
        {
            value = null;
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine($"Error: {args[i]} requires a {name} value.");
                PrintNewFsUsage();
                return false;
            }
            value = args[i + 1];
            i += 2;
            return true;
        }

        // --- Command handlers ---

        static int HandleNewFsDevice(Ufs2ImageCreator creator, string devicePath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.Error.WriteLine("Error: Device writing is only supported on Windows.");
                return 1;
            }

            string formatStr = (creator.FilesystemFormat == 1) ? "UFS1" : "UFS2";
            Console.WriteLine($"=== {formatStr} newfs — Device Mode ===");
            Console.WriteLine();
            Console.WriteLine($"  Target:         {devicePath}");
            Console.WriteLine($"  Block size:     {creator.BlockSize} bytes (-b)");
            Console.WriteLine($"  Fragment size:  {creator.FragmentSize} bytes (-f)");

            // Query device info
            long deviceSize = DriveIO.GetDeviceSize(devicePath);
            int sectorSize = DriveIO.GetSectorSize(devicePath);

            Console.WriteLine($"  Device size:    {deviceSize:N0} bytes ({deviceSize / (1024 * 1024)} MB)");
            Console.WriteLine($"  Sector size:    {sectorSize} bytes");
            if (!string.IsNullOrEmpty(creator.VolumeName))
                Console.WriteLine($"  Volume name:    {creator.VolumeName}");
            Console.WriteLine();

            if (creator.DryRun)
            {
                Console.WriteLine("Dry run (-N) — computing parameters only:");
                Console.WriteLine();
                creator.CreateOnDevice(devicePath);
                return 0;
            }

            // Safety confirmation
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  WARNING: ALL DATA ON THIS DEVICE WILL BE DESTROYED!");
            Console.ResetColor();
            Console.Write("  Type 'yes' to continue: ");
            string? confirmation = Console.ReadLine()?.Trim();

            if (!string.Equals(confirmation, "yes", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  Aborted.");
                return 1;
            }

            Console.WriteLine();
            creator.CreateOnDevice(devicePath);

            Console.WriteLine();
            Console.WriteLine($"Done. The device now contains a {formatStr} filesystem.");
            Console.WriteLine("On FreeBSD, verify with: fsck_ffs /dev/<device>");

            return 0;
        }

        static int HandleNewFsImage(Ufs2ImageCreator creator, string imagePath,
            List<string> positional)
        {
            bool hasInputDir = !string.IsNullOrEmpty(creator.InputDirectory);

            if (positional.Count < 2 && creator.SizeOverride <= 0 && !hasInputDir)
            {
                Console.Error.WriteLine("Error: Image file target requires a size in MB (or use -s for size in sectors, or -D for auto-sizing).");
                Console.Error.WriteLine("  (Device targets auto-detect size. Did you mean a device path?)");
                PrintNewFsUsage();
                return 1;
            }

            // Validate input directory if specified
            if (hasInputDir && !Directory.Exists(creator.InputDirectory))
            {
                Console.Error.WriteLine($"Error: Input directory not found: {creator.InputDirectory}");
                return 1;
            }

            long sizeMb = 0;
            long totalSizeBytes = 0;

            if (creator.SizeOverride > 0)
            {
                totalSizeBytes = creator.SizeOverride * Ufs2Constants.DefaultSectorSize;
            }
            else if (positional.Count >= 2 && long.TryParse(positional[1], out sizeMb) && sizeMb > 0)
            {
                totalSizeBytes = sizeMb * 1024 * 1024;
            }
            else if (!hasInputDir)
            {
                Console.Error.WriteLine($"Error: Invalid size: '{positional[1]}'. Must be a positive integer (MB).");
                return 1;
            }

            // When -D is specified, use the unified CreateImageFromDirectory method that:
            // 1. Calculates required space from directory contents (when no explicit size)
            // 2. Creates an empty UFS2 filesystem
            // 3. Recursively adds all files and directories
            if (hasInputDir)
            {
                // Auto-size: let CreateImageFromDirectory calculate the size
                totalSizeBytes = creator.CreateImageFromDirectory(imagePath, creator.InputDirectory, totalSizeBytes);

                if (!creator.DryRun)
                {
                    Console.WriteLine();
                    Console.WriteLine("Image created successfully.");
                    Console.WriteLine();

                    using var image = new Ufs2Image(imagePath, readOnly: true);
                    Console.WriteLine(image.GetInfo());
                }

                return 0;
            }

            string formatStr = (creator.FilesystemFormat == 1) ? "UFS1" : "UFS2";
            Console.WriteLine($"=== {formatStr} newfs — Image File Mode ===");
            Console.WriteLine();
            Console.WriteLine($"  Target:         {imagePath}");
            Console.WriteLine($"  Size:           {totalSizeBytes / (1024 * 1024)} MB ({totalSizeBytes:N0} bytes)");
            Console.WriteLine($"  Block size:     {creator.BlockSize} bytes (-b)");
            Console.WriteLine($"  Fragment size:  {creator.FragmentSize} bytes (-f)");
            if (!string.IsNullOrEmpty(creator.VolumeName))
                Console.WriteLine($"  Volume name:    {creator.VolumeName}");
            Console.WriteLine();

            creator.CreateImage(imagePath, totalSizeBytes);

            if (!creator.DryRun)
            {
                Console.WriteLine("Image created successfully.");
                Console.WriteLine();

                using var image = new Ufs2Image(imagePath, readOnly: true);
                Console.WriteLine(image.GetInfo());
            }

            return 0;
        }

        static int HandleInfo(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ufs2tool info <image-path>");
                return 1;
            }

            using var image = new Ufs2Image(args[1], readOnly: true);
            Console.WriteLine(image.GetInfo());
            return 0;
        }

        static int HandleLs(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ufs2tool ls <image-path> [path]");
                return 1;
            }

            using var image = new Ufs2Image(args[1], readOnly: true);
            var entries = image.ListRoot();

            Console.WriteLine($"Root directory ({entries.Count} entries):");
            foreach (var entry in entries)
            {
                string typeStr = entry.FileType switch
                {
                    Ufs2Constants.DtDir => "DIR ",
                    Ufs2Constants.DtReg => "FILE",
                    Ufs2Constants.DtLnk => "LINK",
                    _ => "??? "
                };
                Console.WriteLine($"  {typeStr}  inode={entry.Inode,6}  {entry.Name}");
            }
            return 0;
        }

        /// <summary>
        /// Show information about a device (for debugging / verification).
        /// </summary>
        static int HandleDevInfo(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: ufs2tool devinfo <device-path>");
                Console.Error.WriteLine(@"  Example: ufs2tool devinfo \\.\PhysicalDrive2");
                return 1;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.Error.WriteLine("Error: Device info is only supported on Windows.");
                return 1;
            }

            string devicePath = args[1];

            if (!DriveIO.IsDevicePath(devicePath))
            {
                Console.Error.WriteLine($"Error: '{devicePath}' is not a recognized device path.");
                return 1;
            }

            long size = DriveIO.GetDeviceSize(devicePath);
            int sector = DriveIO.GetSectorSize(devicePath);

            Console.WriteLine($"Device:      {devicePath}");
            Console.WriteLine($"Size:        {size:N0} bytes");
            Console.WriteLine($"             {size / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"             {size / 1024.0 / 1024.0 / 1024.0:F2} GB");
            Console.WriteLine($"Sector size: {sector} bytes");
            Console.WriteLine($"Sectors:     {size / sector:N0}");

            return 0;
        }

        static void PrintNewFsUsage()
        {
            Console.WriteLine("""

                Usage:
                  ufs2tool newfs [-EJNUjlnt] [-D input-directory] [-L volname] [-O format]
                                 [-S sector-size] [-a maxcontig] [-b block-size]
                                 [-c blocks-per-cg] [-d max-extent-size] [-e maxbpg]
                                 [-f frag-size] [-g avgfilesize] [-h avgfpdir]
                                 [-i bytes-per-inode] [-m free-space%] [-o optimization]
                                 [-p partition] [-s size] <device-path> [volume-name]
                  ufs2tool newfs [options] <image-path> <size-MB> [volume-name]
                  ufs2tool newfs [options] -D <input-directory> <image-path> [volume-name]

                Targets:
                  Device path    \\.\PhysicalDrive2, \\.\E:    Size auto-detected
                  Image file     myimage.img                   Size required (in MB) or auto from -D

                Boolean flags:
                  -E             Erase (zero) device before creating filesystem
                  -J             Enable gjournal provider
                  -N             Dry run — display parameters without creating filesystem
                  -U             Enable soft updates
                  -j             Enable soft updates journaling (implies -U)
                  -l             Enable multilabel MAC support
                  -n             Do not create .snap directory
                  -t             Enable TRIM/DISCARD flag in superblock

                Options with values:
                  -D directory   Input directory — populate image with directory contents.
                                 Image size is auto-calculated from on-disk block-aligned
                                 usage plus 10% overhead for filesystem metadata.
                                 All files including hidden files are copied.
                                 Soft updates are off by default unless -U is specified.
                  -L volname     Set volume label (max 32 chars)
                  -O format      Filesystem format: 1 (UFS1) or 2 (UFS2, default)
                  -S sector-size Override sector size (default: 512, must be power of 2)
                  -a maxcontig   Max contiguous blocks before forced rotation delay
                  -b block-size  Block size in bytes (default: 32768)
                                 Must be a power of 2, between 4096 and 65536.
                  -c blocks-per-cg  Blocks per cylinder group (default: auto)
                  -d max-extent  Maximum extent size for file allocation
                  -e maxbpg      Maximum blocks per file in a cylinder group
                  -f frag-size   Fragment size in bytes (default: 4096)
                                 Must be a power of 2, at least 512.
                                 Block size / frag size ratio must be 1, 2, 4, or 8.
                  -g avgfilesize Expected average file size in bytes (default: 16384)
                  -h avgfpdir    Expected average files per directory (default: 64)
                  -i bytes/inode Number of bytes per inode (controls inode density)
                  -m free-space  Minimum free space percentage (default: 8)
                  -o optimization  Optimization preference: "time" (default) or "space"
                  -p partition   Partition label (informational)
                  -s size        Filesystem size in 512-byte sectors (overrides auto-detect)

                Examples:
                  ufs2tool newfs \\.\PhysicalDrive2
                  ufs2tool newfs -O 1 -b 16384 -f 2048 \\.\E:
                  ufs2tool newfs -Uj -b 32768 -f 4096 \\.\PhysicalDrive2
                  ufs2tool newfs -N myimage.img 256
                  ufs2tool newfs -O 1 myimage.img 128
                  ufs2tool newfs -b 65536 -f 8192 -L MYVOLUME myimage.img 512
                  ufs2tool newfs -Ujt -m 5 -o space myimage.img 1024
                  ufs2tool newfs -D /path/to/my/files output.img
                  ufs2tool newfs -Uj -O 1 -D /path/to/my/files output.img MyVolume

                FreeBSD equivalents:
                  newfs -O 2 -b 32768 -f 4096 /dev/da0p2
                  newfs -O 1 -b 16384 -f 2048 /dev/ada1p1
                  newfs -U -j -t /dev/ada0p2
                """);
        }

        static void PrintMakeFsUsage()
        {
            Console.WriteLine("""

                Usage:
                  ufs2tool makefs [-DxZ] [-B endian] [-b free-blocks] [-f free-files]
                                  [-M minimum-size] [-m maximum-size] [-o fs-options]
                                  [-S sector-size] [-s image-size] [-T timestamp]
                                  [-t fs-type] image-file directory

                Create a file system image from a directory tree, replicating
                FreeBSD makefs(8) behavior.

                Options:
                  -B endian        Set byte order (le/be) — informational for UFS
                  -b free-blocks   Minimum free blocks (optional '%' suffix for percentage)
                  -f free-files    Minimum free files/inodes (optional '%' suffix for percentage)
                  -M minimum-size  Set minimum size of the image
                  -m maximum-size  Set maximum size of the image
                  -o fs-options    Comma-separated FFS options (see below)
                  -S sector-size   Set sector size (default: 512)
                  -s image-size    Set image size (equivalent to -M and -m combined)
                  -T timestamp     Set timestamp for all files (seconds from epoch)
                  -t fs-type       Filesystem type (only 'ffs' supported, the default)
                  -Z               Create sparse file

                FFS-specific options (-o key=value,...):
                  version          UFS version: 1 for FFS (default), 2 for UFS2
                  bsize            Block size (default: 32768)
                  fsize            Fragment size (default: 4096)
                  label            Volume label (max 32 chars)
                  softupdates      0 for disable (default), 1 for enable
                  density          Bytes per inode
                  minfree          Minimum free space percentage (default: 8)
                  optimization     'time' (default) or 'space'
                  avgfilesize      Expected average file size
                  avgfpdir         Expected number of files per directory
                  maxbpg           Maximum blocks per file in a cylinder group
                  extent           Maximum extent size
                  maxbpcg          Maximum total blocks in a cylinder group

                Size suffixes: b (×512), k (×1024), m (×1M), g (×1G), t (×1T), w (×4)
                Products with 'x': e.g., 512x1024 = 524288

                Examples:
                  ufs2tool makefs output.img /path/to/my/files
                  ufs2tool makefs -t ffs -o version=2 output.img /path/to/my/files
                  ufs2tool makefs -o bsize=32768,fsize=4096,label=MYVOLUME output.img /path/to/files
                  ufs2tool makefs -s 256m output.img /path/to/my/files
                  ufs2tool makefs -b 10% -f 10% output.img /path/to/my/files
                  ufs2tool makefs -M 128m -m 512m output.img /path/to/my/files
                  ufs2tool makefs -o softupdates=1,version=2 output.img /path/to/my/files

                FreeBSD equivalents:
                  makefs -t ffs image.img source_directory
                  makefs -t ffs -o version=2 image.img source_directory
                  makefs -t ffs -o version=2,softupdates=1 image.img source_directory
                """);
        }

        static void PrintUsage()
        {
            Console.WriteLine("""
                UFS2 Tool — FreeBSD UFS1/UFS2 Filesystem Manager for Windows

                Commands:
                  newfs   [options] <target> [size-MB] [volume-name]
                          Create a new UFS1 or UFS2 filesystem on a device or image file
                          Supports all FreeBSD newfs(8) options (except -T, -k, -r)
                  newfs   -D <input-directory> <image-path> [volume-name]
                          Create image from directory contents (size auto-calculated)
                  makefs  [-o fs-options] [-s image-size] image-file directory
                          Create image from directory tree (FreeBSD makefs(8) compatible).
                          Supports -o for FFS options (version, bsize, fsize, label,
                          softupdates, density, minfree, optimization, etc.)
                          Soft updates disabled by default (softupdates=0).
                  info    <image-path>
                          Show filesystem info (UFS1/UFS2)
                  ls      <image-path> [path]
                          List directory contents
                  devinfo <device-path>
                          Show device size and sector information

                Target can be a device (\\.\PhysicalDrive2) or an image file (myimage.img).
                Device targets auto-detect size; image targets require size in MB or -D.

                Examples:
                  ufs2tool newfs \\.\PhysicalDrive2
                  ufs2tool newfs -O 1 -b 16384 -f 2048 myimage.img 128
                  ufs2tool newfs -Uj -b 32768 -f 4096 myimage.img 256 MYVOLUME
                  ufs2tool newfs -N myimage.img 256
                  ufs2tool newfs -D /path/to/my/files output.img
                  ufs2tool newfs -O 1 -D /path/to/my/files output.img MyVolume
                  ufs2tool makefs output.img /path/to/my/files
                  ufs2tool makefs -t ffs -o version=2 output.img /path/to/my/files
                  ufs2tool makefs -o bsize=32768,label=MYVOLUME output.img /path/to/files
                  ufs2tool info myimage.img
                  ufs2tool ls myimage.img
                  ufs2tool devinfo \\.\PhysicalDrive2

                Note: Device operations require Administrator privileges.

                For full newfs options: ufs2tool newfs --help
                """);
        }

        static int PrintUsageAndReturn()
        {
            PrintUsage();
            return 1;
        }
    }
}
