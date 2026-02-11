// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.IO;
using UFS2Tool;

namespace UFS2Tool.Tests
{
    /// <summary>
    /// Tests for the add and delete functionality:
    /// 1. Add a single file to a UFS1 or UFS2 filesystem image
    /// 2. Add a directory (also recursive) to a UFS1 or UFS2 filesystem image
    /// 3. Delete a single file from a UFS1 or UFS2 filesystem image
    /// 4. Delete a directory (also recursive) from a UFS1 or UFS2 filesystem image
    /// </summary>
    public class AddDeleteTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _imagePath;
        private readonly string _outputDir;

        public AddDeleteTests()
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
        // Add a single file (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Add_SingleFile_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "existing.txt"), "existing content");
            CreatePopulatedImage();

            string newFile = Path.Combine(Path.GetTempPath(), $"ufs2test_add_{Guid.NewGuid():N}.txt");
            try
            {
                File.WriteAllText(newFile, "new file content");

                using (var image = new Ufs2Image(_imagePath, readOnly: false))
                {
                    image.Add("/newfile.txt", newFile);
                }

                using (var image = new Ufs2Image(_imagePath, readOnly: true))
                {
                    image.Extract("/", _outputDir);
                }

                Assert.True(File.Exists(Path.Combine(_outputDir, "newfile.txt")));
                Assert.Equal("new file content", File.ReadAllText(Path.Combine(_outputDir, "newfile.txt")));
                // Existing file should still be there
                Assert.True(File.Exists(Path.Combine(_outputDir, "existing.txt")));
                Assert.Equal("existing content", File.ReadAllText(Path.Combine(_outputDir, "existing.txt")));
            }
            finally
            {
                if (File.Exists(newFile))
                    File.Delete(newFile);
            }
        }

        // -----------------------------------------------------------------------
        // Add a single file (UFS1)
        // -----------------------------------------------------------------------

        [Fact]
        public void Add_SingleFile_Ufs1()
        {
            File.WriteAllText(Path.Combine(_testDir, "existing.txt"), "existing");
            CreatePopulatedUfs1Image();

            string newFile = Path.Combine(Path.GetTempPath(), $"ufs2test_add_{Guid.NewGuid():N}.txt");
            try
            {
                File.WriteAllText(newFile, "ufs1 new file");

                using (var image = new Ufs2Image(_imagePath, readOnly: false))
                {
                    image.Add("/newfile.txt", newFile);
                }

                using (var image = new Ufs2Image(_imagePath, readOnly: true))
                {
                    image.Extract("/", _outputDir);
                }

                Assert.True(File.Exists(Path.Combine(_outputDir, "newfile.txt")));
                Assert.Equal("ufs1 new file", File.ReadAllText(Path.Combine(_outputDir, "newfile.txt")));
            }
            finally
            {
                if (File.Exists(newFile))
                    File.Delete(newFile);
            }
        }

        // -----------------------------------------------------------------------
        // Add a file in a subdirectory
        // -----------------------------------------------------------------------

        [Fact]
        public void Add_FileInSubdirectory_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "subdir"));
            File.WriteAllText(Path.Combine(_testDir, "subdir", "old.txt"), "old content");
            CreatePopulatedImage();

            string newFile = Path.Combine(Path.GetTempPath(), $"ufs2test_add_{Guid.NewGuid():N}.txt");
            try
            {
                File.WriteAllText(newFile, "added content");

                using (var image = new Ufs2Image(_imagePath, readOnly: false))
                {
                    image.AddFile("/subdir", newFile);
                }

                using (var image = new Ufs2Image(_imagePath, readOnly: true))
                {
                    image.Extract("/subdir", _outputDir);
                }

                string addedFileName = Path.GetFileName(newFile);
                Assert.True(File.Exists(Path.Combine(_outputDir, addedFileName)));
                Assert.Equal("added content", File.ReadAllText(Path.Combine(_outputDir, addedFileName)));
                Assert.True(File.Exists(Path.Combine(_outputDir, "old.txt")));
            }
            finally
            {
                if (File.Exists(newFile))
                    File.Delete(newFile);
            }
        }

        // -----------------------------------------------------------------------
        // Add an empty directory
        // -----------------------------------------------------------------------

        [Fact]
        public void Add_EmptyDirectory_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "root.txt"), "root");
            CreatePopulatedImage();

            string newDir = Path.Combine(Path.GetTempPath(), $"ufs2test_adddir_{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(newDir);

                using (var image = new Ufs2Image(_imagePath, readOnly: false))
                {
                    image.Add("/emptydir", newDir);
                }

                using (var image = new Ufs2Image(_imagePath, readOnly: true))
                {
                    var entries = image.ListDirectory(image.ResolvePath("/emptydir"));
                    // Should have "." and ".." only
                    var names = entries.Select(e => e.Name).ToList();
                    Assert.Contains(".", names);
                    Assert.Contains("..", names);
                    Assert.Equal(2, entries.Count);
                }
            }
            finally
            {
                if (Directory.Exists(newDir))
                    Directory.Delete(newDir, recursive: true);
            }
        }

        // -----------------------------------------------------------------------
        // Add a directory with files (recursive)
        // -----------------------------------------------------------------------

        [Fact]
        public void Add_DirectoryWithFiles_Recursive_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "root.txt"), "root");
            CreatePopulatedImage();

            string newDir = Path.Combine(Path.GetTempPath(), $"ufs2test_adddir_{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(newDir);
                File.WriteAllText(Path.Combine(newDir, "a.txt"), "content_a");
                File.WriteAllText(Path.Combine(newDir, "b.txt"), "content_b");
                Directory.CreateDirectory(Path.Combine(newDir, "sub"));
                File.WriteAllText(Path.Combine(newDir, "sub", "c.txt"), "content_c");

                using (var image = new Ufs2Image(_imagePath, readOnly: false))
                {
                    image.Add("/newdir", newDir);
                }

                using (var image = new Ufs2Image(_imagePath, readOnly: true))
                {
                    image.Extract("/newdir", _outputDir);
                }

                Assert.True(File.Exists(Path.Combine(_outputDir, "a.txt")));
                Assert.Equal("content_a", File.ReadAllText(Path.Combine(_outputDir, "a.txt")));
                Assert.True(File.Exists(Path.Combine(_outputDir, "b.txt")));
                Assert.Equal("content_b", File.ReadAllText(Path.Combine(_outputDir, "b.txt")));
                Assert.True(Directory.Exists(Path.Combine(_outputDir, "sub")));
                Assert.True(File.Exists(Path.Combine(_outputDir, "sub", "c.txt")));
                Assert.Equal("content_c", File.ReadAllText(Path.Combine(_outputDir, "sub", "c.txt")));
            }
            finally
            {
                if (Directory.Exists(newDir))
                    Directory.Delete(newDir, recursive: true);
            }
        }

        // -----------------------------------------------------------------------
        // Add with binary content
        // -----------------------------------------------------------------------

        [Fact]
        public void Add_PreservesBinaryContent_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "existing.txt"), "x");
            CreatePopulatedImage();

            string newFile = Path.Combine(Path.GetTempPath(), $"ufs2test_add_{Guid.NewGuid():N}.bin");
            try
            {
                byte[] data = new byte[50_000];
                new Random(42).NextBytes(data);
                File.WriteAllBytes(newFile, data);

                using (var image = new Ufs2Image(_imagePath, readOnly: false))
                {
                    image.Add("/binary.bin", newFile);
                }

                using (var image = new Ufs2Image(_imagePath, readOnly: true))
                {
                    image.Extract("/binary.bin", _outputDir);
                }

                byte[] extracted = File.ReadAllBytes(Path.Combine(_outputDir, "binary.bin"));
                Assert.Equal(data.Length, extracted.Length);
                Assert.Equal(data, extracted);
            }
            finally
            {
                if (File.Exists(newFile))
                    File.Delete(newFile);
            }
        }

        // -----------------------------------------------------------------------
        // Add on read-only image should throw
        // -----------------------------------------------------------------------

        [Fact]
        public void Add_ThrowsOnReadOnlyImage()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "data");
            CreatePopulatedImage();

            string newFile = Path.Combine(Path.GetTempPath(), $"ufs2test_add_{Guid.NewGuid():N}.txt");
            try
            {
                File.WriteAllText(newFile, "new");

                using var image = new Ufs2Image(_imagePath, readOnly: true);
                Assert.Throws<InvalidOperationException>(() =>
                    image.Add("/newfile.txt", newFile));
            }
            finally
            {
                if (File.Exists(newFile))
                    File.Delete(newFile);
            }
        }

        // -----------------------------------------------------------------------
        // Delete a single file (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Delete_SingleFile_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "keep.txt"), "keep this");
            File.WriteAllText(Path.Combine(_testDir, "remove.txt"), "remove this");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Delete("/remove.txt");
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            Assert.True(File.Exists(Path.Combine(_outputDir, "keep.txt")));
            Assert.Equal("keep this", File.ReadAllText(Path.Combine(_outputDir, "keep.txt")));
            Assert.False(File.Exists(Path.Combine(_outputDir, "remove.txt")));
        }

        // -----------------------------------------------------------------------
        // Delete a single file (UFS1)
        // -----------------------------------------------------------------------

        [Fact]
        public void Delete_SingleFile_Ufs1()
        {
            File.WriteAllText(Path.Combine(_testDir, "keep.txt"), "keep");
            File.WriteAllText(Path.Combine(_testDir, "remove.txt"), "remove");
            CreatePopulatedUfs1Image();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Delete("/remove.txt");
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            Assert.True(File.Exists(Path.Combine(_outputDir, "keep.txt")));
            Assert.False(File.Exists(Path.Combine(_outputDir, "remove.txt")));
        }

        // -----------------------------------------------------------------------
        // Delete a file in a subdirectory
        // -----------------------------------------------------------------------

        [Fact]
        public void Delete_FileInSubdirectory_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "dir"));
            File.WriteAllText(Path.Combine(_testDir, "dir", "a.txt"), "aaa");
            File.WriteAllText(Path.Combine(_testDir, "dir", "b.txt"), "bbb");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Delete("/dir/a.txt");
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/dir", _outputDir);
            }

            Assert.False(File.Exists(Path.Combine(_outputDir, "a.txt")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "b.txt")));
            Assert.Equal("bbb", File.ReadAllText(Path.Combine(_outputDir, "b.txt")));
        }

        // -----------------------------------------------------------------------
        // Delete an empty directory
        // -----------------------------------------------------------------------

        [Fact]
        public void Delete_EmptyDirectory_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "emptydir"));
            File.WriteAllText(Path.Combine(_testDir, "root.txt"), "root");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Delete("/emptydir");
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            Assert.False(Directory.Exists(Path.Combine(_outputDir, "emptydir")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "root.txt")));
        }

        // -----------------------------------------------------------------------
        // Delete a directory with contents (recursive)
        // -----------------------------------------------------------------------

        [Fact]
        public void Delete_DirectoryWithContents_Recursive_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "mydir"));
            File.WriteAllText(Path.Combine(_testDir, "mydir", "file1.txt"), "content1");
            Directory.CreateDirectory(Path.Combine(_testDir, "mydir", "sub"));
            File.WriteAllText(Path.Combine(_testDir, "mydir", "sub", "file2.txt"), "content2");
            File.WriteAllText(Path.Combine(_testDir, "root.txt"), "root");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Delete("/mydir");
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            Assert.False(Directory.Exists(Path.Combine(_outputDir, "mydir")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "root.txt")));
            Assert.Equal("root", File.ReadAllText(Path.Combine(_outputDir, "root.txt")));
        }

        // -----------------------------------------------------------------------
        // Delete on read-only image should throw
        // -----------------------------------------------------------------------

        [Fact]
        public void Delete_ThrowsOnReadOnlyImage()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "data");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            Assert.Throws<InvalidOperationException>(() =>
                image.Delete("/test.txt"));
        }

        // -----------------------------------------------------------------------
        // Delete non-existent file should throw
        // -----------------------------------------------------------------------

        [Fact]
        public void Delete_ThrowsOnNonExistentPath()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "data");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: false);
            Assert.Throws<FileNotFoundException>(() =>
                image.Delete("/nonexistent.txt"));
        }

        // -----------------------------------------------------------------------
        // Add then delete round-trip
        // -----------------------------------------------------------------------

        [Fact]
        public void Add_Then_Delete_RoundTrip_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "existing.txt"), "existing");
            CreatePopulatedImage();

            string newFile = Path.Combine(Path.GetTempPath(), $"ufs2test_add_{Guid.NewGuid():N}.txt");
            try
            {
                File.WriteAllText(newFile, "temporary content");

                using (var image = new Ufs2Image(_imagePath, readOnly: false))
                {
                    image.Add("/temp.txt", newFile);
                }

                // Verify it was added
                using (var image = new Ufs2Image(_imagePath, readOnly: true))
                {
                    image.Extract("/", _outputDir);
                }
                Assert.True(File.Exists(Path.Combine(_outputDir, "temp.txt")));

                // Now delete it
                if (Directory.Exists(_outputDir))
                    Directory.Delete(_outputDir, recursive: true);

                using (var image = new Ufs2Image(_imagePath, readOnly: false))
                {
                    image.Delete("/temp.txt");
                }

                using (var image = new Ufs2Image(_imagePath, readOnly: true))
                {
                    image.Extract("/", _outputDir);
                }

                Assert.False(File.Exists(Path.Combine(_outputDir, "temp.txt")));
                Assert.True(File.Exists(Path.Combine(_outputDir, "existing.txt")));
            }
            finally
            {
                if (File.Exists(newFile))
                    File.Delete(newFile);
            }
        }

        // -----------------------------------------------------------------------
        // Fsck should pass after add
        // -----------------------------------------------------------------------

        [Fact]
        public void Add_FsckPassesAfterAdd_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "existing.txt"), "existing");
            CreatePopulatedImage();

            string newFile = Path.Combine(Path.GetTempPath(), $"ufs2test_add_{Guid.NewGuid():N}.txt");
            try
            {
                File.WriteAllText(newFile, "added content for fsck test");

                using (var image = new Ufs2Image(_imagePath, readOnly: false))
                {
                    image.Add("/fsck_test.txt", newFile);
                }

                using var checkImage = new Ufs2Image(_imagePath, readOnly: true);
                var result = checkImage.FsckUfs(preen: true);
                Assert.True(result.Clean, $"Fsck errors: {string.Join("; ", result.Errors)}");
            }
            finally
            {
                if (File.Exists(newFile))
                    File.Delete(newFile);
            }
        }

        // -----------------------------------------------------------------------
        // Fsck should pass after delete
        // -----------------------------------------------------------------------

        [Fact]
        public void Delete_FsckPassesAfterDelete_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "keep.txt"), "keep");
            File.WriteAllText(Path.Combine(_testDir, "remove.txt"), "remove for fsck test");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Delete("/remove.txt");
            }

            using var checkImage = new Ufs2Image(_imagePath, readOnly: true);
            var result = checkImage.FsckUfs(preen: true);
            Assert.True(result.Clean, $"Fsck errors: {string.Join("; ", result.Errors)}");
        }

        // -----------------------------------------------------------------------
        // Fsck should pass after add directory recursive
        // -----------------------------------------------------------------------

        [Fact]
        public void Add_Directory_FsckPassesAfterAdd_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "root.txt"), "root");
            CreatePopulatedImage();

            string newDir = Path.Combine(Path.GetTempPath(), $"ufs2test_adddir_{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(newDir);
                File.WriteAllText(Path.Combine(newDir, "f1.txt"), "file1");
                Directory.CreateDirectory(Path.Combine(newDir, "inner"));
                File.WriteAllText(Path.Combine(newDir, "inner", "f2.txt"), "file2");

                using (var image = new Ufs2Image(_imagePath, readOnly: false))
                {
                    image.Add("/added_dir", newDir);
                }

                using var checkImage = new Ufs2Image(_imagePath, readOnly: true);
                var result = checkImage.FsckUfs(preen: true);
                Assert.True(result.Clean, $"Fsck errors: {string.Join("; ", result.Errors)}");
            }
            finally
            {
                if (Directory.Exists(newDir))
                    Directory.Delete(newDir, recursive: true);
            }
        }

        // -----------------------------------------------------------------------
        // Fsck should pass after delete directory recursive
        // -----------------------------------------------------------------------

        [Fact]
        public void Delete_Directory_FsckPassesAfterDelete_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "deldir"));
            File.WriteAllText(Path.Combine(_testDir, "deldir", "file.txt"), "content");
            Directory.CreateDirectory(Path.Combine(_testDir, "deldir", "sub"));
            File.WriteAllText(Path.Combine(_testDir, "deldir", "sub", "nested.txt"), "nested");
            File.WriteAllText(Path.Combine(_testDir, "keep.txt"), "keep");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Delete("/deldir");
            }

            using var checkImage = new Ufs2Image(_imagePath, readOnly: true);
            var result = checkImage.FsckUfs(preen: true);
            Assert.True(result.Clean, $"Fsck errors: {string.Join("; ", result.Errors)}");
        }
    }
}
