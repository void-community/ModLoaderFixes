package net.minecraft.server.level;

import java.util.logging.Level;

import net.minecraft.core.BlockPosition;
import net.minecraft.world.level.World;
import net.minecraft.world.level.block.ITileEntity;
import net.minecraft.world.level.block.entity.TileEntity;
import net.minecraft.world.level.block.state.IBlockData;
import org.bukkit.Bukkit;

/**
 * A server's world, with CraftBukkit's repair of a block entity that isn't its block's as Paper 1.17 has it: it asks whether the block's state, where it means
 * the block, can create a block entity, which no state can, so it logs that it will attempt a fix and hands out the block entity it found.
 */
public class WorldServer extends World {
    @Override
    public TileEntity getTileEntity(BlockPosition position) {
        TileEntity found = super.getTileEntity(position);
        IBlockData type = getType(position);

        if (found == null || type == null || type.getBlock().getClass().getSimpleName().endsWith(found.getClass().getSimpleName().substring("TileEntity".length())))
            return found;

        return fixTileEntity(position, type, found);
    }

    private TileEntity fixTileEntity(BlockPosition position, IBlockData type, TileEntity found) {
        Bukkit.getLogger().log(Level.SEVERE, "Block at {0}, {1}, {2} is {3} but has {4}. Bukkit will attempt to fix this, but there may be additional damage that we cannot recover.", new Object[] { position.getX(), position.getY(), position.getZ(), type.getBlock(), found });

        Object block = type;

        if (block instanceof ITileEntity) {
            TileEntity replacement = ((ITileEntity) block).createTile(position, type);

            if (replacement != null)
                setTileEntity(replacement);

            return replacement;
        }

        return found;
    }
}
