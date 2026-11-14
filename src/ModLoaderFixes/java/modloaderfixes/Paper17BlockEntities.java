package modloaderfixes;

import java.util.logging.Level;

import net.minecraft.core.BlockPosition;
import net.minecraft.world.level.World;
import net.minecraft.world.level.block.Block;
import net.minecraft.world.level.block.ITileEntity;
import net.minecraft.world.level.block.entity.TileEntity;
import net.minecraft.world.level.block.state.IBlockData;

import org.bukkit.Bukkit;

/**
 * Gives a block of a Paper 1.17 world its own block entity when Bukkit reads its state and finds another block's, as PaperBlockEntities does for Paper 1.14.
 * Paper 1.17 has CraftBukkit's repair of such a block entity, but the repair asks whether the block's state, where it means the block, can create a block
 * entity, so it logs that it will attempt a fix and replaces nothing, and the cast in CraftBlockEntityState's constructor still fails: a world from an earlier
 * release with a spawner on a dungeon's chest stops the server. Paper 1.17.1 picks the state by the block entity it finds and has no such constructor. Paper
 * 1.17 keeps CraftBukkit's names in Mojang's packages, so this names the game's classes there.
 */
public final class Paper17BlockEntities {
    private Paper17BlockEntities() {
    }

    /** Stands in for tileEntityClass.cast(entity) in CraftBlockEntityState's constructor. */
    public static Object cast(Class<?> type, Object entity) {
        if (entity instanceof TileEntity && !type.isInstance(entity) && Bukkit.isPrimaryThread()) {
            TileEntity found = (TileEntity) entity;
            World world = found.getWorld();
            BlockPosition position = found.getPosition();
            IBlockData state = world == null || position == null ? null : world.getType(position);
            Block block = state == null ? null : state.getBlock();

            if (block instanceof ITileEntity) {
                TileEntity replacement = ((ITileEntity) block).createTile(position, state);

                if (type.isInstance(replacement)) {
                    world.setTileEntity(replacement);
                    Bukkit.getLogger().log(Level.SEVERE, PaperBlockEntities.REPLACED, new Object[] { position.getX(), position.getY(), position.getZ(), block, found });

                    return replacement;
                }
            }
        }

        return type.cast(entity);
    }
}
