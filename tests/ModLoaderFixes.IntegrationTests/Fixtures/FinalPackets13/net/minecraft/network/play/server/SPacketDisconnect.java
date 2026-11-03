package net.minecraft.network.play.server;

public class SPacketDisconnect {
    private final String reason;

    public SPacketDisconnect(String reason) {
        this.reason = reason;
    }

    public String getReason() {
        return this.reason;
    }
}
