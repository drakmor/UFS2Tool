// Copyright (c) 2026, SvenGDK
// Licensed under the BSD 2-Clause License. See LICENSE file for details.

using System;
using System.Collections.Generic;
using System.IO;

namespace UFS2Tool
{
    /// <summary>
    /// High-level API for reading and writing to a UFS1/UFS2 filesystem image.
    /// </summary>
    public class Ufs2Image : IDisposable
    {
        private FileStream _stream;
        private BinaryReader _reader;
        private BinaryWriter _writer;

        public Ufs2Superblock Superblock { get; private set; }
        public string ImagePath { get; }
        public bool IsReadOnly { get; }

        public Ufs2Image(string imagePath, bool readOnly = false)
        {
            ImagePath = imagePath;
            IsReadOnly = readOnly;

            var access = readOnly ? FileAccess.Read : FileAccess.ReadWrite;
            _stream = new FileStream(imagePath, FileMode.Open, access);
            _reader = new BinaryReader(_stream);
            if (!readOnly)
                _writer = new BinaryWriter(_stream);

            ReadSuperblock();
        }

        private void ReadSuperblock()
        {
            _stream.Position = Ufs2Constants.SuperblockOffset;
            Superblock = Ufs2Superblock.ReadFrom(_reader);

            if (!Superblock.IsValid)
                throw new InvalidDataException(
                    $"Invalid UFS superblock. Magic: 0x{Superblock.Magic:X8}, " +
                    $"Expected: 0x{Ufs2Constants.Ufs2Magic:X8} (UFS2) or 0x{Ufs2Constants.Ufs1Magic:X8} (UFS1)");
        }

        /// <summary>
        /// Read an inode by its inode number. Supports both UFS1 and UFS2 formats.
        /// </summary>
        public Ufs2Inode ReadInode(uint inodeNumber)
        {
            int cgIndex = (int)(inodeNumber / Superblock.InodesPerGroup);
            int inodeIndexInCg = (int)(inodeNumber % Superblock.InodesPerGroup);

            int inodeSize = Superblock.IsUfs1
                ? Ufs2Constants.Ufs1InodeSize : Ufs2Constants.Ufs2InodeSize;

            long cgStart = (long)cgIndex * Superblock.CylGroupSize * Superblock.FSize;
            long inodeTableStart = cgStart + (long)Superblock.IblkNo * Superblock.FSize;

            long inodeOffset = inodeTableStart + (long)inodeIndexInCg * inodeSize;
            _stream.Position = inodeOffset;

            if (Superblock.IsUfs1)
            {
                // Read UFS1 inode and convert to UFS2 inode for API consistency
                var ufs1 = Ufs1Inode.ReadFrom(_reader);
                return ConvertUfs1ToUfs2Inode(ufs1);
            }

            return Ufs2Inode.ReadFrom(_reader);
        }

        /// <summary>
        /// Convert a UFS1 inode to a UFS2 inode for unified API access.
        /// </summary>
        private static Ufs2Inode ConvertUfs1ToUfs2Inode(Ufs1Inode ufs1)
        {
            var inode = new Ufs2Inode
            {
                Mode = ufs1.Mode,
                NLink = ufs1.NLink,
                Uid = ufs1.Uid,
                Gid = ufs1.Gid,
                Size = ufs1.Size,
                Blocks = ufs1.Blocks,
                AccessTime = ufs1.AccessTime,
                ModTime = ufs1.ModTime,
                ChangeTime = ufs1.ChangeTime,
                ATimeNsec = ufs1.ATimeNsec,
                MTimeNsec = ufs1.MTimeNsec,
                CTimeNsec = ufs1.CTimeNsec,
                Generation = ufs1.Generation,
                Flags = ufs1.Flags
            };

            // Convert 32-bit block pointers to 64-bit
            for (int i = 0; i < Ufs2Constants.NDirect; i++)
                inode.DirectBlocks[i] = ufs1.DirectBlocks[i];
            for (int i = 0; i < Ufs2Constants.NIndirect; i++)
                inode.IndirectBlocks[i] = ufs1.IndirectBlocks[i];

            return inode;
        }

