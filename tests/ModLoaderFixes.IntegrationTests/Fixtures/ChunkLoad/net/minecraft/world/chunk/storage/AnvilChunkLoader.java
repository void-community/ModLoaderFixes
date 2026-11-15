package net.minecraft.world.chunk.storage;

import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;

import net.minecraft.world.chunk.Chunk;

/**
 * Minecraft 1.7's chunk loader with Forge's two additions. Chunks that are to be saved wait in a list, which chunkExists and the read look in before the
 * disk. The File IO thread takes a chunk off that list before it writes it, so a chunk that was never on disk is in neither place until the write is done;
 * here that thread's turn comes between the two looks, which is the moment the real one sometimes takes.
 */
public class AnvilChunkLoader {
    private final Set<Long> waiting = new HashSet<Long>();
    private final Map<Long, Chunk> disk = new HashMap<Long, Chunk>();

    /** A chunk that the server unloaded and that the File IO thread still has to write for the first time. */
    public void addWaiting(int x, int z) {
        waiting.add(key(x, z));
    }

    /** A chunk that was written earlier. */
    public void addWritten(int x, int z) {
        disk.put(key(x, z), new Chunk("read"));
    }

    /** Forge's chunkExists: the chunk waits to be saved or is on disk. */
    public boolean chunkExists(int x, int z) {
        boolean exists = waiting.contains(key(x, z)) || disk.containsKey(key(x, z));

        // The File IO thread takes the chunk off the list now and has not written it yet.
        waiting.remove(key(x, z));

        return exists;
    }

    /** Forge's loadChunk__Async: the chunk from the list or from disk, or null when it is in neither. */
    public Chunk loadChunk__Async(int x, int z) {
        return disk.get(key(x, z));
    }

    private static long key(int x, int z) {
        return (long) x << 32 | z & 0xffffffffL;
    }
}
