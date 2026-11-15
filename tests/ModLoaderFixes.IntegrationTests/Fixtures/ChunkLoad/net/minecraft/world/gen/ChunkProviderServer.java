package net.minecraft.world.gen;

import java.util.HashMap;
import java.util.Map;

import net.minecraft.world.World;
import net.minecraft.world.chunk.Chunk;
import net.minecraft.world.chunk.storage.AnvilChunkLoader;
import net.minecraftforge.common.chunkio.ChunkIOExecutor;

/** A server's chunk provider as Forge 1.7.2 to 1.9 patch it, by the names a running Forge gives its methods. */
public class ChunkProviderServer {
    private final Map<Long, Chunk> loaded = new HashMap<Long, Chunk>();
    private final World world;
    private final AnvilChunkLoader loader;

    public ChunkProviderServer(World world, AnvilChunkLoader loader) {
        this.world = world;
        this.loader = loader;
    }

    /** loadChunk: a loaded chunk as it is, one that exists through Forge's synchronous load, and any other generated. */
    public Chunk func_73158_c(int x, int z) {
        Chunk chunk = loaded.get(key(x, z));

        if (chunk == null && loader.chunkExists(x, z))
            chunk = ChunkIOExecutor.syncChunkLoad(world, loader, this, x, z);
        else if (chunk == null)
            chunk = originalLoadChunk(x, z);

        return chunk;
    }

    /** provideChunk, which the world's tick asks for each chunk near a player. */
    public Chunk func_73154_d(int x, int z) {
        Chunk chunk = loaded.get(key(x, z));

        return chunk == null ? func_73158_c(x, z) : chunk;
    }

    /** Minecraft's own load: a loaded chunk as it is, or a generated one. */
    public Chunk originalLoadChunk(int x, int z) {
        Chunk chunk = loaded.get(key(x, z));

        if (chunk == null) {
            chunk = new Chunk("generated");
            loaded.put(key(x, z), chunk);
        }

        return chunk;
    }

    /** Takes a chunk that was read as loaded, as the second stage of Forge's load does. */
    public void add(int x, int z, Chunk chunk) {
        loaded.put(key(x, z), chunk);
    }

    private static long key(int x, int z) {
        return (long) x << 32 | z & 0xffffffffL;
    }
}
