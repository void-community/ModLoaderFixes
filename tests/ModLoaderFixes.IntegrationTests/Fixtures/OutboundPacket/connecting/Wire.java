package connecting;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.ThreadFactory;
import java.util.concurrent.TimeUnit;

import net.minecraft.network.Packet;

/**
 * A client's channel as Minecraft 1.7 has it: the connection's state lives on the channel, and its event loop encodes each packet written to it in the state it
 * finds there. A packet of another state has no id in it, and the connection fails.
 */
public final class Wire {
    private final List<String> sent = new ArrayList<String>();
    private final ExecutorService loop = Executors.newSingleThreadExecutor(new ThreadFactory() {
        @Override
        public Thread newThread(Runnable task) {
            loopThread = new Thread(task, "Netty Client IO #1");

            return loopThread;
        }
    });
    private volatile Thread loopThread;
    private volatile String state = Packet.HANDSHAKING;
    private volatile String failure;
    private volatile Runnable afterStateRead;

    /** Runs a task once, right after the next read of the state, as another thread may run at that moment. */
    void afterStateRead(Runnable task) {
        afterStateRead = task;
    }

    public String state() {
        String read = state;
        Runnable task = afterStateRead;

        afterStateRead = null;

        if (task != null)
            task.run();

        return read;
    }

    public void setState(String state) {
        this.state = state;
    }

    public boolean inEventLoop() {
        return Thread.currentThread() == loopThread;
    }

    public void execute(Runnable task) {
        loop.execute(task);
    }

    /** Encodes a packet in the channel's state, as Minecraft's encoder does on the event loop. */
    public void write(Packet packet) {
        if (failure != null)
            return;

        if (packet.state().equals(state))
            sent.add(packet.name());
        else
            failure = packet.name() + ": Can't serialize unregistered packet";
    }

    /** Waits for the writes queued so far, and describes what went out. */
    String drain() throws InterruptedException {
        loop.shutdown();
        loop.awaitTermination(10, TimeUnit.SECONDS);

        return "sent " + sent + (failure == null ? "" : ", then " + failure);
    }
}
