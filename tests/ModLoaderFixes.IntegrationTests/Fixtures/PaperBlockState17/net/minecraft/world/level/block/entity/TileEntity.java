package net.minecraft.world.level.block.entity;

import net.minecraft.core.BlockPosition;
import net.minecraft.world.level.World;

/** A block entity, which from Minecraft 1.17 is created with its position and knows its world once the world has it. */
public abstract class TileEntity {
    private final BlockPosition position;
    private World world;

    protected TileEntity(BlockPosition position) {
        this.position = position;
    }

    public World getWorld() {
        return world;
    }

    public BlockPosition getPosition() {
        return position;
    }

    public void setWorld(World world) {
        this.world = world;
    }
}
