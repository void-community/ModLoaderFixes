package org.bukkit.craftbukkit.v1_17_R1;

import net.minecraft.server.level.WorldServer;

/** Bukkit's view of a world. */
public class CraftWorld {
    private final WorldServer handle;

    public CraftWorld(WorldServer handle) {
        this.handle = handle;
    }

    public WorldServer getHandle() {
        return handle;
    }
}
