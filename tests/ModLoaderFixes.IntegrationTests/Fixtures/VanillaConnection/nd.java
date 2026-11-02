import io.netty.channel.Channel;
import io.netty.channel.ChannelHandler;
import io.netty.channel.ChannelInitializer;

/**
 * Vanilla Minecraft 1.7's server channel initializer, under the name 1.7.10 gives it: it adds the new connection to the list the tick runs, then to the pipeline
 * as packet_handler.
 */
public class nd extends ChannelInitializer {
    private final NetworkTick tick;

    public nd(NetworkTick tick) {
        this.tick = tick;
    }

    @Override
    protected void initChannel(Channel channel) {
        channel.pipeline().addLast("timeout", new ChannelHandler() {
        });

        Connection connection = new Connection(false);

        tick.connections.add(connection);
        channel.pipeline().addLast("packet_handler", connection);
    }
}
