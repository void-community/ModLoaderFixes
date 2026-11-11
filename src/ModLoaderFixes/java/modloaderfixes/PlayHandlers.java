package modloaderfixes;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import cpw.mods.fml.client.FMLClientHandler;
import cpw.mods.fml.common.network.handshake.NetworkDispatcher;
import net.minecraft.client.network.NetHandlerPlayClient;
import net.minecraft.network.INetHandler;
import net.minecraft.network.NetworkManager;

/**
 * Makes a Forge 1.7 client's handshake go on only once its own connection has the play handler. Once the server says the login succeeded, the network thread
 * waits on FMLClientHandler's playClientBlock for the client thread's network tick to set the play handler up, then takes the play client FMLClientHandler last
 * stored. connectToServer replaces the latch only after the new connection has started, so a server that answers first lets the network thread read the latch of
 * the previous connection, which is counted down already, or which a login the server refused never counts down, or none on the first connection; the handshake
 * then takes the previous connection's play client,
 * and the server's next messages find the login handler still on the connection. This runs on the network thread after NetworkDispatcher's
 * clientListenForServerHandshake returns, and waits until the dispatcher's connection has its play handler, which it then makes the dispatcher's. The client
 * thread counts the latch it has by then down right after it sets the handler, so a wait on that latch also sees the handler. The channel only closes on the
 * network thread, which this blocks, so it tells a connection the client has closed, as the connecting screen's cancel does, by the exit message that
 * closeChannel stores on the client thread, and gives that connection's handshake up with FML's own timeout, which its next wait for the play client would end in
 * anyway. It is defined in the dispatcher's loader and stores netHandler, which is private, through the setter the patch adds.
 */
public final class PlayHandlers {
    private static final long LIMIT = TimeUnit.SECONDS.toNanos(30);
    private static final long PAUSE_MILLISECONDS = 10;
    private static final long SLICE = TimeUnit.MILLISECONDS.toNanos(100);

    private PlayHandlers() {
    }

    /**
     * Runs after clientListenForServerHandshake: waits up to the server's read timeout for the connection's play handler, and throws FML's timeout once the client
     * has closed the connection.
     */
    public static void listenedForServerHandshake(NetworkDispatcher dispatcher) {
        NetworkManager manager = dispatcher.manager;
        long deadline = System.nanoTime() + LIMIT;
        INetHandler handler = manager.func_150729_e();

        try {
            while (!(handler instanceof NetHandlerPlayClient)) {
                if (manager.func_150730_f() != null)
                    throw new RuntimeException("Timeout waiting for client thread to catch up!");

                if (!manager.func_150724_d() || System.nanoTime() - deadline >= 0)
                    return;

                CountDownLatch latch = FMLClientHandler.instance().modloaderfixes$playClientBlock();

                // A latch counted down already is the previous connection's, until the client thread replaces it, and gives nothing to wait on.
                if (latch == null || latch.getCount() == 0)
                    Thread.sleep(PAUSE_MILLISECONDS);
                else
                    latch.await(SLICE, TimeUnit.NANOSECONDS);

                handler = manager.func_150729_e();
            }
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            return;
        }

        dispatcher.modloaderfixes$netHandler(handler);
    }
}
