package net.minecraft.network;

/** The client's end of the connection: disconnect closes it with a reason, and the server's close closes it without one. */
public class Connection {
    private volatile boolean open = true;
    private volatile String disconnectedReason;

    public boolean isConnected() {
        return this.open;
    }

    public void disconnect(String reason) {
        if (this.open) {
            this.open = false;
            this.disconnectedReason = reason;
        }
    }

    public String getDisconnectedReason() {
        return this.disconnectedReason;
    }
}
