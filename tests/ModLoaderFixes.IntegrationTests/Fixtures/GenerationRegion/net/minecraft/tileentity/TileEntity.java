package net.minecraft.tileentity;

import net.minecraft.block.Block;

/** A block entity: the block that created it and, for a chest, the loot table that a dungeon gives it. */
public final class TileEntity {
    private final Block block;
    private String lootTable;

    public TileEntity(Block block) {
        this.block = block;
    }

    public Block getBlock() {
        return block;
    }

    public String getLootTable() {
        return lootTable;
    }

    public void setLootTable(String lootTable) {
        this.lootTable = lootTable;
    }
}
