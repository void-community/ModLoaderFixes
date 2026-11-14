package net.minecraft.server.v1_14_R1;

/** A block entity, which knows the world and the position it is in once the world has it. */
public abstract class TileEntity {
    private World world;
    private BlockPosition position;

    public World getWorld() {
        return world;
    }

    public BlockPosition getPosition() {
        return position;
    }

    void place(World world, BlockPosition position) {
        this.world = world;
        this.position = position;
    }
}
