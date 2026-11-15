package net.minecraft.world.chunk;

/** A chunk, which says whether it was read from disk or generated. */
public final class Chunk {
    private final String origin;

    public Chunk(String origin) {
        this.origin = origin;
    }

    /** The world's tick does this to each chunk near a player. */
    public String enqueueRelightChecks() {
        return origin;
    }
}