        /// <summary>
        /// List directory entries for the given directory inode.
        /// </summary>
        public List<Ufs2DirectoryEntry> ListDirectory(uint dirInodeNumber)
        {
            var inode = ReadInode(dirInodeNumber);
            if (!inode.IsDirectory)
                throw new InvalidOperationException($"Inode {dirInodeNumber} is not a directory.");

            var entries = new List<Ufs2DirectoryEntry>();
            long remaining = inode.Size;

            // Read through direct blocks
            for (int i = 0; i < Ufs2Constants.NDirect && remaining > 0; i++)
            {
                if (inode.DirectBlocks[i] == 0) continue;

                long blockOffset = inode.DirectBlocks[i] * Superblock.FSize;
                _stream.Position = blockOffset;

                long toRead = Math.Min(remaining, Superblock.BSize);
                long blockEnd = _stream.Position + toRead;

                while (_stream.Position < blockEnd)
                {
                    var entry = Ufs2DirectoryEntry.ReadFrom(_reader);
                    if (entry.RecordLength == 0) break; // Safety: prevent infinite loop
                    if (entry.Inode != 0)
                        entries.Add(entry);
                    remaining -= entry.RecordLength;
                }
            }

            // Handle indirect blocks for large directories
            if (remaining > 0 && inode.IndirectBlocks[0] != 0)
            {
                remaining = ReadDirectoryIndirectBlock(inode.IndirectBlocks[0], entries, remaining);
            }

            return entries;
        }

        /// <summary>
        /// Read directory entries from data blocks referenced by an indirect pointer block.
        /// </summary>
        private long ReadDirectoryIndirectBlock(long indirectBlockFrag,
            List<Ufs2DirectoryEntry> entries, long remaining)
        {
            long blockOffset = indirectBlockFrag * Superblock.FSize;
            _stream.Position = blockOffset;

            int ptrSize = Superblock.IsUfs1 ? 4 : 8;
            int pointersPerBlock = Superblock.BSize / ptrSize;

            for (int i = 0; i < pointersPerBlock && remaining > 0; i++)
            {
                _stream.Position = blockOffset + (long)i * ptrSize;
                long ptr = Superblock.IsUfs1 ? _reader.ReadInt32() : _reader.ReadInt64();
                if (ptr == 0) continue;

                long dataOffset = ptr * Superblock.FSize;
                _stream.Position = dataOffset;

                long toRead = Math.Min(remaining, Superblock.BSize);
                long blockEnd = _stream.Position + toRead;

                while (_stream.Position < blockEnd)
                {
                    var entry = Ufs2DirectoryEntry.ReadFrom(_reader);
                    if (entry.RecordLength == 0) break;
                    if (entry.Inode != 0)
                        entries.Add(entry);
                    remaining -= entry.RecordLength;
                }
            }

            return remaining;
        }

        /// <summary>
        /// List the root directory.
        /// </summary>
        public List<Ufs2DirectoryEntry> ListRoot()
        {
            return ListDirectory(Ufs2Constants.RootInode);
        }

        /// <summary>
        /// Read the contents of a regular file by inode number.
        /// </summary>
        public byte[] ReadFile(uint inodeNumber)
        {
            var inode = ReadInode(inodeNumber);
            if (!inode.IsRegularFile)
                throw new InvalidOperationException($"Inode {inodeNumber} is not a regular file.");

            byte[] data = new byte[inode.Size];
            long offset = 0;

            // Direct blocks
            for (int i = 0; i < Ufs2Constants.NDirect && offset < inode.Size; i++)
            {
                if (inode.DirectBlocks[i] == 0) continue;

                long blockOffset = inode.DirectBlocks[i] * Superblock.FSize;
                _stream.Position = blockOffset;

                int toRead = (int)Math.Min(Superblock.BSize, inode.Size - offset);
                _stream.Read(data, (int)offset, toRead);
                offset += toRead;
            }

            // Single indirect
            if (offset < inode.Size && inode.IndirectBlocks[0] != 0)
            {
                offset = ReadIndirectBlock(inode.IndirectBlocks[0], data, offset, inode.Size);
            }

            // Double indirect
            if (offset < inode.Size && inode.IndirectBlocks[1] != 0)
            {
                offset = ReadDoubleIndirectBlock(inode.IndirectBlocks[1], data, offset, inode.Size);
            }

            // Triple indirect
            if (offset < inode.Size && inode.IndirectBlocks[2] != 0)
            {
                offset = ReadTripleIndirectBlock(inode.IndirectBlocks[2], data, offset, inode.Size);
            }

            return data;
        }

