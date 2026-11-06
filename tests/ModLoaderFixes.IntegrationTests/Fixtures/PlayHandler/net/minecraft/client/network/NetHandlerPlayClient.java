package net.minecraft.client.network;

import net.minecraft.network.INetHandler;
import net.minecraft.network.NetworkManager;

/** The client's play handler, which the client thread creates for a connection once it switches to the play state. */
public class NetHandlerPlayClient implements INetHandler {
    private final NetworkManager manager;

    public NetHandlerPlayClient(NetworkManager manager) {
        this.manager = manager;
    }

    public NetworkManager func_147298_b() {
        return manager;
    }
}
