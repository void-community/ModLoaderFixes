package net.minecraft.block;

/** Minecraft 1.16's base of blocks, by MCP's names, as far as it declares the base of block states. */
public abstract class AbstractBlock {
    /** The base of a block state, which declares getBlock; the code that has a BlockState calls it on that class. */
    public abstract static class AbstractBlockState {
        private final Block block;

        protected AbstractBlockState(Block block) {
            this.block = block;
        }

        /** getBlock. */
        public Block func_177230_c() {
            return block;
        }
    }
}
