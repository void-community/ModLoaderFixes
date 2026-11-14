package net.minecraft.world.chunk;

import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;

import net.minecraft.block.BlockState;
import net.minecraft.tileentity.TileEntity;
import net.minecraft.util.math.BlockPos;

/** A chunk that is still generating: it keeps the block entities that were created and notes those still to create, and never removes one on its own. */
public final class ChunkPrimer implements IChunk {
    private final Map<BlockPos, BlockState> states = new HashMap<BlockPos, BlockState>();
    private final Map<BlockPos, TileEntity> entities = new HashMap<BlockPos, TileEntity>();
    private final Set<BlockPos> noted = new HashSet<BlockPos>();

    @Override
    public BlockState func_177436_a(BlockPos position, BlockState state, boolean moving) {
        BlockState replaced = states.put(position, state);

        return replaced == state ? null : replaced;
    }

    @Override
    public void func_177425_e(BlockPos position) {
        entities.remove(position);
        noted.remove(position);
    }

    @Override
    public void noteTileEntity(BlockPos position) {
        noted.add(position);
    }

    @Override
    public TileEntity getTileEntity(BlockPos position) {
        return entities.get(position);
    }

    @Override
    public BlockState getBlockState(BlockPos position) {
        return states.get(position);
    }

    @Override
    public TileEntity createNoted(BlockPos position) {
        if (!noted.remove(position))
            return null;

        TileEntity created = new TileEntity(states.get(position).func_177230_c());

        entities.put(position, created);

        return created;
    }
}
