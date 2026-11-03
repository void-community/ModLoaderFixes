package net.minecraft.network;

/** The client's end of the connection, under Forge's names for Minecraft 1.13.2: closeChannel closes it with a reason, which only the first close keeps. */
public class NetworkManager {
    private volatile boolean open = true;
    private volatile String terminationReason;

    /** isChannelOpen. */
    public boolean func_150724_d() {
        return this.open;
    }

    /** closeChannel. */
    public void func_150718_a(String reason) {
        if (this.open) {
            this.open = false;
            this.terminationReason = reason;
        }
    }

    /** getExitMessage. */
    public String func_150730_f() {
        return this.terminationReason;
    }
}
