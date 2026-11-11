package modloaderfixes;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import cpw.mods.fml.client.FMLClientHandler;

/**
 * Waits for a Forge 1.7 client thread to set the play handler up as long as the server would keep the connection. FML's handshake waits for it on the network
 * thread, for one second in Forge for 1.7.2 and five in 1.7.10, and gives the connection up when the client thread is busy for longer. A server drops a connection
 * it has read nothing from for 30 seconds, and the client sends nothing while it waits, so waiting longer would not keep the connection. The latch it waits on
 * stops being the client's when FMLClientHandler.connectToServer gives playClientBlock a new one, which it does only after the new connection has started. So a
 * connection whose server answers first can be waiting on the latch of the connection before it, which a login the server refused never counts down, until its
 * own connectToServer replaces it. A replaced latch is then no reason to give the connection up: the wait ends there, and PlayHandlers, after the handshake's
 * wait, waits for the connection's own play handler or lets go of a connection the client has closed. It is defined in FMLClientHandler's loader, and reads
 * playClientBlock, which is private, through the getter the patch adds.
 */
public final class PlayClients {
    private static final long LIMIT = TimeUnit.SECONDS.toNanos(30);
    private static final long SLICE = TimeUnit.MILLISECONDS.toNanos(100);

    private PlayClients() {
    }

    /**
     * Stands in for latch.await(timeout, unit) in waitForPlayClient: true once the client thread counts the latch down or replaces it, false when the wait lasts
     * as long as the server would keep the connection. Before the first connection has made a latch there is none to wait on, and FML would throw a
     * NullPointerException, so it returns true at once; either way PlayHandlers waits for the connection's own play handler.
     */
    public static boolean await(CountDownLatch latch, long timeout, TimeUnit unit) throws InterruptedException {
        if (latch == null)
            return true;

        long deadline = System.nanoTime() + Math.max(LIMIT, unit.toNanos(timeout));

        while (!latch.await(SLICE, TimeUnit.NANOSECONDS)) {
            if (FMLClientHandler.instance().modloaderfixes$playClientBlock() != latch)
                return true;

            if (System.nanoTime() - deadline >= 0)
                return false;
        }

        return true;
    }
}
