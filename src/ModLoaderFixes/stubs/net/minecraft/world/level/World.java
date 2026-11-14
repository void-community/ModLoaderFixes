package net.minecraft.world.level;

import net.minecraft.core.BlockPosition;
import net.minecraft.world.level.block.entity.TileEntity;
import net.minecraft.world.level.block.state.IBlockData;

/** A world of Paper 1.17, by CraftBukkit's names in Mojang's packages: the block at a position, and the block entity it puts at the entity's own position. */
public abstract class World {
    public IBlockData getType(BlockPosition position) {
        throw new UnsupportedOperationException();
    }

    public void setTileEntity(TileEntity entity) {
        throw new UnsupportedOperationException();
    }
}
