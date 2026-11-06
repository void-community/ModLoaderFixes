package cpw.mods.fml.common.network.handshake;

import net.minecraft.network.INetHandler;
import net.minecraft.network.NetworkManager;

/** Forge 1.7's NetworkDispatcher, with the setter of the private netHandler that PlayClientWait adds to it. */
public class NetworkDispatcher {
    /** The connection the dispatcher handles FML's messages for. */
    public final NetworkManager manager = null;

    /** Stores the play handler that FML's messages are handled with. */
    public void modloaderfixes$netHandler(INetHandler handler) {
        throw new UnsupportedOperationException();
    }
}
