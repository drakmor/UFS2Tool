// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UFS2Tool;

namespace UFS2Tool.Tests
{
    /// <summary>
    /// Tests for large, complex directory trees with many subdirectories and files,
    /// matching the scenarios described in the problem statement that cause
    /// "INCORRECT BLOCK COUNT" and "DIRECTORY CORRUPTED" errors in fsck_ufs.
    /// </summary>
    public class LargeComplexTreeTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _imagePath;

        public LargeComplexTreeTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"ufs2complex_{Guid.NewGuid():N}");
            _imagePath = Path.Combine(Path.GetTempPath(), $"ufs2complex_{Guid.NewGuid():N}.img");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (File.Exists(_imagePath)) File.Delete(_imagePath);
            if (Directory.Exists(_testDir)) Directory.Delete(_testDir, recursive: true);
        }

        /// <summary>
        /// Test scenario from problem statement: 50+ subdirectories, 5000+ files,
        /// various file sizes including non-block-aligned sizes.
        /// Verifies di_blocks is correct for all inodes.
        /// </summary>
        [Fact]
        public void ComplexTree_50Subdirs_5000Files_AllDiBlocksCorrect()
        {
            // Create 50 subdirectories in root
            for (int i = 0; i < 50; i++)
            {
                string subDir = Path.Combine(_testDir, $"subdir_{i:D03}");
                Directory.CreateDirectory(subDir);
                
                // Put 100 files in each subdirectory (50 * 100 = 5000 files)
                for (int j = 0; j < 100; j++)
                {
                    string fileName = Path.Combine(subDir, $"file_{j:D04}.dat");
                    // Use various sizes to test fragment-level and block-level allocation
                    int size = (i * 1000 + j * 100) % 50000; // Sizes from 0 to 49,999 bytes
                    byte[] data = new byte[size];
                    // Add some content to make it non-zero
                    if (size > 0)
                    {
                        data[0] = (byte)i;
                        data[size - 1] = (byte)j;
                    }
                    File.WriteAllBytes(fileName, data);
                }
            }

            var creator = new Ufs2ImageCreator();
            var (_, diskSize, totalEntries) = Ufs2ImageCreator.CalculateDirectorySizes(_testDir, creator.BlockSize);
            long imageSize = creator.CalculateImageSize(diskSize);
            
            creator.CreateImage(_imagePath, imageSize);
            creator.PopulateFromDirectory(_imagePath, _testDir);

            // Verify the image
            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var sb = image.Superblock;
            int fragsPerBlock = sb.BSize / sb.FSize;

            // Verify all inodes have correct di_blocks
            // Limit check to avoid excessive iteration in large filesystems
            const int MaxInodesToCheck = 10000;
            long totalInodes = (long)sb.InodesPerGroup * sb.NumCylGroups;
            int verifiedInodes = 0;
            List<string> errors = new List<string>();
            
            for (uint ino = Ufs2Constants.RootInode; ino < Math.Min(totalInodes, MaxInodesToCheck); ino++)
            {
                var inode = image.ReadInode(ino);
                if (inode.Mode == 0) continue; // Skip unused inodes
                
                verifiedInodes++;
                
                // Calculate expected di_blocks based on file/directory size
                long expectedBlocks512 = CalculateExpectedDiBlocks(inode, sb, fragsPerBlock);
                
                if (inode.Blocks != expectedBlocks512)
                {
                    string error = $"Inode {ino}: di_blocks mismatch. " +
                        $"Stored={inode.Blocks}, Expected={expectedBlocks512}, " +
                        $"Size={inode.Size}, IsDir={inode.IsDirectory}";
                    errors.Add(error);
                    // Only capture first 10 errors to avoid overwhelming output
                    if (errors.Count >= 10) break;
                }
            }
            
            // Report any errors found
            if (errors.Count > 0)
            {
                string errorReport = "di_blocks mismatches found:\n" + string.Join("\n", errors);
                Assert.Fail(errorReport);
            }
            
            // Ensure we verified a reasonable number of inodes
            Assert.True(verifiedInodes >= 5051, // At least 1 root + 50 dirs + 5000 files
                $"Expected at least 5051 inodes, but only verified {verifiedInodes}");
        }

        /// <summary>
        /// Test root directory with 100+ direct entries to stress directory packing.
        /// </summary>
        [Fact]
        public void RootDirectory_100Entries_NoDirCorruption()
        {
            // Create 100 subdirectories directly in root
            for (int i = 0; i < 100; i++)
            {
                string subDir = Path.Combine(_testDir, $"dir_{i:D03}");
                Directory.CreateDirectory(subDir);
                // Add one file to each to ensure they're real directories
                File.WriteAllBytes(Path.Combine(subDir, "marker.txt"), new byte[] { (byte)i });
            }

            var creator = new Ufs2ImageCreator();
            var (_, diskSize, _) = Ufs2ImageCreator.CalculateDirectorySizes(_testDir, creator.BlockSize);
            long imageSize = creator.CalculateImageSize(diskSize);
            
            creator.CreateImage(_imagePath, imageSize);
            creator.PopulateFromDirectory(_imagePath, _testDir);

            // Verify we can read the root directory and all entries
            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var rootEntries = image.ListDirectory(Ufs2Constants.RootInode);
            
            // Should have ".", "..", and 100 subdirectories
            Assert.True(rootEntries.Count >= 102,
                $"Root directory should have at least 102 entries, found {rootEntries.Count}");
            
            // Verify all subdirectories are present
            var subdirNames = rootEntries
                .Where(e => e.FileType == Ufs2Constants.DtDir && e.Name != "." && e.Name != "..")
                .Select(e => e.Name)
                .OrderBy(n => n)
                .ToList();
            
            Assert.Equal(100, subdirNames.Count);
            
            // Verify we can read each subdirectory
            foreach (var entry in rootEntries)
            {
                if (entry.FileType == Ufs2Constants.DtDir && entry.Name != "." && entry.Name != "..")
                {
                    var subdirEntries = image.ListDirectory(entry.Inode);
                    Assert.True(subdirEntries.Count >= 3, // ".", "..", "marker.txt"
                        $"Subdirectory {entry.Name} should have at least 3 entries");
                }
            }
        }

        /// <summary>
        /// Test files with sizes that require fragment-level allocation.
        /// BlockSize=32768, FragmentSize=4096, so files < 32768 should use fragments.
        /// </summary>
        [Fact]
        public void SmallFiles_FragmentAllocation_CorrectDiBlocks()
        {
            // Create files with sizes that test fragment boundaries
            var testSizes = new[]
            {
                1024,   // 1 fragment (1024 < 4096)
                4096,   // 1 fragment exactly
                5000,   // 2 fragments (5000 needs 2 frags)
                8192,   // 2 fragments exactly
                10000,  // 3 fragments
                16384,  // 4 fragments exactly
                20000,  // 5 fragments
                32768,  // 8 fragments = 1 full block exactly
            };

            for (int i = 0; i < testSizes.Length; i++)
            {
                string fileName = Path.Combine(_testDir, $"file_{i}_{testSizes[i]}.dat");
                File.WriteAllBytes(fileName, new byte[testSizes[i]]);
            }

            var creator = new Ufs2ImageCreator();
            var (_, diskSize, _) = Ufs2ImageCreator.CalculateDirectorySizes(_testDir, creator.BlockSize);
            long imageSize = creator.CalculateImageSize(diskSize);
            
            creator.CreateImage(_imagePath, imageSize);
            creator.PopulateFromDirectory(_imagePath, _testDir);

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var sb = image.Superblock;
            int fragsPerBlock = sb.BSize / sb.FSize;
            
            // Verify each file has correct di_blocks
            var rootEntries = image.ListDirectory(Ufs2Constants.RootInode);
            var fileEntries = rootEntries.Where(e => e.FileType == Ufs2Constants.DtReg).ToList();
            
            Assert.Equal(testSizes.Length, fileEntries.Count);
            
            foreach (var entry in fileEntries)
            {
                var inode = image.ReadInode(entry.Inode);
                long expectedBlocks512 = CalculateExpectedDiBlocks(inode, sb, fragsPerBlock);
                
                Assert.Equal(expectedBlocks512, inode.Blocks);
            }
        }

        /// <summary>
        /// Calculate expected di_blocks for an inode based on its size and metadata blocks.
        /// This replicates the CORRECTED logic in WriteInodeAtPosition for validation.
        /// Per the fix for fsck_ufs INCORRECT BLOCK COUNT errors: di_blocks counts the last
        /// block at fragment granularity, matching FreeBSD's fsck_ufs expectations.
        /// </summary>
        private long CalculateExpectedDiBlocks(Ufs2Inode inode, Ufs2Superblock sb, int fragsPerBlock)
        {
            int sectorsPerFrag = sb.FSize / Ufs2Constants.DefaultSectorSize;
            
            // Count full data blocks at full-block granularity
            long fullBlocks = (inode.Size > 0) ? inode.Size / sb.BSize : 0;
            long dataFrags = fullBlocks * fragsPerBlock;

            // Count the last (partial) block at fragment granularity
            long tailBytes = (inode.Size > 0) ? inode.Size % sb.BSize : 0;
            if (tailBytes > 0)
            {
                long tailFrags = (tailBytes + sb.FSize - 1) / sb.FSize;
                dataFrags += tailFrags;
            }
            
            // Count metadata blocks: for simplicity, count non-zero indirect block pointers.
            // Note: This simplified count works for small to medium files (< ~134 MB with default settings)
            // because they only use direct blocks or single-indirect blocks.
            // For larger files with double/triple-indirect blocks, the FileBlockAllocation.MetadataBlockCount
            // in the actual implementation counts all intermediate indirect blocks, which this simplified
            // test calculation doesn't. The test files in this suite are small enough that this approximation
            // is valid (all files < 50 KB).
            int metadataBlockCount = 0;
            for (int i = 0; i < Ufs2Constants.NIndirect; i++)
            {
                if (inode.IndirectBlocks[i] != 0)
                    metadataBlockCount++;
            }
            
            long metadataFrags = metadataBlockCount * fragsPerBlock;
            
            long blocks512 = (dataFrags + metadataFrags) * sectorsPerFrag;
            return blocks512;
        }
    }
}
