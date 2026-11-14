package net.minecraft.server.v1_14_R1;

/** A block that has a block entity. */
public interface ITileEntity {
    TileEntity createTile(IBlockAccess world);
}
