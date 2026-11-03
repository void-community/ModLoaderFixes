package net.minecraft.client.network;

import java.util.List;
import java.util.concurrent.CopyOnWriteArrayList;

import net.minecraft.network.NetworkManager;

/** The play client, which shows chat and, as S40PacketDisconnect has priority, handles a Disconnect on the network thread by closing the connection. */
public class NetHandlerPlayClient {
    public final List<String> shown = new CopyOnWriteArrayList<String>();
    public volatile String disconnectedReason;

    private final NetworkManager field_147302_e;

    public NetHandlerPlayClient(NetworkManager manager) {
        this.field_147302_e = manager;
    }

    /** getNetworkManager. */
    public NetworkManager func_147298_b() {
        return this.field_147302_e;
    }

    public void handleChat(String message) {
        this.shown.add("[CHAT] " + message);
    }

    public void handleDisconnect(String reason) {
        this.field_147302_e.func_150718_a(reason);
    }

    /** onDisconnect, which shows the reason in place of the world. */
    public void onDisconnect(String reason) {
        this.disconnectedReason = reason;
    }
}
