package modloaderfixes;

import java.util.function.BiConsumer;
import java.util.function.Function;

/**
 * Removes the block entity that a block leaves behind when world generation in Minecraft 1.13 to 1.16.5 puts another block in its place. The region that
 * generation writes through only notes that the new block needs a block entity, and removes the old block's only when the new block has none, so a spawner set
 * where a chest stood keeps the chest's block entity: a dungeon whose centre lands on an earlier dungeon's chest leaves a spawner that never spawns, and Paper
 * 1.14 and 1.17 stop when they read it. A chunk that is already part of the world removes the block entity of a block that another block replaces, and the
 * patched region asks the same here for the chunks it generates. It names no game type.
 */
public final class GenerationBlocks {
    private GenerationBlocks() {
    }

    /**
     * Has the chunk remove its block entity at the position when it had a block state there whose block isn't the block of the state that was placed. A chunk
     * gives no replaced state when it kept its block, and the same block in another state keeps its block entity, as a chest that a structure turns does. The
     * patched region passes the state's getBlock and the chunk's removeTileEntity, which this can't name, as functions that capture nothing, so setting a block
     * allocates nothing more than before.
     */
    public static void removeEntityOfOtherBlock(Object replaced, Object placed, Function<Object, Object> block, Object chunk, Object position, BiConsumer<Object, Object> removeEntity) {
        if (replaced != null && block.apply(replaced) != block.apply(placed))
            removeEntity.accept(chunk, position);
    }
}
