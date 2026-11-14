package net.minecraft.world.level.block;

import net.minecraft.core.BlockPosition;
import net.minecraft.world.level.block.entity.TileEntity;
import net.minecraft.world.level.block.entity.TileEntityMobSpawner;
import net.minecraft.world.level.block.state.IBlockData;

/** The spawner, whose block entity is a TileEntityMobSpawner. */
public class BlockMobSpawner extends Block implements ITileEntity {
    public BlockMobSpawner() {
        super("spawner");
    }

    @Override
    public TileEntity createTile(BlockPosition position, IBlockData state) {
        return new TileEntityMobSpawner(position);
    }
}
