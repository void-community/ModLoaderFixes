package net.minecraft.world.level.block;

import net.minecraft.core.BlockPosition;
import net.minecraft.world.level.block.entity.TileEntity;
import net.minecraft.world.level.block.state.IBlockData;

/** A block that has a block entity, which from Minecraft 1.17 is created for a position and a state. */
public interface ITileEntity {
    TileEntity createTile(BlockPosition position, IBlockData state);
}
