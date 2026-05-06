// Based on code from CUE4Parse by FabianFG
// Licensed under the Apache License, Version 2.0
// https://github.com/FabianFG/CUE4Parse

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AssetStudio;

public class QtsVFSFile
{
    public Dictionary<ulong, List<FHoKCompressedChunk>> Entries = new();

    public QtsVFSFile(FileReader reader)
    {
        reader.Endian = EndianType.LittleEndian;
        reader.Position = 0;
        var header = new FHoKHeader(reader);
        var tables = FEntriesTables(reader, header.Entries2.Offset);
        var tables1 = FEntriesTables(reader, header.Entries3.Offset);
        var offsets = new HashSet<int>();
        foreach (var x in tables.Concat(tables1))
        {
            offsets.Add(x.Offset1);
            offsets.Add(x.Offset2);
            offsets.Add(x.Offset3);
        }

        List<FHoKEntryBlock> blocks = new List<FHoKEntryBlock>();
        foreach (var x in offsets)
        {
            reader.Position = x + 4080;
            var block = new FHoKEntryBlock(reader);
            // fix offsets for duplicate block
            // idk what is the purpose of duplicate so we read all entries
            block.Offset = x;
            blocks.Add(block);
        }

        offsets = new HashSet<int>();
        foreach (var block in blocks)
        {
            if (block.Type is 2) continue; // this contains offset for other blocks
            var size = block.EntryCount;
            // skip ids
            reader.Position = block.Offset + 2040;
            var entries = reader.ReadInt32Array(size);
            foreach (var offset in entries)
            {
                offsets.Add(offset);
            }
        }

        var result = offsets.ToArray();
        Array.Sort(result);
        ReadEntries(reader, result, Entries);
    }

    private static void ReadEntries(FileReader reader, int[] offsets, Dictionary<ulong, List<FHoKCompressedChunk>> result)
    {
        HashSet<int> additionalOffsets = new HashSet<int>();
        foreach (var x in offsets)
        {
            reader.Position = x;
            if (reader.ReadInt32() != x) continue;
            var next = reader.ReadInt32();
            if (next != -1 && !offsets.Contains(next)) additionalOffsets.Add(next);
            reader.Position += 16;
            var compressedSize = reader.ReadInt32() - 4;
            var uncompressedSize = reader.ReadInt32();
            var offset = reader.Position;

            reader.Position += compressedSize;
            reader.AlignStream(4);
            var id = reader.ReadUInt64();
            var mainBlock = reader.ReadInt32();  // Main block sequence number
            var subBlock = reader.ReadInt32();   // Sub block sequence number
            var entry = new FHoKCompressedChunk(offset, compressedSize, uncompressedSize, mainBlock, subBlock);

            if (result.TryGetValue(id, out var list))
            {
                list.Add(entry);
            }
            else
            {
                result[id] = new List<FHoKCompressedChunk> { entry };
            }
        }

        if (additionalOffsets.Count > 0)
            ReadEntries(reader, additionalOffsets.ToArray(), result);
    }


    private static FHoKEntriesTable[] FEntriesTables(FileReader reader, int offset)
    {
        if (offset == -1) return Array.Empty<FHoKEntriesTable>();
        reader.Position = offset;
        var max = reader.ReadInt32();
        var count = reader.ReadInt32();
        reader.Position += 16;
        return reader.ReadArray(() => new FHoKEntriesTable(reader), count);
    }


    // from cue4parse
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct FHoKHeader
    {
        public ulong Magic;
        public int Size;
        public int FullSize;
        public FHoKEntriesOffsetSize Unknown0;
        public FHoKEntriesOffsetSize Unknown1; // always -1
        public FHoKEntriesOffsetSize Index;
        public FHoKEntriesOffsetSize IndexData;
        public FHoKEntriesOffsetSize Entries1;
        public FHoKEntriesOffsetSize Entries2;
        public FHoKEntriesOffsetSize Entries3;
        public FHoKEntriesOffsetSize Unknown2; // always -1

        public FHoKHeader(FileReader reader)
        {
            Magic = reader.ReadUInt64();
            Size = reader.ReadInt32();
            FullSize = reader.ReadInt32();
            Unknown0 = new FHoKEntriesOffsetSize(reader);
            Unknown1 = new FHoKEntriesOffsetSize(reader);
            Index = new FHoKEntriesOffsetSize(reader);
            IndexData = new FHoKEntriesOffsetSize(reader);
            Entries1 = new FHoKEntriesOffsetSize(reader);
            Entries2 = new FHoKEntriesOffsetSize(reader);
            Entries3 = new FHoKEntriesOffsetSize(reader);
            Unknown2 = new FHoKEntriesOffsetSize(reader);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct FHoKEntriesTable
    {
        public int Offset1;
        public int Offset2;
        public int Offset3;
        public int Unknown1;
        public int Unknown2;
        public int Unknown3;

        public FHoKEntriesTable(FileReader reader)
        {
            Offset1 = reader.ReadInt32();
            Offset2 = reader.ReadInt32();
            Offset3 = reader.ReadInt32();
            Unknown1 = reader.ReadInt32();
            Unknown2 = reader.ReadInt32();
            Unknown3 = reader.ReadInt32();
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct FHoKEntriesOffsetSize
    {
        public int Offset;
        public int Size;

        public FHoKEntriesOffsetSize(FileReader reader)
        {
            Offset = reader.ReadInt32();
            Size = reader.ReadInt32();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FHoKEntryBlock
    {
        public int Offset;
        public int Unknown;
        public int NextOffset;
        public ushort EntryCount;
        public ushort Type;

        public FHoKEntryBlock(FileReader reader)
        {
            Offset = reader.ReadInt32();
            Unknown = reader.ReadInt32();
            NextOffset = reader.ReadInt32();
            EntryCount = reader.ReadUInt16();
            Type = reader.ReadUInt16();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct FHoKCompressedChunk
    {
        public readonly long Offset;
        public readonly int CompressedSize;
        public readonly int UncompressedSize;
        public readonly int MainBlock;  // Main block sequence number, used for sorting
        public readonly int SubBlock;   // Sub block sequence number, used for sorting

        public FHoKCompressedChunk(long offset, int compressedSize, int uncompressedSize, int mainBlock, int subBlock)
        {
            Offset = offset;
            CompressedSize = compressedSize;
            UncompressedSize = uncompressedSize;
            MainBlock = mainBlock;
            SubBlock = subBlock;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static ulong Compute(string text, bool addSlash)
    {
        ArgumentNullException.ThrowIfNull(text);

        int length = text.Length;
        Span<char> raw = length <= 256 ? stackalloc char[length + 1] : new char[length + 1];
        if (addSlash)
        {
            raw[0] = '/';
            text.AsSpan().ToLowerInvariant(raw[1..]);
            length++;
        }
        else
        {
            text.AsSpan().ToLowerInvariant(raw);
        }

        uint h1 = 0x5BD1E995;
        uint h2 = 0xAB9423A7;

        if (length == 0)
            return ((ulong)h2 << 32) | h1;

        int left = 0;
        int right = length - 1;

        while (left < length)
        {
            h1 = (h1 * 33u) ^ raw[left++];
            h2 = (h2 * 33u) ^ raw[right--];
        }

        return ((ulong)h2 << 32) | h1;
    }
}