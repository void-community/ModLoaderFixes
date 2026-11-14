package net.minecraft.block;

/** A block, which may have a block entity. */
public final class Block {
    private final String name;
    private final boolean hasTileEntity;

    public Block(String name, boolean hasTileEntity) {
        this.name = name;
        this.hasTileEntity = hasTileEntity;
    }

    public boolean hasTileEntity() {
        return hasTileEntity;
    }

    @Override
    public String toString() {
        return name;
    }
}
