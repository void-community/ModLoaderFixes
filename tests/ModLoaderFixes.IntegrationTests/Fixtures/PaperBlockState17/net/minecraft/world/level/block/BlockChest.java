package net.minecraft.world.level.block;

import net.minecraft.core.BlockPosition;
import net.minecraft.world.level.block.entity.TileEntity;
import net.minecraft.world.level.block.entity.TileEntityChest;
import net.minecraft.world.level.block.state.IBlockData;

/** The chest, whose block entity is a TileEntityChest. */
public class BlockChest extends Block implements ITileEntity {
    public BlockChest() {
        super("chest");
    }

    @Override
    public TileEntity createTile(BlockPosition position, IBlockData state) {
        return new TileEntityChest(position);
    }
}
