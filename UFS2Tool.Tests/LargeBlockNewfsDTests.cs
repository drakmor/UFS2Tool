// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.IO;
using System.Linq;
using UFS2Tool;

namespace UFS2Tool.Tests
{
    /// <summary>
    /// Regression tests for newfs -D with large blocks/fragments.
    /// These cases are sensitive to directory-size estimation because each missed
    /// directory block costs a full 64 KiB when bsize == fsize == 65536.
    /// </summary>
    public class LargeBlockNewfsDTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _imagePath;

        public LargeBlockNewfsDTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"ufs2largeblk_dir_{Guid.NewGuid():N}");
            _imagePath = Path.Combine(Path.GetTempPath(), $"ufs2largeblk_img_{Guid.NewGuid():N}.img");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (File.Exists(_imagePath))
                File.Delete(_imagePath);
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }

        [Fact]
        public void CalculateDirectorySizes_LargeBlockLongNames_PreciseEstimateExceedsLegacyEstimate()
        {
            string bigDir = Path.Combine(_testDir, "bigdir");
            Directory.CreateDirectory(bigDir);

            for (int i = 0; i < 1200; i++)
            {
                string name = $"file_{i:D04}_{new string('x', 180)}.bin";
                File.WriteAllText(Path.Combine(bigDir, name), "x");
            }

            const int blockSize = 65536;
            const int fragmentSize = 65536;

            var (_, legacySize, _) = Ufs2ImageCreator.CalculateDirectorySizes(_testDir, blockSize);
            var (_, preciseSize, _) = Ufs2ImageCreator.CalculateDirectorySizes(
                _testDir, blockSize, fragmentSize, filesystemFormat: 2);

            Assert.True(preciseSize > legacySize,
                $"Precise directory sizing should exceed legacy estimate for long-name large-block trees. " +
                $"legacy={legacySize}, precise={preciseSize}");
        }

        [Fact]
        public void CreateImageFromDirectory_LargeBlockConfig_PreservesAllEntries()
        {
            string bigDir = Path.Combine(_testDir, "bigdir");
            Directory.CreateDirectory(bigDir);

            for (int i = 0; i < 1200; i++)
            {
                string name = $"file_{i:D04}_{new string('x', 180)}.bin";
                File.WriteAllText(Path.Combine(bigDir, name), $"data_{i}");
            }

            var creator = new Ufs2ImageCreator
            {
                FilesystemFormat = 2,
                BlockSize = 65536,
                FragmentSize = 65536,
                SectorSize = 4096,
                BytesPerInode = 262144,
                MinFreePercent = 0
            };

            creator.CreateImageFromDirectory(_imagePath, _testDir);

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            var sb = image.Superblock;
            Assert.True(sb.IsValid);
            Assert.Equal(65536, sb.BSize);
            Assert.Equal(65536, sb.FSize);
            Assert.Equal(1, sb.FragsPerBlock);

            var rootEntries = image.ListRoot();
            var bigDirEntry = rootEntries.First(e => e.Name == "bigdir");
            var dirEntries = image.ListDirectory(bigDirEntry.Inode);
            var fileEntries = dirEntries.Where(e => e.FileType == Ufs2Constants.DtReg).ToList();

            Assert.Equal(1200, fileEntries.Count);

            string sampleName = $"file_{1199:D04}_{new string('x', 180)}.bin";
            var sampleEntry = fileEntries.First(e => e.Name == sampleName);
            string sampleContent = System.Text.Encoding.UTF8.GetString(image.ReadFile(sampleEntry.Inode));
            Assert.Equal("data_1199", sampleContent);
        }
    }
}
