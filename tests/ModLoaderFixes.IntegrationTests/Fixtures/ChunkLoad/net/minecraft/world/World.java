package net.minecraft.world;

import net.minecraft.world.chunk.Chunk;
import net.minecraft.world.chunk.storage.AnvilChunkLoader;
import net.minecraft.world.gen.ChunkProviderServer;

/**
 * A server's world, whose tick asks its provider for each chunk near a player and uses it. main ticks two chunks as a player joins: one that was written
 * earlier, and one that the server unloaded a moment ago, when other players left, and that the File IO thread takes off its list to write for the first
 * time while the provider loads it again.
 */
public class World {
    private final AnvilChunkLoader loader = new AnvilChunkLoader();
    private final ChunkProviderServer provider = new ChunkProviderServer(this, loader);

    /** The part of the tick that takes the chunk for granted. */
    private String tick(int x, int z) {
        Chunk chunk = provider.func_73154_d(x, z);

        return chunk.enqueueRelightChecks();
    }

    public static void main(String[] arguments) {
        World world = new World();

        world.loader.addWritten(0, 0);
        world.loader.addWaiting(0, 1);

        try {
            String written = world.tick(0, 0);
            String waiting = world.tick(0, 1);
            boolean kept = world.tick(0, 0).equals("read") && world.provider.func_73154_d(0, 1) == world.provider.func_73154_d(0, 1);

            System.out.println("TICKED a chunk that was " + written + " and one that was " + waiting + (kept ? ", and the provider kept both" : ", and the provider lost one"));
            System.exit(written.equals("read") && waiting.equals("generated") && kept ? 0 : 1);
        } catch (NullPointerException exception) {
            System.out.println("Exception ticking world: " + exception);
            System.exit(1);
        }
    }
}
