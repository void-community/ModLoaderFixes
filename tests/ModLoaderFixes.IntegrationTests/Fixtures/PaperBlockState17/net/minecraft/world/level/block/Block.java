package net.minecraft.world.level.block;

/** A block. */
public class Block {
    private final String name;

    public Block(String name) {
        this.name = name;
    }

    @Override
    public String toString() {
        return "Block{minecraft:" + name + "}";
    }
}
