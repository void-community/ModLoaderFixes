package net.minecraft.server.v1_14_R1;

/** The spawner, whose block entity is a TileEntityMobSpawner. */
public class BlockMobSpawner extends Block implements ITileEntity {
    public BlockMobSpawner() {
        super("spawner");
    }

    @Override
    public TileEntity createTile(IBlockAccess world) {
        return new TileEntityMobSpawner();
    }
}
