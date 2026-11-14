package net.minecraft.world.level.block.state;

import net.minecraft.world.level.block.Block;

/** Minecraft 1.17's base of blocks, as far as it declares the base of block states. */
public abstract class BlockBase {
    /** The base of a block state, which declares getBlock; the code that has an IBlockData calls it on that class. */
    public abstract static class BlockData {
        private final Block block;

        protected BlockData(Block block) {
            this.block = block;
        }

        public Block getBlock() {
            return block;
        }
    }
}
