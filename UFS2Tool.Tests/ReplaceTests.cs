// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.IO;
using UFS2Tool;

namespace UFS2Tool.Tests
{
    /// <summary>
    /// Tests for the replace functionality:
    /// 1. Replace a single file in a UFS1 or UFS2 filesystem image
    /// 2. Replace a directory (matching files) in a UFS1 or UFS2 filesystem image
    /// 3. Replace with same size, smaller, and larger content
    /// </summary>
    public class ReplaceTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _imagePath;
        private readonly string _outputDir;

        public ReplaceTests()
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
        // Replace a single file (UFS2) — same size
        // -----------------------------------------------------------------------

        [Fact]
        public void Replace_SingleFile_SameSize_Ufs2()
        {
            string original = "Hello Original!";
            string replacement = "Hello Replaced!";
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), original);
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.ReplaceFileContent("/test.txt", System.Text.Encoding.UTF8.GetBytes(replacement));
            }

            // Verify by extracting
            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            string extracted = File.ReadAllText(Path.Combine(_outputDir, "test.txt"));
            Assert.Equal(replacement, extracted);
        }

        // -----------------------------------------------------------------------
        // Replace a single file (UFS2) — smaller content
        // -----------------------------------------------------------------------

        [Fact]
        public void Replace_SingleFile_SmallerContent_Ufs2()
        {
            string original = "This is a long original content string for testing.";
            string replacement = "Short";
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), original);
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.ReplaceFileContent("/test.txt", System.Text.Encoding.UTF8.GetBytes(replacement));
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            string extracted = File.ReadAllText(Path.Combine(_outputDir, "test.txt"));
            Assert.Equal(replacement, extracted);
        }

        // -----------------------------------------------------------------------
        // Replace a single file (UFS2) — larger content
        // -----------------------------------------------------------------------

        [Fact]
        public void Replace_SingleFile_LargerContent_Ufs2()
        {
            string original = "Short";
            // Create a replacement that is larger
            string replacement = new string('X', 10000);
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), original);
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.ReplaceFileContent("/test.txt", System.Text.Encoding.UTF8.GetBytes(replacement));
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            string extracted = File.ReadAllText(Path.Combine(_outputDir, "test.txt"));
            Assert.Equal(replacement, extracted);
        }

        // -----------------------------------------------------------------------
        // Replace a single file (UFS1)
        // -----------------------------------------------------------------------

        [Fact]
        public void Replace_SingleFile_Ufs1()
        {
            string original = "UFS1 Original Content";
            string replacement = "UFS1 Replaced Content";
            File.WriteAllText(Path.Combine(_testDir, "ufs1file.txt"), original);
            CreatePopulatedUfs1Image();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.ReplaceFileContent("/ufs1file.txt", System.Text.Encoding.UTF8.GetBytes(replacement));
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            string extracted = File.ReadAllText(Path.Combine(_outputDir, "ufs1file.txt"));
            Assert.Equal(replacement, extracted);
        }

        // -----------------------------------------------------------------------
        // Replace using host file path (Replace method)
        // -----------------------------------------------------------------------

        [Fact]
        public void Replace_FileFromHostPath_Ufs2()
        {
            string original = "Original Data";
            string replacement = "Replaced Data";
            File.WriteAllText(Path.Combine(_testDir, "data.txt"), original);
            CreatePopulatedImage();

            string replacementFile = Path.Combine(Path.GetTempPath(), $"ufs2test_repl_{Guid.NewGuid():N}.txt");
            try
            {
                File.WriteAllText(replacementFile, replacement);

                using (var image = new Ufs2Image(_imagePath, readOnly: false))
                {
                    image.Replace("/data.txt", replacementFile);
                }

                using (var image = new Ufs2Image(_imagePath, readOnly: true))
                {
                    image.Extract("/", _outputDir);
                }

                string extracted = File.ReadAllText(Path.Combine(_outputDir, "data.txt"));
                Assert.Equal(replacement, extracted);
            }
            finally
            {
                if (File.Exists(replacementFile))
                    File.Delete(replacementFile);
            }
        }

        // -----------------------------------------------------------------------
        // Replace a directory (matching files)
        // -----------------------------------------------------------------------

        [Fact]
        public void Replace_Directory_MatchingFiles_Ufs2()
        {
            // Create original files
            Directory.CreateDirectory(Path.Combine(_testDir, "mydir"));
            File.WriteAllText(Path.Combine(_testDir, "mydir", "a.txt"), "original_a");
            File.WriteAllText(Path.Combine(_testDir, "mydir", "b.txt"), "original_b");
            File.WriteAllText(Path.Combine(_testDir, "root.txt"), "root_content");
            CreatePopulatedImage();

            // Create replacement directory
            string replDir = Path.Combine(Path.GetTempPath(), $"ufs2test_repldir_{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(replDir);
                File.WriteAllText(Path.Combine(replDir, "a.txt"), "replaced_a");
                // b.txt is not in the replacement dir, so it should remain unchanged

                using (var image = new Ufs2Image(_imagePath, readOnly: false))
                {
                    image.Replace("/mydir", replDir);
                }

                using (var image = new Ufs2Image(_imagePath, readOnly: true))
                {
                    image.Extract("/", _outputDir);
                }

                Assert.Equal("replaced_a", File.ReadAllText(Path.Combine(_outputDir, "mydir", "a.txt")));
                Assert.Equal("original_b", File.ReadAllText(Path.Combine(_outputDir, "mydir", "b.txt")));
                Assert.Equal("root_content", File.ReadAllText(Path.Combine(_outputDir, "root.txt")));
            }
            finally
            {
                if (Directory.Exists(replDir))
                    Directory.Delete(replDir, recursive: true);
            }
        }

        // -----------------------------------------------------------------------
        // Replace a directory recursively (UFS1)
        // -----------------------------------------------------------------------

        [Fact]
        public void Replace_Directory_MatchingFiles_Ufs1()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "dir"));
            File.WriteAllText(Path.Combine(_testDir, "dir", "file.txt"), "original");
            CreatePopulatedUfs1Image();

            string replDir = Path.Combine(Path.GetTempPath(), $"ufs2test_repldir_{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(replDir);
                File.WriteAllText(Path.Combine(replDir, "file.txt"), "replaced");

                using (var image = new Ufs2Image(_imagePath, readOnly: false))
                {
                    image.Replace("/dir", replDir);
                }

                using (var image = new Ufs2Image(_imagePath, readOnly: true))
                {
                    image.Extract("/", _outputDir);
                }

                Assert.Equal("replaced", File.ReadAllText(Path.Combine(_outputDir, "dir", "file.txt")));
            }
            finally
            {
                if (Directory.Exists(replDir))
                    Directory.Delete(replDir, recursive: true);
            }
        }

        // -----------------------------------------------------------------------
        // Replace with binary content
        // -----------------------------------------------------------------------

        [Fact]
        public void Replace_PreservesBinaryContent_Ufs2()
        {
            byte[] original = new byte[50_000];
            new Random(100).NextBytes(original);
            File.WriteAllBytes(Path.Combine(_testDir, "binary.dat"), original);
            CreatePopulatedImage();

            byte[] replacement = new byte[50_000];
            new Random(200).NextBytes(replacement);

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.ReplaceFileContent("/binary.dat", replacement);
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            byte[] extracted = File.ReadAllBytes(Path.Combine(_outputDir, "binary.dat"));
            Assert.Equal(replacement.Length, extracted.Length);
            Assert.Equal(replacement, extracted);
        }

        // -----------------------------------------------------------------------
        // Replace on read-only image should throw
        // -----------------------------------------------------------------------

        [Fact]
        public void Replace_ThrowsOnReadOnlyImage()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "data");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            Assert.Throws<InvalidOperationException>(() =>
                image.ReplaceFileContent("/test.txt", new byte[] { 0x41 }));
        }

        // -----------------------------------------------------------------------
        // Replace non-existent file should throw
        // -----------------------------------------------------------------------

        [Fact]
        public void Replace_ThrowsOnNonExistentPath()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "data");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: false);
            Assert.Throws<FileNotFoundException>(() =>
                image.ReplaceFileContent("/nonexistent.txt", new byte[] { 0x41 }));
        }

        // -----------------------------------------------------------------------
        // Replace file in subdirectory
        // -----------------------------------------------------------------------

        [Fact]
        public void Replace_FileInSubdirectory_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "a", "b"));
            File.WriteAllText(Path.Combine(_testDir, "a", "b", "deep.txt"), "original_deep");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.ReplaceFileContent("/a/b/deep.txt", System.Text.Encoding.UTF8.GetBytes("replaced_deep"));
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/a/b", _outputDir);
            }

            Assert.Equal("replaced_deep", File.ReadAllText(Path.Combine(_outputDir, "deep.txt")));
        }

        // -----------------------------------------------------------------------
        // Replace with empty content
        // -----------------------------------------------------------------------

        [Fact]
        public void Replace_WithEmptyContent_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "some content");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.ReplaceFileContent("/test.txt", Array.Empty<byte>());
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                image.Extract("/", _outputDir);
            }

            string extracted = File.ReadAllText(Path.Combine(_outputDir, "test.txt"));
            Assert.Equal("", extracted);
        }

        // -----------------------------------------------------------------------
        // Fsck should pass after replacement
        // -----------------------------------------------------------------------

        [Fact]
        public void Replace_FsckPassesAfterReplacement_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "original content");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.ReplaceFileContent("/test.txt", System.Text.Encoding.UTF8.GetBytes("replaced content"));
            }

            using var checkImage = new Ufs2Image(_imagePath, readOnly: true);
            var result = checkImage.FsckUfs(preen: true);
            Assert.True(result.Clean, $"Fsck errors: {string.Join("; ", result.Errors)}");
        }
    }
}
