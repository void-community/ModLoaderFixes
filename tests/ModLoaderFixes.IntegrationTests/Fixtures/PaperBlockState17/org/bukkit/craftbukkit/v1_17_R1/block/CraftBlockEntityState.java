package org.bukkit.craftbukkit.v1_17_R1.block;

import net.minecraft.core.BlockPosition;
import net.minecraft.server.level.WorldServer;
import net.minecraft.world.level.block.BlockChest;
import net.minecraft.world.level.block.BlockMobSpawner;
import net.minecraft.world.level.block.entity.TileEntity;
import net.minecraft.world.level.block.entity.TileEntityChest;
import net.minecraft.world.level.block.entity.TileEntityMobSpawner;
import net.minecraft.world.level.block.state.IBlockData;
import org.bukkit.Bukkit;
import org.bukkit.block.Block;
import org.bukkit.craftbukkit.v1_17_R1.CraftWorld;

/**
 * Paper 1.17's state of a block with a block entity, whose constructor casts the block entity at the block's position to the class that the block's state
 * expects. main reads the states of a world as the timings history does: of a chest, of a spawner that has a chest's block entity, as a world from Minecraft
 * 1.16.5 or earlier can have where a dungeon set its spawner on an earlier dungeon's chest, and of such a spawner again from a thread that isn't the server's.
 */
public class CraftBlockEntityState<T extends TileEntity> {
    private final Class<T> tileEntityClass;
    private final T tileEntity;

    public CraftBlockEntityState(Block block, Class<T> tileEntityClass) {
        this.tileEntityClass = tileEntityClass;

        CraftBlock craftBlock = (CraftBlock) block;
        CraftWorld world = craftBlock.getWorld();

        this.tileEntity = tileEntityClass.cast(world.getHandle().getTileEntity(craftBlock.getPosition()));

        if (this.tileEntity == null)
            throw new IllegalStateException("Tile is null, asynchronous access? " + block);
    }

    public T getTileEntity() {
        return tileEntity;
    }

    public static void main(String[] arguments) throws Exception {
        WorldServer server = new WorldServer();
        CraftWorld world = new CraftWorld(server);
        BlockPosition chest = new BlockPosition(8, 10, 7);
        BlockPosition spawner = new BlockPosition(8, 10, 8);
        BlockPosition other = new BlockPosition(8, 10, 9);

        server.setType(chest, new IBlockData(new BlockChest()));
        server.setTileEntity(new TileEntityChest(chest));

        for (BlockPosition position : new BlockPosition[] { spawner, other }) {
            server.setType(position, new IBlockData(new BlockMobSpawner()));
            server.setTileEntity(new TileEntityChest(position));
        }

        TileEntity kept = server.getTileEntity(chest);
        boolean chestRead = new CraftBlockEntityState<TileEntityChest>(new CraftBlock(world, chest), TileEntityChest.class).getTileEntity() == kept;
        String read;

        try {
            TileEntityMobSpawner state = new CraftBlockEntityState<TileEntityMobSpawner>(new CraftBlock(world, spawner), TileEntityMobSpawner.class).getTileEntity();

            read = state == server.getTileEntity(spawner) && state.getWorld() == server ? "READ the spawner's state from its own block entity" : "READ a block entity that the world doesn't have";
        } catch (ClassCastException exception) {
            read = "CRASHED on the spawner: " + exception.getMessage();
        }

        // A plugin's thread can't replace a block entity, so its read fails as before and leaves the world as it is.
        Bukkit.setPrimaryThread(new Thread());

        String asynchronous;

        try {
            new CraftBlockEntityState<TileEntityMobSpawner>(new CraftBlock(world, other), TileEntityMobSpawner.class);
            asynchronous = "REPLACED a block entity off the server thread";
        } catch (ClassCastException exception) {
            asynchronous = server.getTileEntity(other) instanceof TileEntityChest ? "REFUSED the read off the server thread" : "REPLACED a block entity off the server thread";
        }

        System.out.println(read);
        System.out.println((chestRead ? "READ" : "LOST") + " the chest's state; " + asynchronous);
        System.exit(read.startsWith("READ the spawner's state from its own") && chestRead && asynchronous.startsWith("REFUSED") ? 0 : 1);
    }
}
