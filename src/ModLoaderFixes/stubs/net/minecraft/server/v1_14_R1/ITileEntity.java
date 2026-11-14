package net.minecraft.server.v1_14_R1;

/** A block of Paper 1.14 that has a block entity, by CraftBukkit's names. */
public interface ITileEntity {
    TileEntity createTile(IBlockAccess world);
}
