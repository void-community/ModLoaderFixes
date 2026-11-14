package net.minecraft.server.v1_14_R1;

/** A block state. */
public class IBlockData {
    private final Block block;

    public IBlockData(Block block) {
        this.block = block;
    }

    public Block getBlock() {
        return block;
    }
}
