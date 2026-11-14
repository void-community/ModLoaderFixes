package net.minecraft.server.v1_14_R1;

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
