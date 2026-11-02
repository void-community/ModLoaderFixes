package net.minecraft.network.protocol.game;

/** The packet with which a server disconnects the client. */
public class ClientboundDisconnectPacket {
    private final String reason;

    public ClientboundDisconnectPacket(String reason) {
        this.reason = reason;
    }

    public String getReason() {
        return this.reason;
    }
}
