package net.minecraftforge.common.chunkio;

import net.minecraft.world.World;
import net.minecraft.world.chunk.storage.AnvilChunkLoader;
import net.minecraft.world.gen.ChunkProviderServer;

/** A chunk that Forge is to load. */
class QueuedChunk {
    final int x;
    final int z;
    final AnvilChunkLoader loader;
    final World world;
    final ChunkProviderServer provider;

    QueuedChunk(int x, int z, AnvilChunkLoader loader, World world, ChunkProviderServer provider) {
        this.x = x;
        this.z = z;
        this.loader = loader;
        this.world = world;
        this.provider = provider;
    }
}
