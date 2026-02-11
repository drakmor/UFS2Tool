// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.IO;
using UFS2Tool;

namespace UFS2Tool.Tests
{
    /// <summary>
    /// Tests for the extract functionality:
    /// 1. Extract the entire content of a UFS1 or UFS2 filesystem image
    /// 2. Extract a single file or folder from a UFS1 or UFS2 filesystem image
    /// </summary>
    public class ExtractTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _imagePath;
        private readonly string _outputDir;

        public ExtractTests()
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
        // ResolvePath tests
        // -----------------------------------------------------------------------

        [Fact]
        public void ResolvePath_RootReturnsRootInode()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "hello");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            uint inode = image.ResolvePath("/");
            Assert.Equal((uint)Ufs2Constants.RootInode, inode);
        }

        [Fact]
        public void ResolvePath_EmptyStringReturnsRootInode()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "hello");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            uint inode = image.ResolvePath("");
            Assert.Equal((uint)Ufs2Constants.RootInode, inode);
        }

        [Fact]
        public void ResolvePath_FindsFileInRoot()
        {
            File.WriteAllText(Path.Combine(_testDir, "myfile.txt"), "data");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            uint inode = image.ResolvePath("/myfile.txt");
            Assert.NotEqual(0u, inode);

            var inodeData = image.ReadInode(inode);
            Assert.True(inodeData.IsRegularFile);
        }

        [Fact]
        public void ResolvePath_FindsNestedPath()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "a", "b"));
            File.WriteAllText(Path.Combine(_testDir, "a", "b", "deep.txt"), "deep");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            uint inode = image.ResolvePath("/a/b/deep.txt");
            Assert.NotEqual(0u, inode);
        }

        [Fact]
        public void ResolvePath_ThrowsOnMissingPath()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "data");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            Assert.Throws<FileNotFoundException>(() => image.ResolvePath("/nonexistent.txt"));
        }

        // -----------------------------------------------------------------------
        // Extract entire filesystem (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Extract_EntireUfs2Filesystem()
        {
            string content = "Hello UFS2 extraction!";
            File.WriteAllText(Path.Combine(_testDir, "root.txt"), content);
            Directory.CreateDirectory(Path.Combine(_testDir, "sub"));
            File.WriteAllText(Path.Combine(_testDir, "sub", "inner.txt"), "inner");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            image.Extract("/", _outputDir);

            Assert.True(File.Exists(Path.Combine(_outputDir, "root.txt")));
            Assert.Equal(content, File.ReadAllText(Path.Combine(_outputDir, "root.txt")));
            Assert.True(Directory.Exists(Path.Combine(_outputDir, "sub")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "sub", "inner.txt")));
            Assert.Equal("inner", File.ReadAllText(Path.Combine(_outputDir, "sub", "inner.txt")));
        }

        // -----------------------------------------------------------------------
        // Extract entire filesystem (UFS1)
        // -----------------------------------------------------------------------

        [Fact]
        public void Extract_EntireUfs1Filesystem()
        {
            string content = "Hello UFS1 extraction!";
            File.WriteAllText(Path.Combine(_testDir, "root.txt"), content);
            Directory.CreateDirectory(Path.Combine(_testDir, "sub"));
            File.WriteAllText(Path.Combine(_testDir, "sub", "inner.txt"), "inner");
            CreatePopulatedUfs1Image();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            image.Extract("/", _outputDir);

            Assert.True(File.Exists(Path.Combine(_outputDir, "root.txt")));
            Assert.Equal(content, File.ReadAllText(Path.Combine(_outputDir, "root.txt")));
            Assert.True(Directory.Exists(Path.Combine(_outputDir, "sub")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "sub", "inner.txt")));
            Assert.Equal("inner", File.ReadAllText(Path.Combine(_outputDir, "sub", "inner.txt")));
        }

        // -----------------------------------------------------------------------
        // Extract a single file
        // -----------------------------------------------------------------------

        [Fact]
        public void Extract_SingleFile()
        {
            byte[] data = new byte[50_000];
            new Random(123).NextBytes(data);
            File.WriteAllBytes(Path.Combine(_testDir, "binary.dat"), data);
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            image.Extract("/binary.dat", _outputDir);

            string extractedPath = Path.Combine(_outputDir, "binary.dat");
            Assert.True(File.Exists(extractedPath));
            byte[] extracted = File.ReadAllBytes(extractedPath);
            Assert.Equal(data, extracted);
        }

        [Fact]
        public void Extract_SingleFileFromSubdirectory()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "mydir"));
            File.WriteAllText(Path.Combine(_testDir, "mydir", "note.txt"), "note content");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            image.Extract("/mydir/note.txt", _outputDir);

            string extractedPath = Path.Combine(_outputDir, "note.txt");
            Assert.True(File.Exists(extractedPath));
            Assert.Equal("note content", File.ReadAllText(extractedPath));
        }

        // -----------------------------------------------------------------------
        // Extract a single folder
        // -----------------------------------------------------------------------

        [Fact]
        public void Extract_SingleFolder()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "folder"));
            File.WriteAllText(Path.Combine(_testDir, "folder", "a.txt"), "aaa");
            File.WriteAllText(Path.Combine(_testDir, "folder", "b.txt"), "bbb");
            // Also create a file in root that should NOT be extracted
            File.WriteAllText(Path.Combine(_testDir, "root_only.txt"), "root");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            image.Extract("/folder", _outputDir);

            Assert.True(File.Exists(Path.Combine(_outputDir, "a.txt")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "b.txt")));
            Assert.Equal("aaa", File.ReadAllText(Path.Combine(_outputDir, "a.txt")));
            Assert.Equal("bbb", File.ReadAllText(Path.Combine(_outputDir, "b.txt")));
            // Root file should not be in the output
            Assert.False(File.Exists(Path.Combine(_outputDir, "root_only.txt")));
        }

        [Fact]
        public void Extract_NestedFolderRecursively()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "a", "b", "c"));
            File.WriteAllText(Path.Combine(_testDir, "a", "b", "c", "leaf.txt"), "leaf");
            File.WriteAllText(Path.Combine(_testDir, "a", "b", "mid.txt"), "mid");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            image.Extract("/a", _outputDir);

            Assert.True(Directory.Exists(Path.Combine(_outputDir, "b", "c")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "b", "c", "leaf.txt")));
            Assert.Equal("leaf", File.ReadAllText(Path.Combine(_outputDir, "b", "c", "leaf.txt")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "b", "mid.txt")));
            Assert.Equal("mid", File.ReadAllText(Path.Combine(_outputDir, "b", "mid.txt")));
        }

        // -----------------------------------------------------------------------
        // Binary content preservation
        // -----------------------------------------------------------------------

        [Fact]
        public void Extract_PreservesBinaryContent()
        {
            byte[] original = new byte[100_000];
            new Random(42).NextBytes(original);
            File.WriteAllBytes(Path.Combine(_testDir, "data.bin"), original);
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            image.Extract("/", _outputDir);

            byte[] extracted = File.ReadAllBytes(Path.Combine(_outputDir, "data.bin"));
            Assert.Equal(original.Length, extracted.Length);
            Assert.Equal(original, extracted);
        }

        // -----------------------------------------------------------------------
        // UFS1 single file/folder extraction
        // -----------------------------------------------------------------------

        [Fact]
        public void Extract_SingleFile_Ufs1()
        {
            File.WriteAllText(Path.Combine(_testDir, "ufs1file.txt"), "ufs1 content");
            CreatePopulatedUfs1Image();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            image.Extract("/ufs1file.txt", _outputDir);

            string extractedPath = Path.Combine(_outputDir, "ufs1file.txt");
            Assert.True(File.Exists(extractedPath));
            Assert.Equal("ufs1 content", File.ReadAllText(extractedPath));
        }

        [Fact]
        public void Extract_SingleFolder_Ufs1()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "dir"));
            File.WriteAllText(Path.Combine(_testDir, "dir", "file.txt"), "content");
            CreatePopulatedUfs1Image();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            image.Extract("/dir", _outputDir);

            Assert.True(File.Exists(Path.Combine(_outputDir, "file.txt")));
            Assert.Equal("content", File.ReadAllText(Path.Combine(_outputDir, "file.txt")));
        }
    }
}
