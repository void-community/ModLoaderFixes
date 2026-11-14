package net.minecraft.world.level;

import java.util.HashMap;
import java.util.Map;

import net.minecraft.core.BlockPosition;
import net.minecraft.world.level.block.entity.TileEntity;
import net.minecraft.world.level.block.state.IBlockData;

/** A world: the block and the block entity at each position, which nothing keeps in step with each other. */
public abstract class World {
    private final Map<Long, IBlockData> blocks = new HashMap<Long, IBlockData>();
    private final Map<Long, TileEntity> entities = new HashMap<Long, TileEntity>();

    public IBlockData getType(BlockPosition position) {
        return blocks.get(key(position));
    }

    public TileEntity getTileEntity(BlockPosition position) {
        return entities.get(key(position));
    }

    public void setTileEntity(TileEntity entity) {
        entity.setWorld(this);
        entities.put(key(entity.getPosition()), entity);
    }

    /** Puts a block at a position without touching the block entity there, as a chunk loaded from disk has them. */
    public void setType(BlockPosition position, IBlockData state) {
        blocks.put(key(position), state);
    }

    private static long key(BlockPosition position) {
        return ((long) position.getX() << 40) ^ ((long) position.getY() << 20) ^ position.getZ();
    }
}
