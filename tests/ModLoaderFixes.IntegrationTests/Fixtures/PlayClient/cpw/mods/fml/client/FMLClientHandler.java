package cpw.mods.fml.client;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

/**
 * Forge 1.7.2's wait for the play client: the network thread waits a second on playClientBlock, which the client thread counts down once its network tick has set
 * the play handler up, and a new connection replaces. Its main method joins while the client thread is busy for two seconds, then starts a connection whose
 * client thread never catches up and connects again before it does.
 */
public class FMLClientHandler {
    private static final FMLClientHandler INSTANCE = new FMLClientHandler();

    private CountDownLatch playClientBlock;

    public static FMLClientHandler instance() {
        return INSTANCE;
    }

    public void connectToServer() {
        playClientBlock = new CountDownLatch(1);
    }

    public void setPlayClient() {
        playClientBlock.countDown();
    }

    public void waitForPlayClient() {
        boolean arrived = false;

        try {
            arrived = playClientBlock.await(1, TimeUnit.SECONDS);
        } catch (InterruptedException exception) {
        }

        if (!arrived)
            throw new RuntimeException("Timeout waiting for client thread to catch up!");
    }

    public static void main(String[] arguments) throws InterruptedException {
        FMLClientHandler handler = instance();

        handler.connectToServer();
        Handshake busy = Handshake.start(handler);
        // The client thread is still busy, as right after a previous connection ended, before its network tick sets the play handler up.
        Thread.sleep(2000);
        handler.setPlayClient();
        busy.thread.join();

        handler.connectToServer();
        Handshake abandoned = Handshake.start(handler);
        Thread.sleep(300);
        // The player cancels and connects again, so the client thread never ticks the first connection.
        handler.connectToServer();
        abandoned.thread.join();

        String abandonedResult = abandoned.elapsedMillis < 5000 ? "given up" : "waited " + abandoned.elapsedMillis + " ms";

        System.out.println("busy client thread: " + busy.result + "; abandoned connection: " + abandonedResult);
        System.exit(busy.result.equals("joined") && abandonedResult.equals("given up") ? 0 : 1);
    }

    // FML's handshake on the network thread, which waits for the play client once the server says the login succeeded.
    private static final class Handshake implements Runnable {
        private final FMLClientHandler handler;
        private final Thread thread = new Thread(this, "Netty Client IO #0");
        private volatile String result;
        private volatile long elapsedMillis;

        private Handshake(FMLClientHandler handler) {
            this.handler = handler;
        }

        static Handshake start(FMLClientHandler handler) {
            Handshake handshake = new Handshake(handler);

            handshake.thread.start();

            return handshake;
        }

        @Override
        public void run() {
            long start = System.nanoTime();

            try {
                handler.waitForPlayClient();
                result = "joined";
            } catch (RuntimeException exception) {
                result = exception.getMessage();
            }

            elapsedMillis = TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - start);
        }
    }
}
