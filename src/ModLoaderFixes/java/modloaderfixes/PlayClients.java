package modloaderfixes;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import cpw.mods.fml.client.FMLClientHandler;

/**
 * Waits for a Forge 1.7 client thread to set the play handler up as long as the server would keep the connection. FML's handshake waits for it on the network
 * thread, for one second in Forge for 1.7.2 and five in 1.7.10, and gives the connection up when the client thread is busy for longer. A server drops a connection
 * it has read nothing from for 30 seconds, and the client sends nothing while it waits, so waiting longer would not keep the connection. The network thread can't
 * see its channel close while it waits, since the close runs on that thread, so it stops early only when the client has started a new connection, which gives
 * FMLClientHandler a new latch. It is defined in FMLClientHandler's loader, and reads playClientBlock, which is private, through the getter the patch adds.
 */
public final class PlayClients {
    private static final long LIMIT = TimeUnit.SECONDS.toNanos(30);
    private static final long SLICE = TimeUnit.MILLISECONDS.toNanos(100);

    private PlayClients() {
    }

    /**
     * Stands in for latch.await(timeout, unit) in waitForPlayClient: true once the client thread counts the latch down, false when the wait is given up. Before the
     * first connection has made a latch there is none to wait on, and FML would throw a NullPointerException, so it returns true and leaves the wait for the
     * connection's play handler to PlayHandlers.
     */
    public static boolean await(CountDownLatch latch, long timeout, TimeUnit unit) throws InterruptedException {
        if (latch == null)
            return true;

        long deadline = System.nanoTime() + Math.max(LIMIT, unit.toNanos(timeout));

        while (!latch.await(SLICE, TimeUnit.NANOSECONDS)) {
            if (FMLClientHandler.instance().modloaderfixes$playClientBlock() != latch || System.nanoTime() - deadline >= 0)
                return false;
        }

        return true;
    }
}
