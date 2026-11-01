import java.util.ArrayList;
import java.util.List;
import java.util.Queue;
import java.util.concurrent.ConcurrentLinkedQueue;

import io.netty.channel.Channel;
import io.netty.channel.ChannelHandler;

/**
 * A connection as vanilla Minecraft 1.7 keeps it, under a name of its own as the obfuscated releases each have one: it is constructed with whether it is a
 * client's, has no channel until channelActive, and processing its packets reads the channel at once.
 */
public class Connection implements ChannelHandler {
    private final Queue<String> received = new ConcurrentLinkedQueue<String>();
    private final List<String> answered = new ArrayList<String>();
    private Channel channel;
    private boolean disconnected;

    public Connection(boolean client) {
    }

    /** The network thread's channelActive, which gives the connection its channel. */
    public void channelActive(Channel channel) {
        this.channel = channel;
    }

    /** A packet the network thread queued for the server thread, such as the login start that follows a handshake. */
    public void receive(String packet) {
        received.add(packet);
    }

    public boolean isOpen() {
        return channel != null && channel.isOpen();
    }

    /** Processes the received packets, which starts by reading the channel's state, as Minecraft 1.7's does. */
    public void process() {
        if (!channel.isOpen())
            throw new IllegalStateException("processed after its channel closed");

        for (String packet = received.poll(); packet != null; packet = received.poll())
            answered.add(packet);
    }

    /** What the tick tells the handler of a connection it takes for closed; a handshake handler does nothing with it. */
    void disconnected() {
        disconnected = true;
    }

    String describe(String name) {
        return name + (disconnected ? " dropped" : " kept") + ", answered " + answered;
    }
}
