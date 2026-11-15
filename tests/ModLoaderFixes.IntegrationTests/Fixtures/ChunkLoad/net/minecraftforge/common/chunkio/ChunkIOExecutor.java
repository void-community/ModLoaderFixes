package net.minecraftforge.common.chunkio;

import net.minecraft.world.World;
import net.minecraft.world.chunk.Chunk;
import net.minecraft.world.chunk.storage.AnvilChunkLoader;
import net.minecraft.world.gen.ChunkProviderServer;
import net.minecraftforge.common.util.AsynchronousExecutor;

/** Forge 1.7.2 to 1.9's chunk loading executor, whose synchronous load returns what the read gave, also when the second stage generated the chunk instead. */
public class ChunkIOExecutor {
    private static final AsynchronousExecutor<QueuedChunk, Chunk> instance = new AsynchronousExecutor<QueuedChunk, Chunk>(new ChunkIOProvider());

    public static Chunk syncChunkLoad(World world, AnvilChunkLoader loader, ChunkProviderServer provider, int x, int z) {
        return instance.getSkipQueue(new QueuedChunk(x, z, loader, world, provider));
    }
}
