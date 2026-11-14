package net.minecraft.world.level.block.entity;

import net.minecraft.core.BlockPosition;
import net.minecraft.world.level.World;

/** A block entity of Paper 1.17, by CraftBukkit's names in Mojang's packages: the world and the position it is in. */
public abstract class TileEntity {
    public World getWorld() {
        throw new UnsupportedOperationException();
    }

    public BlockPosition getPosition() {
        throw new UnsupportedOperationException();
    }
}
