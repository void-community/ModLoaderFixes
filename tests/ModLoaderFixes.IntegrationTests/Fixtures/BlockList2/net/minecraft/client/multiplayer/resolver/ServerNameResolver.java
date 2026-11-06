package net.minecraft.client.multiplayer.resolver;

import java.util.function.Predicate;

import com.mojang.patchy.MojangBlockListSupplier;

/** Minecraft 1.17 and later's resolver of server addresses, whose default instance asks patchy for the list of blocked servers in its static initializer. */
public class ServerNameResolver {
    public static final ServerNameResolver DEFAULT = new ServerNameResolver(new MojangBlockListSupplier().createBlockList());

    private final Predicate<String> blockList;

    private ServerNameResolver(Predicate<String> blockList) {
        this.blockList = blockList;
    }

    public boolean hasBlockList() {
        return blockList != null;
    }
}
