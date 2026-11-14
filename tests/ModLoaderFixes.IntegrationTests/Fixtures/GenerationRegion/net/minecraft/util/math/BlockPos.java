package net.minecraft.util.math;

/** A block's position, by MCP's names. */
public final class BlockPos {
    private final int x;
    private final int y;
    private final int z;

    public BlockPos(int x, int y, int z) {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    @Override
    public boolean equals(Object other) {
        return other instanceof BlockPos && ((BlockPos) other).x == x && ((BlockPos) other).y == y && ((BlockPos) other).z == z;
    }

    @Override
    public int hashCode() {
        return 31 * (31 * x + y) + z;
    }
}
