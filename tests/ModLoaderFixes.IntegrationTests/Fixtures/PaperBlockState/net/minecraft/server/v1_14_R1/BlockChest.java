package net.minecraft.server.v1_14_R1;

/** The chest, whose block entity is a TileEntityChest. */
public class BlockChest extends Block implements ITileEntity {
    public BlockChest() {
        super("chest");
    }

    @Override
    public TileEntity createTile(IBlockAccess world) {
        return new TileEntityChest();
    }
}
