// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.IO;
using UFS2Tool;

namespace UFS2Tool.Tests
{
    /// <summary>
    /// Tests for the stat functionality:
    /// 1. Stat the root directory of a UFS2 image
    /// 2. Stat a regular file in a UFS2 image
    /// 3. Stat a subdirectory in a UFS2 image
    /// 4. Stat the root directory of a UFS1 image
    /// 5. Stat a regular file in a UFS1 image
    /// 6. Stat after chmod to verify updated permissions
    /// 7. Verify stat on nonexistent path throws
    /// </summary>
    public class StatTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _imagePath;

        public StatTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"ufs2test_stat_src_{Guid.NewGuid():N}");
            _imagePath = Path.Combine(Path.GetTempPath(), $"ufs2test_stat_{Guid.NewGuid():N}.img");
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
        // Stat root directory (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Stat_RootDirectory_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "file.txt"), "data");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            string stat = image.GetStat("/");

            Assert.Contains("File: /", stat);
            Assert.Contains("Type: Directory", stat);
            Assert.Contains("Inode: 2", stat);
            Assert.Contains("d", stat); // symbolic mode starts with 'd'
            Assert.Contains("Links:", stat);
            Assert.Contains("Size:", stat);
            Assert.Contains("Blocks:", stat);
            Assert.Contains("Access:", stat);
            Assert.Contains("Modify:", stat);
            Assert.Contains("Change:", stat);
            Assert.Contains("Birth:", stat);
            Assert.Contains("Direct:", stat);
        }

        // -----------------------------------------------------------------------
        // Stat regular file (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Stat_RegularFile_Ufs2()
        {
            string content = "Hello, UFS2!";
            File.WriteAllText(Path.Combine(_testDir, "hello.txt"), content);
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            string stat = image.GetStat("/hello.txt");

            Assert.Contains("File: /hello.txt", stat);
            Assert.Contains("Type: Regular File", stat);
            Assert.Contains("Mode:", stat);
            Assert.Contains("Links: 1", stat);
            Assert.Contains($"Size: {content.Length}", stat);
            // Mode should show regular file indicator '-'
            Assert.Contains("-r", stat);
        }

        // -----------------------------------------------------------------------
        // Stat subdirectory (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Stat_Subdirectory_Ufs2()
        {
            string subdir = Path.Combine(_testDir, "mydir");
            Directory.CreateDirectory(subdir);
            File.WriteAllText(Path.Combine(subdir, "inner.txt"), "inner");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            string stat = image.GetStat("/mydir");

            Assert.Contains("File: /mydir", stat);
            Assert.Contains("Type: Directory", stat);
            Assert.Contains("Links: 2", stat); // . and parent entry
        }

        // -----------------------------------------------------------------------
        // Stat root directory (UFS1)
        // -----------------------------------------------------------------------

        [Fact]
        public void Stat_RootDirectory_Ufs1()
        {
            File.WriteAllText(Path.Combine(_testDir, "file.txt"), "data");
            CreatePopulatedUfs1Image();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            string stat = image.GetStat("/");

            Assert.Contains("File: /", stat);
            Assert.Contains("Type: Directory", stat);
            Assert.Contains("Inode: 2", stat);
        }

        // -----------------------------------------------------------------------
        // Stat regular file (UFS1)
        // -----------------------------------------------------------------------

        [Fact]
        public void Stat_RegularFile_Ufs1()
        {
            string content = "Hello, UFS1!";
            File.WriteAllText(Path.Combine(_testDir, "hello.txt"), content);
            CreatePopulatedUfs1Image();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            string stat = image.GetStat("/hello.txt");

            Assert.Contains("File: /hello.txt", stat);
            Assert.Contains("Type: Regular File", stat);
            Assert.Contains($"Size: {content.Length}", stat);
        }

        // -----------------------------------------------------------------------
        // Stat after chmod (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Stat_AfterChmod_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "chmod test");
            CreatePopulatedImage();

            // Chmod to 0755
            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Chmod("/test.txt", 0x1ED); // 0755 octal = 0x1ED
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                string stat = image.GetStat("/test.txt");
                Assert.Contains("0755", stat);
                Assert.Contains("-rwxr-xr-x", stat);
            }
        }

        // -----------------------------------------------------------------------
        // Stat nonexistent path throws
        // -----------------------------------------------------------------------

        [Fact]
        public void Stat_NonexistentPath_Throws()
        {
            File.WriteAllText(Path.Combine(_testDir, "file.txt"), "data");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            Assert.Throws<FileNotFoundException>(() => image.GetStat("/nonexistent.txt"));
        }
    }
}