        private long ReadIndirectBlock(long indirectBlockFrag, byte[] data, long offset, long fileSize)
        {
            long blockOffset = indirectBlockFrag * Superblock.FSize;
            _stream.Position = blockOffset;

            int ptrSize = Superblock.IsUfs1 ? 4 : 8; // UFS1: 32-bit, UFS2: 64-bit
            int pointersPerBlock = Superblock.BSize / ptrSize;
            var ptrReader = new BinaryReader(_stream);

            for (int i = 0; i < pointersPerBlock && offset < fileSize; i++)
            {
                long ptr = Superblock.IsUfs1 ? ptrReader.ReadInt32() : ptrReader.ReadInt64();
                if (ptr == 0) continue;

                long dataOffset = ptr * Superblock.FSize;
                _stream.Position = dataOffset;

                int toRead = (int)Math.Min(Superblock.BSize, fileSize - offset);
                _stream.Read(data, (int)offset, toRead);
                offset += toRead;

                // Restore position for next pointer
                _stream.Position = blockOffset + ((long)(i + 1) * ptrSize);
            }

            return offset;
        }

        private long ReadDoubleIndirectBlock(long doubleIndirectBlockFrag, byte[] data, long offset, long fileSize)
        {
            long blockOffset = doubleIndirectBlockFrag * Superblock.FSize;
            _stream.Position = blockOffset;

            int ptrSize = Superblock.IsUfs1 ? 4 : 8;
            int pointersPerBlock = Superblock.BSize / ptrSize;
            var ptrReader = new BinaryReader(_stream);

            for (int i = 0; i < pointersPerBlock && offset < fileSize; i++)
            {
                long ptr = Superblock.IsUfs1 ? ptrReader.ReadInt32() : ptrReader.ReadInt64();
                if (ptr == 0) continue;

                offset = ReadIndirectBlock(ptr, data, offset, fileSize);

                // Restore position for next pointer in the double-indirect block
                _stream.Position = blockOffset + ((long)(i + 1) * ptrSize);
            }

            return offset;
        }

        private long ReadTripleIndirectBlock(long tripleIndirectBlockFrag, byte[] data, long offset, long fileSize)
        {
            long blockOffset = tripleIndirectBlockFrag * Superblock.FSize;
            _stream.Position = blockOffset;

            int ptrSize = Superblock.IsUfs1 ? 4 : 8;
            int pointersPerBlock = Superblock.BSize / ptrSize;
            var ptrReader = new BinaryReader(_stream);

            for (int i = 0; i < pointersPerBlock && offset < fileSize; i++)
            {
                long ptr = Superblock.IsUfs1 ? ptrReader.ReadInt32() : ptrReader.ReadInt64();
                if (ptr == 0) continue;

                offset = ReadDoubleIndirectBlock(ptr, data, offset, fileSize);

                // Restore position for next pointer in the triple-indirect block
                _stream.Position = blockOffset + ((long)(i + 1) * ptrSize);
            }

            return offset;
        }

        /// <summary>
        /// Get filesystem summary information.
        /// </summary>
        public string GetInfo()
        {
            string formatStr = Superblock.IsUfs1 ? "UFS1" : "UFS2";
            var flags = new System.Collections.Generic.List<string>();
            if ((Superblock.Flags & Ufs2Constants.FsDosoftdep) != 0) flags.Add("SOFTUPDATES");
            if ((Superblock.Flags & Ufs2Constants.FsSuj) != 0) flags.Add("SUJ");
            if ((Superblock.Flags & Ufs2Constants.FsGjournal) != 0) flags.Add("GJOURNAL");
            if ((Superblock.Flags & Ufs2Constants.FsMultilabel) != 0) flags.Add("MULTILABEL");
            if ((Superblock.Flags & Ufs2Constants.FsTrim) != 0) flags.Add("TRIM");
            string flagStr = flags.Count > 0 ? string.Join(", ", flags) : "none";

            string optimStr = Superblock.Optimization == Ufs2Constants.FsOptSpace ? "space" : "time";

            return $"""
                {formatStr} Filesystem Image: {ImagePath}
                Magic: 0x{Superblock.Magic:X8} ({(Superblock.IsValid ? "Valid " + formatStr : "INVALID")})
                Block Size: {Superblock.BSize} bytes
                Fragment Size: {Superblock.FSize} bytes
                Cylinder Groups: {Superblock.NumCylGroups}
                Inodes Per Group: {Superblock.InodesPerGroup}
                Total Fragments: {Superblock.TotalBlocks}
                Total Size: {Superblock.TotalBlocks * Superblock.FSize / (1024 * 1024)} MB
                Free Blocks: {Superblock.FreeBlocks}
                Free Inodes: {Superblock.FreeInodes}
                Directories: {Superblock.Directories}
                Volume Name: {Superblock.VolumeName}
                Min Free: {Superblock.MinFreePercent}%
                Optimization: {optimStr}
                Flags: {flagStr}
                Last Modified: {DateTimeOffset.FromUnixTimeSeconds(Superblock.Time):u}
                """;
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _stream?.Dispose();
        }
    }
}