using System;
using System.IO;
using UFS2Tool;
using Xunit;

namespace UFS2Tool.Tests
{
    public class ProgressOutputTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _imagePath;

        public ProgressOutputTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"ufs2test_prog_{Guid.NewGuid():N}");
            _imagePath = Path.Combine(Path.GetTempPath(), $"ufs2test_prog_{Guid.NewGuid():N}.img");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (File.Exists(_imagePath)) File.Delete(_imagePath);
            if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
        }

        [Fact]
        public void Add_SingleFile_WritesProgressToOutput()
        {
            File.WriteAllText(Path.Combine(_testDir, "existing.txt"), "existing");
            var creator = new Ufs2ImageCreator();
            var (_, diskSize, _) = Ufs2ImageCreator.CalculateDirectorySizes(_testDir, creator.BlockSize);
            long imageSize = creator.CalculateImageSize(diskSize);
            creator.CreateImage(_imagePath, imageSize);
            creator.PopulateFromDirectory(_imagePath, _testDir);

            string newFile = Path.Combine(Path.GetTempPath(), $"ufs2test_prog_add_{Guid.NewGuid():N}.txt");
            try
            {
                File.WriteAllText(newFile, "new content");
                using var sw = new StringWriter();
                using (var image = new Ufs2Image(_imagePath))
                {
                    image.Output = sw;
                    image.Add("/added.txt", newFile);
                }
                string output = sw.ToString();
                Assert.Contains("Adding file: /added.txt", output);
            }
            finally
            {
                if (File.Exists(newFile)) File.Delete(newFile);
            }
        }

        [Fact]
        public void Add_DirectoryRecursive_WritesProgressToOutput()
        {
            File.WriteAllText(Path.Combine(_testDir, "existing.txt"), "existing");
            var creator = new Ufs2ImageCreator();
            var (_, diskSize, _) = Ufs2ImageCreator.CalculateDirectorySizes(_testDir, creator.BlockSize);
            long imageSize = creator.CalculateImageSize(diskSize);
            creator.CreateImage(_imagePath, imageSize);
            creator.PopulateFromDirectory(_imagePath, _testDir);

            string addDir = Path.Combine(Path.GetTempPath(), $"ufs2test_prog_adddir_{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(addDir);
                File.WriteAllText(Path.Combine(addDir, "a.txt"), "content_a");
                Directory.CreateDirectory(Path.Combine(addDir, "sub"));
                File.WriteAllText(Path.Combine(addDir, "sub", "b.txt"), "content_b");

                using var sw = new StringWriter();
                using (var image = new Ufs2Image(_imagePath))
                {
                    image.Output = sw;
                    image.Add("/newdir", addDir);
                }
                string output = sw.ToString();
                Assert.Contains("Adding directory: /newdir", output);
                Assert.Contains("Adding file: /newdir/a.txt", output);
                Assert.Contains("Adding directory: /newdir/sub", output);
                Assert.Contains("Adding file: /newdir/sub/b.txt", output);
            }
            finally
            {
                if (Directory.Exists(addDir)) Directory.Delete(addDir, true);
            }
        }

        [Fact]
        public void Add_WithNullOutput_DoesNotThrow()
        {
            File.WriteAllText(Path.Combine(_testDir, "existing.txt"), "existing");
            var creator = new Ufs2ImageCreator();
            var (_, diskSize, _) = Ufs2ImageCreator.CalculateDirectorySizes(_testDir, creator.BlockSize);
            long imageSize = creator.CalculateImageSize(diskSize);
            creator.CreateImage(_imagePath, imageSize);
            creator.PopulateFromDirectory(_imagePath, _testDir);

            string newFile = Path.Combine(Path.GetTempPath(), $"ufs2test_prog_null_{Guid.NewGuid():N}.txt");
            try
            {
                File.WriteAllText(newFile, "content");
                using (var image = new Ufs2Image(_imagePath))
                {
                    // Output is null by default — should not throw
                    image.Add("/nooutput.txt", newFile);
                }
            }
            finally
            {
                if (File.Exists(newFile)) File.Delete(newFile);
            }
        }
    }
}
