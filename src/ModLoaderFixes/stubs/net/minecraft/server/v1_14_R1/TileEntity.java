package net.minecraft.server.v1_14_R1;

/** A block entity of Paper 1.14, by CraftBukkit's names: the world and the position it is in. */
public abstract class TileEntity {
    public World getWorld() {
        throw new UnsupportedOperationException();
    }

    public BlockPosition getPosition() {
        throw new UnsupportedOperationException();
    }
}
