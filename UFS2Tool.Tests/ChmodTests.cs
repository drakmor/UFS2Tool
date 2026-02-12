// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.IO;
using UFS2Tool;

namespace UFS2Tool.Tests
{
    /// <summary>
    /// Tests for the chmod functionality:
    /// 1. Change permissions of a single file or directory in a UFS2 image
    /// 2. Change permissions of a single file or directory in a UFS1 image
    /// 3. Change permissions of an entire UFS2 image recursively
    /// 4. Change permissions of an entire UFS1 image recursively
    /// 5. Verify file type bits are preserved after chmod
    /// 6. Verify read-only images reject chmod
    /// </summary>
    public class ChmodTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _imagePath;

        public ChmodTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"ufs2test_chmod_src_{Guid.NewGuid():N}");
            _imagePath = Path.Combine(Path.GetTempPath(), $"ufs2test_chmod_{Guid.NewGuid():N}.img");
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
        // Chmod single file (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Chmod_SingleFile_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "hello");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                // Change to 0777 (rwxrwxrwx)
                image.Chmod("/test.txt", 0x1FF); // 0777 octal = 0x1FF
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                var inode = image.ReadInode(image.ResolvePath("/test.txt"));
                ushort permBits = (ushort)(inode.Mode & 0x0FFF);
                Assert.Equal(0x1FF, permBits); // 0777
                Assert.True(inode.IsRegularFile); // Type bits preserved
            }
        }

        // -----------------------------------------------------------------------
        // Chmod single directory (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void Chmod_SingleDirectory_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "subdir"));
            File.WriteAllText(Path.Combine(_testDir, "subdir", "file.txt"), "content");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                // Change directory to 0700 (rwx------)
                image.Chmod("/subdir", 0x1C0); // 0700 octal = 0x1C0
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                var inode = image.ReadInode(image.ResolvePath("/subdir"));
                ushort permBits = (ushort)(inode.Mode & 0x0FFF);
                Assert.Equal(0x1C0, permBits); // 0700
                Assert.True(inode.IsDirectory); // Type bits preserved
            }
        }

        // -----------------------------------------------------------------------
        // Chmod single file (UFS1)
        // -----------------------------------------------------------------------

        [Fact]
        public void Chmod_SingleFile_Ufs1()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "hello");
            CreatePopulatedUfs1Image();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Chmod("/test.txt", 0x1A4); // 0644 octal = 0x1A4
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                var inode = image.ReadInode(image.ResolvePath("/test.txt"));
                ushort permBits = (ushort)(inode.Mode & 0x0FFF);
                Assert.Equal(0x1A4, permBits); // 0644
                Assert.True(inode.IsRegularFile);
            }
        }

        // -----------------------------------------------------------------------
        // ChmodAll entire image (UFS2)
        // -----------------------------------------------------------------------

        [Fact]
        public void ChmodAll_EntireImage_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "dir1"));
            File.WriteAllText(Path.Combine(_testDir, "file1.txt"), "content1");
            File.WriteAllText(Path.Combine(_testDir, "dir1", "file2.txt"), "content2");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                // Files: 0644 (0x1A4), Directories: 0755 (0x1ED)
                image.ChmodAll(0x1A4, 0x1ED);
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                // Check root directory
                var rootInode = image.ReadInode(Ufs2Constants.RootInode);
                Assert.Equal(0x1ED, rootInode.Mode & 0x0FFF); // 0755
                Assert.True(rootInode.IsDirectory);

                // Check dir1
                var dir1Inode = image.ReadInode(image.ResolvePath("/dir1"));
                Assert.Equal(0x1ED, dir1Inode.Mode & 0x0FFF); // 0755
                Assert.True(dir1Inode.IsDirectory);

                // Check file1.txt
                var file1Inode = image.ReadInode(image.ResolvePath("/file1.txt"));
                Assert.Equal(0x1A4, file1Inode.Mode & 0x0FFF); // 0644
                Assert.True(file1Inode.IsRegularFile);

                // Check file2.txt in subdirectory
                var file2Inode = image.ReadInode(image.ResolvePath("/dir1/file2.txt"));
                Assert.Equal(0x1A4, file2Inode.Mode & 0x0FFF); // 0644
                Assert.True(file2Inode.IsRegularFile);
            }
        }

        // -----------------------------------------------------------------------
        // ChmodAll entire image (UFS1)
        // -----------------------------------------------------------------------

        [Fact]
        public void ChmodAll_EntireImage_Ufs1()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "subdir"));
            File.WriteAllText(Path.Combine(_testDir, "a.txt"), "aaa");
            File.WriteAllText(Path.Combine(_testDir, "subdir", "b.txt"), "bbb");
            CreatePopulatedUfs1Image();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.ChmodAll(0x1FF, 0x1FF); // 0777 for all
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                var rootInode = image.ReadInode(Ufs2Constants.RootInode);
                Assert.Equal(0x1FF, rootInode.Mode & 0x0FFF);

                var subInode = image.ReadInode(image.ResolvePath("/subdir"));
                Assert.Equal(0x1FF, subInode.Mode & 0x0FFF);

                var aInode = image.ReadInode(image.ResolvePath("/a.txt"));
                Assert.Equal(0x1FF, aInode.Mode & 0x0FFF);

                var bInode = image.ReadInode(image.ResolvePath("/subdir/b.txt"));
                Assert.Equal(0x1FF, bInode.Mode & 0x0FFF);
            }
        }

        // -----------------------------------------------------------------------
        // Chmod preserves file type bits
        // -----------------------------------------------------------------------

        [Fact]
        public void Chmod_PreservesFileTypeBits_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "content");
            Directory.CreateDirectory(Path.Combine(_testDir, "testdir"));
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Chmod("/test.txt", 0x000); // 0000
                image.Chmod("/testdir", 0x000);  // 0000
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                var fileInode = image.ReadInode(image.ResolvePath("/test.txt"));
                Assert.Equal(0x000, fileInode.Mode & 0x0FFF); // Permissions cleared
                Assert.Equal(Ufs2Constants.IfReg, (ushort)(fileInode.Mode & 0xF000)); // File type preserved

                var dirInode = image.ReadInode(image.ResolvePath("/testdir"));
                Assert.Equal(0x000, dirInode.Mode & 0x0FFF); // Permissions cleared
                Assert.Equal(Ufs2Constants.IfDir, (ushort)(dirInode.Mode & 0xF000)); // Dir type preserved
            }
        }

        // -----------------------------------------------------------------------
        // Chmod on read-only image throws
        // -----------------------------------------------------------------------

        [Fact]
        public void Chmod_ReadOnly_Throws()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "content");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            Assert.Throws<InvalidOperationException>(() => image.Chmod("/test.txt", 0x1FF));
        }

        // -----------------------------------------------------------------------
        // ChmodAll on read-only image throws
        // -----------------------------------------------------------------------

        [Fact]
        public void ChmodAll_ReadOnly_Throws()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "content");
            CreatePopulatedImage();

            using var image = new Ufs2Image(_imagePath, readOnly: true);
            Assert.Throws<InvalidOperationException>(() => image.ChmodAll(0x1A4, 0x1ED));
        }

        // -----------------------------------------------------------------------
        // Chmod updates ctime
        // -----------------------------------------------------------------------

        [Fact]
        public void Chmod_UpdatesChangeTime_Ufs2()
        {
            File.WriteAllText(Path.Combine(_testDir, "test.txt"), "content");
            CreatePopulatedImage();

            long ctimeBefore;
            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                var inode = image.ReadInode(image.ResolvePath("/test.txt"));
                ctimeBefore = inode.ChangeTime;
            }

            // Small delay to ensure timestamp differs
            System.Threading.Thread.Sleep(1100);

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Chmod("/test.txt", 0x1FF);
            }

            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                var inode = image.ReadInode(image.ResolvePath("/test.txt"));
                Assert.True(inode.ChangeTime >= ctimeBefore, "ChangeTime should be updated after chmod");
            }
        }

        // -----------------------------------------------------------------------
        // Chmod passes fsck validation
        // -----------------------------------------------------------------------

        [Fact]
        public void Chmod_FsckPasses_Ufs2()
        {
            Directory.CreateDirectory(Path.Combine(_testDir, "subdir"));
            File.WriteAllText(Path.Combine(_testDir, "file.txt"), "content");
            File.WriteAllText(Path.Combine(_testDir, "subdir", "nested.txt"), "nested");
            CreatePopulatedImage();

            using (var image = new Ufs2Image(_imagePath, readOnly: false))
            {
                image.Chmod("/file.txt", 0x1A4);   // 0644
                image.Chmod("/subdir", 0x1ED);      // 0755
                image.ChmodAll(0x1B6, 0x1FF);       // files=0666, dirs=0777
            }

            // Verify filesystem is still consistent
            using (var image = new Ufs2Image(_imagePath, readOnly: true))
            {
                var result = image.FsckUfs();
                Assert.True(result.Clean, $"fsck failed after chmod: {string.Join("; ", result.Errors)}");
            }
        }
    }
}
