using System.Buffers.Binary;
using System.IO.Compression;

namespace ModLoaderFixes.IntegrationTests.Servers;

/// <summary>Edits a world's region file as it lies on disk, to give a server a world that it couldn't have saved itself.</summary>
internal static class RegionFiles
{
    private const int Entries = 1024;
    private const int Sector = 4096;
    private const byte Zlib = 2;

    /// <summary>
    /// Gives each spawner's block entity in a region file a chest's id, and leaves the block a spawner: what world generation up to Minecraft 1.16.5 leaves where a
    /// dungeon sets its spawner on an earlier dungeon's chest. A chunk is a compressed NBT document that holds each name with its length before it, and nothing
    /// in it counts the bytes that follow, so the id is replaced in place and the chunk compressed again into the sectors it had.
    /// </summary>
    /// <param name="region">The region file's bytes, which are changed.</param>
    /// <returns>How many block entities got the chest's id.</returns>
    internal static int GiveSpawnersAChestsBlockEntity(byte[] region)
    {
        ArgumentNullException.ThrowIfNull(region);

        byte[] spawner = [0x00, 0x15, .. "minecraft:mob_spawner"u8];
        byte[] chest = [0x00, 0x0F, .. "minecraft:chest"u8];
        int swapped = 0;

        for (int index = 0; index < Entries; index++)
        {
            int entry = BinaryPrimitives.ReadInt32BigEndian(region.AsSpan(index * 4));
            int start = (entry >>> 8) * Sector;

            if (entry == 0 || region[start + 4] != Zlib)
                continue;

            int length = BinaryPrimitives.ReadInt32BigEndian(region.AsSpan(start)) - 1;
            byte[] chunk = Transform(region.AsSpan(start + 5, length), CompressionMode.Decompress);
            int found = Replace(ref chunk, spawner, chest);

            if (found == 0)
                continue;

            byte[] packed = Transform(chunk, CompressionMode.Compress);

            if (packed.Length + 5 > (entry & 0xFF) * Sector)
                throw new InvalidOperationException(message: "The chunk no longer fits the sectors it had.");

            BinaryPrimitives.WriteInt32BigEndian(region.AsSpan(start), packed.Length + 1);
            packed.CopyTo(region, start + 5);
            swapped += found;
        }

        return swapped;
    }

    private static int Replace(ref byte[] data, ReadOnlySpan<byte> old, ReadOnlySpan<byte> replacement)
    {
        int count = 0;

        for (int at = data.AsSpan().IndexOf(old); at >= 0; at = data.AsSpan().IndexOf(old))
        {
            data = [.. data.AsSpan(start: 0, at), .. replacement, .. data.AsSpan(at + old.Length)];
            count++;
        }

        return count;
    }

    private static byte[] Transform(ReadOnlySpan<byte> data, CompressionMode mode)
    {
        using MemoryStream source = new(data.ToArray());

        using MemoryStream result = new();

        if (mode == CompressionMode.Decompress)
        {
            using ZLibStream inflater = new(source, mode);

            inflater.CopyTo(result);
        }
        else
        {
            using (ZLibStream deflater = new(result, mode, leaveOpen: true))
                source.CopyTo(deflater);
        }

        return result.ToArray();
    }
}
