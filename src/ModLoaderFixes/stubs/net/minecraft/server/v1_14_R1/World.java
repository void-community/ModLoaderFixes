package net.minecraft.server.v1_14_R1;

/** A world of Paper 1.14, by CraftBukkit's names: the block at a position, and the block entity it puts there. */
public abstract class World implements IBlockAccess {
    public IBlockData getType(BlockPosition position) {
        throw new UnsupportedOperationException();
    }

    public void setTileEntity(BlockPosition position, TileEntity entity) {
        throw new UnsupportedOperationException();
    }
}
