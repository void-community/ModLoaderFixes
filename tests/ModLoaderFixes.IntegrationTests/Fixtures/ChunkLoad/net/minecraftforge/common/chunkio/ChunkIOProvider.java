package net.minecraftforge.common.chunkio;

import net.minecraft.world.chunk.Chunk;
import net.minecraftforge.common.util.AsynchronousExecutor;

/** The stages of Forge's chunk load: the read, and what the server thread does with its result. */
class ChunkIOProvider implements AsynchronousExecutor.CallBackProvider<QueuedChunk, Chunk> {
    @Override
    public Chunk callStage1(QueuedChunk queuedChunk) {
        return queuedChunk.loader.loadChunk__Async(queuedChunk.x, queuedChunk.z);
    }

    @Override
    public void callStage2(QueuedChunk queuedChunk, Chunk chunk) {
        if (chunk == null) {
            // If the chunk loading failed just do it synchronously (may generate)
            queuedChunk.provider.originalLoadChunk(queuedChunk.x, queuedChunk.z);
            return;
        }

        queuedChunk.provider.add(queuedChunk.x, queuedChunk.z, chunk);
    }
}
