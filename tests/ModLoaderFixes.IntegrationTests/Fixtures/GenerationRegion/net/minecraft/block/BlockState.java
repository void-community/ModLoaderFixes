package net.minecraft.block;

/** A state of a block, by MCP's names: a chest that faces north and one that faces east are two states of one block. */
public class BlockState extends AbstractBlock.AbstractBlockState {
    public BlockState(Block block) {
        super(block);
    }
}
