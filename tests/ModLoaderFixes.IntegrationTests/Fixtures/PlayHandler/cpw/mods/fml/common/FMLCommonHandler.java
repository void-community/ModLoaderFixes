package cpw.mods.fml.common;

import cpw.mods.fml.client.FMLClientHandler;
import net.minecraft.network.INetHandler;

/** FML's side-neutral handler, which hands the client's calls to FMLClientHandler. */
public class FMLCommonHandler {
    private static final FMLCommonHandler INSTANCE = new FMLCommonHandler();

    public static FMLCommonHandler instance() {
        return INSTANCE;
    }

    public void waitForPlayClient() {
        FMLClientHandler.instance().waitForPlayClient();
    }

    public INetHandler getClientPlayHandler() {
        return FMLClientHandler.instance().getClientPlayHandler();
    }
}
