package net.minecraft.world.chunk;

import net.minecraft.block.BlockState;
import net.minecraft.tileentity.TileEntity;
import net.minecraft.util.math.BlockPos;

/** A chunk as world generation writes to it, by MCP's names where the agent's patch names them. */
public interface IChunk {
    /** setBlockState: sets the block and returns the state it replaced, or null when the state is the same. */
    BlockState func_177436_a(BlockPos position, BlockState state, boolean moving);

    /** removeTileEntity: forgets the block entity at the position, created or only noted. */
    void func_177425_e(BlockPos position);

    /** Notes that the block at the position needs a block entity, as the DUMMY tag of the real chunk does. */
    void noteTileEntity(BlockPos position);

    /** The block entity that was created at the position, or null. */
    TileEntity getTileEntity(BlockPos position);

    /** The state at the position, or null. */
    BlockState getBlockState(BlockPos position);

    /** Creates the noted block entity, from the block that is at the position by then. */
    TileEntity createNoted(BlockPos position);
}
