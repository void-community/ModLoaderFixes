package org.bukkit.craftbukkit.v1_17_R1.block;

import net.minecraft.core.BlockPosition;
import org.bukkit.block.Block;
import org.bukkit.craftbukkit.v1_17_R1.CraftWorld;

/** Bukkit's view of a block: its world and its position. */
public class CraftBlock implements Block {
    private final CraftWorld world;
    private final BlockPosition position;

    public CraftBlock(CraftWorld world, BlockPosition position) {
        this.world = world;
        this.position = position;
    }

    public CraftWorld getWorld() {
        return world;
    }

    public BlockPosition getPosition() {
        return position;
    }
}
