// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.IO;
using System.Linq;
using UFS2Tool;

namespace UFS2Tool.Tests
{
    /// <summary>
    /// Tests for the du (disk usage) functionality:
    /// 1. Disk usage of root directory on a UFS2 image
    /// 2. Disk usage of a specific subdirectory
    /// 3. Disk usage of a single file
    /// 4. Disk usage with summary only
    /// 5. Disk usage with max depth
    /// 6. Disk usage on a UFS1 image
    /// 7. Disk usage on empty filesystem
    /// 8. Disk usage reports non-zero blocks for files with content
    /// 9. Disk usage on nonexistent path throws
    /// </summary>
    public class DuTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _imagePath;

        public DuTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"ufs2test_du_src_{Guid.NewGuid():N}");
            _imagePath = Path.Combine(Path.GetTempPath(), $"ufs2test_du_{Guid.NewGuid():N}.img");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (File.Exists(_imagePath))
                File.Delete(_imagePath);
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }

        /// <summary>Helper to create a UFS2 image populated from _testDir.</summary>
        private void CreatePopulatedImage()
        {
            var creator = new Ufs2ImageCreator();
            var (_, diskSize, _) = Ufs2ImageCreator.CalculateDirectorySizes(_testDir, creator.BlockSize);
            long imageSize = creator.CalculateImageSize(diskSize);
            creator.CreateImage(_imagePath, imageSize);
            creator.PopulateFromDirectory(_imagePath, _testDir);
        }

        /// <summary>Helper to create a UFS1 image populated from _testDir.</summary>
        private void CreatePopulatedUfs1Image()
        {
            var creator = new Ufs2ImageCreator { FilesystemFormat = 1 };
            var (_, diskSize, _) = Ufs2ImageCreator.CalculateDirectorySizes(_testDir, creator.BlockSize);
            long imageSize = creator.CalculateImageSize(diskSize);
            creator.CreateImage(_imagePath, imageSize);
            creator.PopulateFromDirectory(_imagePath, _testDir);
        }

        // -----------------------------------------------------------------------
        // Disk usage of root directory (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void DiskUsage_RootDirectory_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "file1.txt"), "hello");
            File.WriteAllText(Path.Combine(_testDir, "file2.txt"), "world");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var entries = image.DiskUsage("/");

            // Should have at least the root directory entry
            Assert.NotEmpty(entries);
            var root = entries.First(e => e.Path == "/");
            Assert.True(root.IsDirectory);
            Assert.True(root.Blocks > 0);
            Assert.True(root.Bytes > 0);
        }

        // -----------------------------------------------------------------------
        // Disk usage of a specific subdirectory (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void DiskUsage_Subdirectory_Ufs2()
        {
            string subdir = Path.Combine(_testDir, "sub");
            Directory.CreateDirectory(subdir);
            File.WriteAllText(Path.Combine(subdir, "inner.txt"), "inner content");
            File.WriteAllText(Path.Combine(_testDir, "top.txt"), "top level");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var entries = image.DiskUsage("/sub");

            Assert.Single(entries);
            Assert.Equal("/sub", entries[0].Path);
            Assert.True(entries[0].IsDirectory);
            Assert.True(entries[0].Blocks > 0);
        }

        // -----------------------------------------------------------------------
        // Disk usage of a single file (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void DiskUsage_SingleFile_Ufs2()
        {
            string content = "Hello, disk usage test!";
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), content);
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var entries = image.DiskUsage("/test.txt");

            Assert.Single(entries);
            Assert.Equal("/test.txt", entries[0].Path);
            Assert.False(entries[0].IsDirectory);
            Assert.Equal(content.Length, entries[0].Bytes);
            Assert.True(entries[0].Blocks > 0);
        }

        // -----------------------------------------------------------------------
        // Disk usage with summary only (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void DiskUsage_SummaryOnly_Ufs2()
        {
            string subdir = Path.Combine(_testDir, "dir1");
            Directory.CreateDirectory(subdir);
            File.WriteAllText(Path.Combine(subdir, "a.txt"), "aaa");
            string subdir2 = Path.Combine(_testDir, "dir2");
            Directory.CreateDirectory(subdir2);
            File.WriteAllText(Path.Combine(subdir2, "b.txt"), "bbb");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var entries = image.DiskUsage("/", summaryOnly: true);

            // Summary should return exactly one entry — the root
            Assert.Single(entries);
            Assert.Equal("/", entries[0].Path);
            Assert.True(entries[0].IsDirectory);
        }

        // -----------------------------------------------------------------------
        // Disk usage with max depth (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void DiskUsage_MaxDepth_Ufs2()
        {
            string deep = Path.Combine(_testDir, "a", "b", "c");
            Directory.CreateDirectory(deep);
            File.WriteAllText(Path.Combine(deep, "deep.txt"), "deep");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);

            // depth=0 means only the root
            var entries0 = image.DiskUsage("/", maxDepth: 0);
            Assert.Single(entries0);
            Assert.Equal("/", entries0[0].Path);

            // depth=1 means root + immediate subdirectories
            var entries1 = image.DiskUsage("/", maxDepth: 1);
            Assert.True(entries1.Count >= 2); // root + /a at minimum
            Assert.Contains(entries1, e => e.Path == "/");
            Assert.Contains(entries1, e => e.Path == "/a");
            Assert.DoesNotContain(entries1, e => e.Path == "/a/b");

            // Unlimited depth should find all directories
            var entriesAll = image.DiskUsage("/");
            Assert.Contains(entriesAll, e => e.Path == "/a/b/c");
        }

        // -----------------------------------------------------------------------
        // Disk usage on a UFS1 image
        // -----------------------------------------------------------------------

        [Fact]
        public void DiskUsage_RootDirectory_Ufs1()
        {
            File.WriteAllText(Path.Combine(_testDir, "file.txt"), "ufs1 content");
            CreatePopulatedUfs1Image();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var entries = image.DiskUsage("/");

            Assert.NotEmpty(entries);
            var root = entries.First(e => e.Path == "/");
            Assert.True(root.IsDirectory);
            Assert.True(root.Blocks > 0);
        }

        // -----------------------------------------------------------------------
        // Disk usage on empty filesystem
        // -----------------------------------------------------------------------

        [Fact]
        public void DiskUsage_EmptyFilesystem_Ufs2()
        {
            // Create image with no files
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var entries = image.DiskUsage("/");

            // Should still have root entry with its directory overhead
            Assert.NotEmpty(entries);
            var root = entries.First(e => e.Path == "/");
            Assert.True(root.IsDirectory);
            Assert.True(root.Blocks >= 0);
            Assert.Equal(0, root.Bytes); // No files, so logical size should be 0
        }

        // -----------------------------------------------------------------------
        // Disk usage reports non-zero blocks for files with content
        // -----------------------------------------------------------------------

        [Fact]
        public void DiskUsage_BlocksAccumulated_Ufs2()
        {
            // Create a file large enough to require at least a full fragment
            byte[] data = new byte[8192];
            Array.Fill<byte>(data, 0x42);
            File.WriteAllBytes(Path.Combine(_testDir, "large.bin"), data);
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var entries = image.DiskUsage("/");

            var root = entries.First(e => e.Path == "/");
            // 8192 bytes of content + directory overhead → blocks > 0
            Assert.True(root.Blocks > 0);
            Assert.True(root.Bytes >= 8192);
        }

        // -----------------------------------------------------------------------
        // Disk usage on nonexistent path throws
        // -----------------------------------------------------------------------

        [Fact]
        public void DiskUsage_NonexistentPath_Throws()
        {
            File.WriteAllText(Path.Combine(_testDir, "file.txt"), "data");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            Assert.Throws<FileNotFoundException>(() => image.DiskUsage("/nonexistent"));
        }
    }
}
