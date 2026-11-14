package net.minecraft.world.gen;

import net.minecraft.block.Block;
import net.minecraft.block.BlockState;
import net.minecraft.tileentity.TileEntity;
import net.minecraft.util.math.BlockPos;
import net.minecraft.world.chunk.ChunkPrimer;
import net.minecraft.world.chunk.IChunk;

/**
 * The region that Minecraft 1.16 generates a chunk through, by MCP's names. Its setBlockState sets the block in the chunk and, for a block with a block entity,
 * notes that the chunk has to create one; it removes the old block's entity only when the new block has none. main generates as two dungeons do whose rooms
 * overlap: the first sets a chest and gives it a loot table, which creates the chest's block entity, and the second sets its spawner where the chest is and
 * asks for the spawner's block entity. It also turns a filled chest, as a structure does, which must keep what the chest holds.
 */
public class WorldGenRegion {
    private static final Block CHEST = new Block("chest", true);
    private static final Block SPAWNER = new Block("spawner", true);
    private static final Block STONE = new Block("stone", false);

    private final IChunk chunk = new ChunkPrimer();

    /** setBlockState. */
    public boolean func_241211_a_(BlockPos position, BlockState state, int flags, int limit) {
        IChunk chunk = this.chunk;
        BlockState replaced = chunk.func_177436_a(position, state, false);
        Block block = state.func_177230_c();

        if (block.hasTileEntity()) {
            chunk.noteTileEntity(position);
        } else if (replaced != null && replaced.func_177230_c().hasTileEntity()) {
            chunk.func_177425_e(position);
        }

        return true;
    }

    /** getTileEntity: the block entity that the chunk has at the position, or the one it noted, created now. */
    public TileEntity getTileEntity(BlockPos position) {
        TileEntity existing = chunk.getTileEntity(position);

        return existing != null ? existing : chunk.createNoted(position);
    }

    public static void main(String[] arguments) {
        WorldGenRegion region = new WorldGenRegion();
        BlockPos centre = new BlockPos(-208, 14, 328);
        BlockPos corner = new BlockPos(-205, 14, 331);
        BlockPos wall = new BlockPos(-204, 14, 331);

        // The first dungeon's chests, each with its loot table.
        for (BlockPos chest : new BlockPos[] { centre, corner, wall }) {
            region.func_241211_a_(chest, new BlockState(CHEST), 2, 512);
            region.getTileEntity(chest).setLootTable("chests/simple_dungeon");
        }

        // The second dungeon's spawner, at its centre whatever is there.
        region.func_241211_a_(centre, new BlockState(SPAWNER), 2, 512);

        TileEntity spawner = region.getTileEntity(centre);

        // A structure turns a chest it has filled, and the second dungeon's wall replaces another.
        region.func_241211_a_(corner, new BlockState(CHEST), 2, 512);
        region.func_241211_a_(wall, new BlockState(STONE), 2, 512);

        TileEntity turned = region.getTileEntity(corner);
        boolean kept = turned != null && "chests/simple_dungeon".equals(turned.getLootTable());
        boolean removed = region.getTileEntity(wall) == null;

        if (spawner.getBlock() != SPAWNER)
            System.out.println("Failed to fetch mob spawner entity: the spawner has the block entity of the " + spawner.getBlock());
        else
            System.out.println("FETCHED the spawner's own block entity");

        System.out.println((kept ? "KEPT" : "LOST") + " the loot of the turned chest; " + (removed ? "REMOVED" : "LEFT") + " the block entity of the replaced chest");
        System.exit(spawner.getBlock() == SPAWNER && kept && removed ? 0 : 1);
    }
}
