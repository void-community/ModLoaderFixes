package cpw.mods.fml.common.network.handshake;

import cpw.mods.fml.common.FMLCommonHandler;
import net.minecraft.network.EnumConnectionState;
import net.minecraft.network.INetHandler;
import net.minecraft.network.NetworkManager;

/** FML 1.7's handler of a connection's handshake, which switches to the play state on the network thread and waits for the client thread's play client. */
public class NetworkDispatcher {
    public final NetworkManager manager;
    private INetHandler netHandler;

    public NetworkDispatcher(NetworkManager manager) {
        this.manager = manager;
    }

    void clientListenForServerHandshake() {
        manager.func_150723_a(EnumConnectionState.PLAY);
        FMLCommonHandler.instance().waitForPlayClient();
        netHandler = FMLCommonHandler.instance().getClientPlayHandler();
    }

    public INetHandler getNetHandler() {
        return netHandler;
    }

    /** What the network thread does for a server's Login Success and then its REGISTER, as one connection's handshake. */
    public void handleLoginSuccessThenRegister() {
        clientListenForServerHandshake();
        cpw.mods.fml.client.FMLClientHandler.instance().fireNetRegistrationEvent(manager);
    }
}
