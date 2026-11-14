package net.minecraft.server.v1_14_R1;

import java.util.HashMap;
import java.util.Map;

/** A world: the block and the block entity at each position, which nothing keeps in step with each other. */
public abstract class World implements IBlockAccess {
    private final Map<Long, IBlockData> blocks = new HashMap<Long, IBlockData>();
    private final Map<Long, TileEntity> entities = new HashMap<Long, TileEntity>();

    public IBlockData getType(BlockPosition position) {
        return blocks.get(key(position));
    }

    public TileEntity getTileEntity(BlockPosition position) {
        return entities.get(key(position));
    }

    public void setTileEntity(BlockPosition position, TileEntity entity) {
        entity.place(this, position);
        entities.put(key(position), entity);
    }

    /** Puts a block at a position without touching the block entity there, as a chunk loaded from disk has them. */
    public void setType(BlockPosition position, IBlockData state) {
        blocks.put(key(position), state);
    }

    private static long key(BlockPosition position) {
        return ((long) position.getX() << 40) ^ ((long) position.getY() << 20) ^ position.getZ();
    }
}
