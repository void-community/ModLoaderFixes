package modloaderfixes;

import java.util.List;

import io.netty.channel.ChannelHandler;
import io.netty.channel.ChannelPipeline;

/**
 * Adds a new connection of vanilla Minecraft 1.7 to the list that the network tick runs once the connection's channel is active. The channel initializer adds
 * it to the list and then to the pipeline, while channelActive gives it its channel only afterwards, and a tick in between drops it. Netty runs the
 * initializer and channelActive in one task of the channel's event loop, so a task the initializer queues on that loop runs after both. The initializer's two
 * calls come here: the first is remembered, and the second, which reaches the connection's channel through its pipeline, queues the add. Both run on the
 * event loop, one after the other, so the thread keeps what is remembered between them. It names no class of Minecraft, whose names change in every release.
 */
public final class NewConnections {
    private static final ThreadLocal<Object[]> PENDING = new ThreadLocal<Object[]>();

    private NewConnections() {
    }

    /** Stands in for list.add(connection) in the initializer: remembers both until addLast puts the connection into its pipeline. */
    public static boolean add(List<Object> list, Object connection) {
        PENDING.set(new Object[] { list, connection });

        return true;
    }

    /** Stands in for pipeline.addLast(name, connection) in the initializer: adds the connection, and queues the add that add(list, connection) put off. */
    @SuppressWarnings("unchecked")
    public static ChannelPipeline addLast(ChannelPipeline pipeline, String name, ChannelHandler connection) {
        ChannelPipeline result = pipeline.addLast(name, connection);
        Object[] pending = PENDING.get();

        PENDING.remove();

        if (pending != null) {
            final List<Object> list = (List<Object>) pending[0];
            final Object added = pending[1];

            pipeline.channel().eventLoop().execute(new Runnable() {
                @Override
                public void run() {
                    list.add(added);
                }
            });
        }

        return result;
    }
}
