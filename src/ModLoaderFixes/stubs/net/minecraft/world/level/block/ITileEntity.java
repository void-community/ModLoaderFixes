package net.minecraft.world.level.block;

import net.minecraft.core.BlockPosition;
import net.minecraft.world.level.block.entity.TileEntity;
import net.minecraft.world.level.block.state.IBlockData;

/** A block of Paper 1.17 that has a block entity, by CraftBukkit's names in Mojang's packages. */
public interface ITileEntity {
    TileEntity createTile(BlockPosition position, IBlockData state);
}
