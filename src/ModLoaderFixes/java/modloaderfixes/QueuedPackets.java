package modloaderfixes;

import java.util.concurrent.ExecutionException;
import java.util.concurrent.Executor;
import java.util.concurrent.Future;
import java.util.concurrent.FutureTask;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

/**
 * Lets a Minecraft client from 1.13 handle the packets a server sent before it disconnected the client. The network thread queues most packets, such as chat,
 * for the client thread, while it handles the Disconnect at once and closes the connection. The server closes its end right after the Disconnect too. The client
 * thread then leaves the world and drops the tasks still queued, and from 1.14.4 it also drops each packet it finds the connection closed for, so the chat it
 * sent just before is lost. The client's handleDisconnect calls here first, on the network thread: it queues a task after the packets already queued and waits
 * until the client thread has run it, so they find the connection open, and the network thread reads nothing more, not even the server's close, until it
 * returns. It names no class of Minecraft, whose names change in every release.
 */
public final class QueuedPackets {
    // How long the network thread waits for a client thread that is busy or waits on the network thread itself, as it does when it closes the connection.
    private static final long TIMEOUT_MILLISECONDS = 1000;

    private static final Runnable NOTHING = new Runnable() {
        @Override
        public void run() {
        }
    };

    private QueuedPackets() {
    }

    /** Waits until the client from 1.14, whose executor runs tasks in the order they are queued, has run the tasks queued before this call, or for at most a second. */
    public static void awaitHandled(Executor client) {
        FutureTask<Object> handled = new FutureTask<Object>(NOTHING, null);

        client.execute(handled);
        awaitHandled(handled);
    }

    /**
     * Gets a task that does nothing, for a Minecraft 1.13 client, which is no executor: its handleDisconnect queues it with the client's own method, which runs
     * tasks in the order they are queued, and hands the future that method returns to {@link #awaitHandled(Future)}.
     */
    public static Runnable nothing() {
        return NOTHING;
    }

    /** Waits until the client thread has run the task of the future, which it queued after the packets queued before it, or for at most a second. */
    public static void awaitHandled(Future<?> handled) {
        try {
            handled.get(TIMEOUT_MILLISECONDS, TimeUnit.MILLISECONDS);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        } catch (ExecutionException exception) {
            // The task does nothing, so it never fails.
        } catch (TimeoutException exception) {
            // The disconnect goes ahead, and the packets still queued are dropped as before.
        }
    }
}
