// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.IO;
using System.Linq;
using UFS2Tool;

namespace UFS2Tool.Tests
{
    /// <summary>
    /// Tests for the rename functionality:
    /// 1. Rename a single file in a UFS2 filesystem image
    /// 2. Rename a single file in a UFS1 filesystem image
    /// 3. Rename a directory in a UFS2 filesystem image
    /// 4. Verify content is preserved after rename
    /// 5. Verify that renamed directories retain their contents
    /// </summary>
    public class RenameTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _imagePath;
        private readonly string _outputDir;

        public RenameTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"ufs2test_src_{Guid.NewGuid():N}");
            _imagePath = Path.Combine(Path.GetTempPath(), $"ufs2test_{Guid.NewGuid():N}.img");
            _outputDir = Path.Combine(Path.GetTempPath(), $"ufs2test_out_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (File.Exists(_imagePath))
                File.Delete(_imagePath);
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
            if (Directory.Exists(_outputDir))
                Directory.Delete(_outputDir, recursive: true);
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
        // Rename a single file (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Rename_SingleFile_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "original.txt"), "hello world");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Rename("/original.txt", "renamed.txt");
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            Assert.False(File.Exists(Path.Combine(_outputDir, "original.txt")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "renamed.txt")));
            Assert.Equal("hello world", File.ReadAllText(Path.Combine(_outputDir, "renamed.txt")));
        }

        // -----------------------------------------------------------------------
        // Rename a single file (UFS1)
        // -----------------------------------------------------------------------

        [Fact]
        public void Rename_SingleFile_Ufs1()
        {
            File.WriteAllText(Path.Combine(_testDir, "original.txt"), "ufs1 content");
            CreatePopulatedUfs1Image();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Rename("/original.txt", "renamed.txt");
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            Assert.False(File.Exists(Path.Combine(_outputDir, "original.txt")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "renamed.txt")));
            Assert.Equal("ufs1 content", File.ReadAllText(Path.Combine(_outputDir, "renamed.txt")));
        }

        // -----------------------------------------------------------------------
        // Rename a directory (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Rename_Directory_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "olddir"));
            File.WriteAllText(Path.Combine(_testDir, "olddir", "child.txt"), "child content");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Rename("/olddir", "newdir");
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            Assert.False(Directory.Exists(Path.Combine(_outputDir, "olddir")));
            Assert.True(Directory.Exists(Path.Combine(_outputDir, "newdir")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "newdir", "child.txt")));
            Assert.Equal("child content", File.ReadAllText(Path.Combine(_outputDir, "newdir", "child.txt")));
        }

        // -----------------------------------------------------------------------
        // Rename a file in a subdirectory (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Rename_FileInSubdirectory_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "subdir"));
            File.WriteAllText(Path.Combine(_testDir, "subdir", "old.txt"), "sub content");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Rename("/subdir/old.txt", "new.txt");
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/subdir", _outputDir);
            }

            Assert.False(File.Exists(Path.Combine(_outputDir, "old.txt")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "new.txt")));
            Assert.Equal("sub content", File.ReadAllText(Path.Combine(_outputDir, "new.txt")));
        }

        // -----------------------------------------------------------------------
        // Rename to same name is a no-op
        // -----------------------------------------------------------------------

        [Fact]
        public void Rename_SameName_NoOp()
        {
            File.WriteAllText(Path.Combine(_testDir, "file.txt"), "same name");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Rename("/file.txt", "file.txt");
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            Assert.True(File.Exists(Path.Combine(_outputDir, "file.txt")));
            Assert.Equal("same name", File.ReadAllText(Path.Combine(_outputDir, "file.txt")));
        }

        // -----------------------------------------------------------------------
        // Rename fails on read-only image
        // -----------------------------------------------------------------------

        [Fact]
        public void Rename_ReadOnly_Throws()
        {
            File.WriteAllText(Path.Combine(_testDir, "file.txt"), "content");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            Assert.Throws<InvalidOperationException>(() => image.Rename("/file.txt", "new.txt"));
        }

        // -----------------------------------------------------------------------
        // Rename with path separator in name throws
        // -----------------------------------------------------------------------

        [Fact]
        public void Rename_PathSeparatorInName_Throws()
        {
            File.WriteAllText(Path.Combine(_testDir, "file.txt"), "content");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: false);
            Assert.Throws<ArgumentException>(() => image.Rename("/file.txt", "sub/new.txt"));
        }

        // -----------------------------------------------------------------------
        // Rename with empty name throws
        // -----------------------------------------------------------------------

        [Fact]
        public void Rename_EmptyName_Throws()
        {
            File.WriteAllText(Path.Combine(_testDir, "file.txt"), "content");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: false);
            Assert.Throws<ArgumentException>(() => image.Rename("/file.txt", ""));
        }
    }
}
