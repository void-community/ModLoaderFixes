package net.minecraft.network;

import java.util.Queue;
import java.util.concurrent.ConcurrentLinkedQueue;

/**
 * The client's end of the connection, under Forge's names for Minecraft 1.7.10: the network thread queues most packets, which the client tick processes, and
 * closeChannel closes it with a reason, which only the first close keeps.
 */
public class NetworkManager {
    private final Queue<Runnable> receivedPacketsQueue = new ConcurrentLinkedQueue<Runnable>();
    private volatile boolean open = true;
    private volatile String terminationReason;

    public void receive(Runnable packet) {
        this.receivedPacketsQueue.add(packet);
    }

    /** isChannelOpen. */
    public boolean func_150724_d() {
        return this.open;
    }

    /** processReceivedPackets. */
    public void func_74428_b() {
        for (Runnable packet = this.receivedPacketsQueue.poll(); packet != null; packet = this.receivedPacketsQueue.poll())
            packet.run();
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
