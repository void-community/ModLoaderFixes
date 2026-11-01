package net.minecraft.server.v1_7_R4;

import java.util.ArrayList;
import java.util.List;
import java.util.Queue;
import java.util.concurrent.ConcurrentLinkedQueue;

import net.minecraft.util.io.netty.channel.Channel;

/**
 * A connection as PaperSpigot 1.7.10 keeps it, by CraftBukkit's names: it has no channel until channelActive, which clears Spigot's preparing flag after it
 * stores the channel, and processing its packets reads the channel at once. The network thread's channelActive can run at any moment of a tick; the test
 * makes it run just after the tick checks the channel.
 */
public class NetworkManager {
    public boolean preparing = true;

    private final Queue<String> received = new ConcurrentLinkedQueue<String>();
    private final List<String> answered = new ArrayList<String>();
    private Channel m;
    private boolean disconnected;
    private Runnable afterCheck;

    /** Makes channelActive run right after the next check of the channel, as the network thread may. */
    void activateAfterCheck(final Channel channel) {
        afterCheck = new Runnable() {
            @Override
            public void run() {
                channelActive(channel);
            }
        };
    }

    void channelActive(Channel channel) {
        m = channel;
        preparing = false;
    }

    /** Runs a channelActive that no check has run yet, as the network thread does in the end. */
    void finishActivation() {
        runAfterCheck();
    }

    void receive(String packet) {
        received.add(packet);
    }

    public boolean isConnected() {
        boolean connected = m != null && m.isOpen();

        runAfterCheck();

        return connected;
    }

    /** processReceivedPackets, which starts by reading the channel's state, as Minecraft 1.7's does. */
    public void a() {
        if (!m.isOpen())
            throw new IllegalStateException("processed after its channel closed");

        for (String packet = received.poll(); packet != null; packet = received.poll())
            answered.add(packet);
    }

    /** What the tick tells the handler of a connection it takes for closed; a handshake listener does nothing with it. */
    void disconnected() {
        disconnected = true;
    }

    String describe(String name) {
        return name + (disconnected ? " dropped" : " kept") + ", answered " + answered;
    }

    private void runAfterCheck() {
        Runnable activation = afterCheck;

        afterCheck = null;

        if (activation != null)
            activation.run();
    }
}
