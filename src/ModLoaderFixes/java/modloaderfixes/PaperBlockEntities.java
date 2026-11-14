package modloaderfixes;

import java.util.logging.Level;

import net.minecraft.server.v1_14_R1.Block;
import net.minecraft.server.v1_14_R1.BlockPosition;
import net.minecraft.server.v1_14_R1.ITileEntity;
import net.minecraft.server.v1_14_R1.TileEntity;
import net.minecraft.server.v1_14_R1.World;

import org.bukkit.Bukkit;

/**
 * Gives a block of a Paper 1.14 world its own block entity when Bukkit reads its state and finds another block's. CraftBlockEntityState casts the block entity
 * at the block's position to the class that the block's state expects, and a block with another block's entity, as world generation up to Minecraft 1.16.5
 * leaves where a dungeon sets its spawner on an earlier dungeon's chest, fails the cast: the timings history, which reads the state of every block entity of
 * the loaded chunks, stops the server with a ClassCastException. CraftBukkit replaces such a block entity as the world hands it out, but its code for that is
 * missing from Paper 1.14 to 1.14.4 before build 217. The constructor casts here instead, and a block entity that fails the cast gives way to the one that its
 * block creates, on the server thread, as CraftBukkit's own repair does; whatever else fails the cast still does.
 */
public final class PaperBlockEntities {
    /** What the server's log says of a replaced block entity, with the position, the block and the block entity found there. */
    static final String REPLACED = "Block at {0}, {1}, {2} is {3} but has {4}. ModLoaderFixes gave the block its own block entity so that its state can be read; "
            + "what the other one held is lost.";

    private PaperBlockEntities() {
    }

    /** Stands in for tileEntityClass.cast(entity) in CraftBlockEntityState's constructor. */
    public static Object cast(Class<?> type, Object entity) {
        if (entity instanceof TileEntity && !type.isInstance(entity) && Bukkit.isPrimaryThread()) {
            TileEntity found = (TileEntity) entity;
            World world = found.getWorld();
            BlockPosition position = found.getPosition();
            Block block = world == null || position == null ? null : world.getType(position).getBlock();

            if (block instanceof ITileEntity) {
                TileEntity replacement = ((ITileEntity) block).createTile(world);

                if (type.isInstance(replacement)) {
                    world.setTileEntity(position, replacement);
                    Bukkit.getLogger().log(Level.SEVERE, REPLACED, new Object[] { position.getX(), position.getY(), position.getZ(), block, found });

                    return replacement;
                }
            }
        }

        return type.cast(entity);
    }
}
