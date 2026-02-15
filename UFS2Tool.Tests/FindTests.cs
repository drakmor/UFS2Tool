// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.IO;
using System.Linq;
using UFS2Tool;

namespace UFS2Tool.Tests
{
    /// <summary>
    /// Tests for the find functionality:
    /// 1. Find files by exact name in a UFS2 image
    /// 2. Find files by wildcard pattern (*.txt)
    /// 3. Find with type filter (files only, directories only)
    /// 4. Find in a subdirectory using -path
    /// 5. Find in a deep directory tree
    /// 6. Find with no matches returns empty
    /// 7. Find with single character wildcard (?)
    /// 8. Find in a UFS1 image
    /// 9. Find all entries (pattern "*")
    /// </summary>
    public class FindTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _imagePath;

        public FindTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"ufs2test_find_src_{Guid.NewGuid():N}");
            _imagePath = Path.Combine(Path.GetTempPath(), $"ufs2test_find_{Guid.NewGuid():N}.img");
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
        // Find by exact name (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Find_ExactName_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "target.txt"), "found me");
            File.WriteAllText(Path.Combine(_testDir, "other.bin"), "not this");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var results = image.Find("target.txt");

            Assert.Single(results);
            Assert.Equal("/target.txt", results[0].Path);
            Assert.Equal(Ufs2Constants.DtReg, results[0].FileType);
            Assert.Equal(8, results[0].Size); // "found me" = 8 bytes
        }

        // -----------------------------------------------------------------------
        // Find by wildcard pattern *.txt (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Find_WildcardPattern_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "readme.txt"), "hello");
            File.WriteAllText(Path.Combine(_testDir, "notes.txt"), "world");
            File.WriteAllText(Path.Combine(_testDir, "data.bin"), "binary");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var results = image.Find("*.txt");

            Assert.Equal(2, results.Count);
            var names = results.Select(r => r.Path).OrderBy(p => p).ToList();
            Assert.Contains("/notes.txt", names);
            Assert.Contains("/readme.txt", names);
        }

        // -----------------------------------------------------------------------
        // Find with type filter: files only
        // -----------------------------------------------------------------------

        [Fact]
        public void Find_TypeFilterFiles_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "mydir"));
            File.WriteAllText(Path.Combine(_testDir, "mydir", "inner.txt"), "inner");
            File.WriteAllText(Path.Combine(_testDir, "top.txt"), "top");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);

            // Search for everything named "*" but filter to files only
            var filesOnly = image.Find("*", "/", "f");
            Assert.True(filesOnly.All(r => r.FileType == Ufs2Constants.DtReg));
            Assert.Contains(filesOnly, r => r.Path == "/top.txt");
            Assert.Contains(filesOnly, r => r.Path == "/mydir/inner.txt");
            // mydir itself should not appear
            Assert.DoesNotContain(filesOnly, r => r.Path == "/mydir");
        }

        // -----------------------------------------------------------------------
        // Find with type filter: directories only
        // -----------------------------------------------------------------------

        [Fact]
        public void Find_TypeFilterDirectories_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "dir1"));
            Directory.CreateDirectory(Path.Combine(_testDir, "dir2"));
            File.WriteAllText(Path.Combine(_testDir, "file.txt"), "data");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var dirsOnly = image.Find("*", "/", "d");

            Assert.True(dirsOnly.All(r => r.FileType == Ufs2Constants.DtDir));
            var dirPaths = dirsOnly.Select(r => r.Path).ToList();
            Assert.Contains("/dir1", dirPaths);
            Assert.Contains("/dir2", dirPaths);
            Assert.DoesNotContain("/file.txt", dirPaths);
        }

        // -----------------------------------------------------------------------
        // Find starting from a subdirectory
        // -----------------------------------------------------------------------

        [Fact]
        public void Find_StartFromSubdirectory_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "sub"));
            File.WriteAllText(Path.Combine(_testDir, "sub", "deep.txt"), "deep");
            File.WriteAllText(Path.Combine(_testDir, "top.txt"), "top");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var results = image.Find("*.txt", "/sub");

            Assert.Single(results);
            Assert.Equal("/sub/deep.txt", results[0].Path);
        }

        // -----------------------------------------------------------------------
        // Find in deep directory tree
        // -----------------------------------------------------------------------

        [Fact]
        public void Find_DeepTree_Ufs2()
        {
            string deep = Path.Combine(_testDir, "a", "b", "c");
            Directory.CreateDirectory(deep);
            File.WriteAllText(Path.Combine(deep, "target.dat"), "found");
            File.WriteAllText(Path.Combine(_testDir, "a", "other.dat"), "other");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var results = image.Find("*.dat");

            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.Path == "/a/b/c/target.dat");
            Assert.Contains(results, r => r.Path == "/a/other.dat");
        }

        // -----------------------------------------------------------------------
        // Find with no matches
        // -----------------------------------------------------------------------

        [Fact]
        public void Find_NoMatches_ReturnsEmpty()
        {
            File.WriteAllText(Path.Combine(_testDir, "file.txt"), "data");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var results = image.Find("*.xyz");

            Assert.Empty(results);
        }

        // -----------------------------------------------------------------------
        // Find with single character wildcard (?)
        // -----------------------------------------------------------------------

        [Fact]
        public void Find_SingleCharWildcard_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "a1.txt"), "one");
            File.WriteAllText(Path.Combine(_testDir, "a2.txt"), "two");
            File.WriteAllText(Path.Combine(_testDir, "ab.txt"), "ab");
            File.WriteAllText(Path.Combine(_testDir, "abc.txt"), "abc");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var results = image.Find("a?.txt");

            Assert.Equal(3, results.Count);
            var names = results.Select(r => r.Path).OrderBy(p => p).ToList();
            Assert.Contains("/a1.txt", names);
            Assert.Contains("/a2.txt", names);
            Assert.Contains("/ab.txt", names);
            // abc.txt has 3 chars before .txt, so "a?" won't match "abc"
            Assert.DoesNotContain("/abc.txt", names);
        }

        // -----------------------------------------------------------------------
        // Find in a UFS1 image
        // -----------------------------------------------------------------------

        [Fact]
        public void Find_WildcardPattern_Ufs1()
        {
            File.WriteAllText(Path.Combine(_testDir, "hello.txt"), "hello ufs1");
            File.WriteAllText(Path.Combine(_testDir, "data.bin"), "binary");
            CreatePopulatedUfs1Image();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var results = image.Find("*.txt");

            Assert.Single(results);
            Assert.Equal("/hello.txt", results[0].Path);
            Assert.Equal(10, results[0].Size); // "hello ufs1" = 10 bytes
        }

        // -----------------------------------------------------------------------
        // Find all entries with "*"
        // -----------------------------------------------------------------------

        [Fact]
        public void Find_AllEntries_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "mydir"));
            File.WriteAllText(Path.Combine(_testDir, "mydir", "child.txt"), "child");
            File.WriteAllText(Path.Combine(_testDir, "root.txt"), "root");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var results = image.Find("*");

            // Should find: .snap (or not, depending), mydir, mydir/child.txt, root.txt
            Assert.True(results.Count >= 3);
            Assert.Contains(results, r => r.Path == "/mydir");
            Assert.Contains(results, r => r.Path == "/mydir/child.txt");
            Assert.Contains(results, r => r.Path == "/root.txt");
        }

        // -----------------------------------------------------------------------
        // Find is case-insensitive
        // -----------------------------------------------------------------------

        [Fact]
        public void Find_CaseInsensitive_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "README.txt"), "readme");
            File.WriteAllText(Path.Combine(_testDir, "notes.TXT"), "notes");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var results = image.Find("*.txt");

            Assert.Equal(2, results.Count);
            var paths = results.Select(r => r.Path).ToList();
            Assert.Contains("/README.txt", paths);
            Assert.Contains("/notes.TXT", paths);
        }
    }
}
