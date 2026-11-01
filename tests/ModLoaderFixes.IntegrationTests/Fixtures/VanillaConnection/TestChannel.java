import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.List;
import java.util.Queue;

import io.netty.channel.Channel;
import io.netty.channel.ChannelHandler;
import io.netty.channel.ChannelPipeline;
import io.netty.channel.EventLoop;

/** A channel with its pipeline and event loop, whose tasks run when the test says, as the loop runs them after its current one. */
public class TestChannel implements Channel, ChannelPipeline, EventLoop {
    private final List<ChannelHandler> handlers = new ArrayList<ChannelHandler>();
    private final Queue<Runnable> tasks = new ArrayDeque<Runnable>();

    @Override
    public boolean isOpen() {
        return true;
    }

    @Override
    public ChannelPipeline pipeline() {
        return this;
    }

    @Override
    public EventLoop eventLoop() {
        return this;
    }

    @Override
    public ChannelPipeline addLast(String name, ChannelHandler handler) {
        handlers.add(handler);

        return this;
    }

    @Override
    public Channel channel() {
        return this;
    }

    @Override
    public boolean inEventLoop() {
        return true;
    }

    @Override
    public void execute(Runnable task) {
        tasks.add(task);
    }

    /** Netty's fireChannelActive, which follows the initializer in the same task of the loop. */
    void activate() {
        connection().channelActive(this);
    }

    void runTasks() {
        for (Runnable task = tasks.poll(); task != null; task = tasks.poll())
            task.run();
    }

    Connection connection() {
        for (ChannelHandler handler : handlers) {
            if (handler instanceof Connection)
                return (Connection) handler;
        }

        throw new IllegalStateException("no connection in the pipeline");
    }
}
