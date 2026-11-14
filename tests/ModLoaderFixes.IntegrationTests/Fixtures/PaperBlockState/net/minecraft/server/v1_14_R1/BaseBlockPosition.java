package net.minecraft.server.v1_14_R1;

/** The coordinates of a position. */
public class BaseBlockPosition {
    private final int x;
    private final int y;
    private final int z;

    public BaseBlockPosition(int x, int y, int z) {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public final int getX() {
        return x;
    }

    public int getY() {
        return y;
    }

    public int getZ() {
        return z;
    }
}
