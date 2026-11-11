package cpw.mods.fml.client;

import java.lang.ref.WeakReference;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import cpw.mods.fml.common.network.handshake.NetworkDispatcher;
import net.minecraft.client.network.NetHandlerPlayClient;
import net.minecraft.network.INetHandler;
import net.minecraft.network.NetworkManager;

/**
 * Forge 1.7.10's FMLClientHandler: connectToServer shows the connecting screen, which starts the connection, and only then gives playClientBlock a new latch;
 * setPlayClient counts the latch down and then stores the play client. main connects again and again, as a game that joins servers does, and each time the
 * server answers while the client thread is still busy showing the screen, so the network thread waits for the play client before the latch is replaced: on the
 * first connection there is no latch yet, on the second it is the first connection's, which is counted down already, and after a login the server refused it is
 * that connection's, which nothing counts down. A REGISTER then casts the connection's handler to NetHandlerPlayClient, as FML's CustomPacketRegistrationEvent
 * does. Last, the client cancels a connection while its network thread waits for the play client, and connects no more.
 */
public class FMLClientHandler {
    private static final FMLClientHandler INSTANCE = new FMLClientHandler();
    private static final long BUSY_MILLISECONDS = 300;

    private CountDownLatch playClientBlock;
    private WeakReference<NetHandlerPlayClient> currentPlayClient;

    public static FMLClientHandler instance() {
        return INSTANCE;
    }

    public void connectToServer(Handshake connecting) {
        showGuiScreen(connecting);
        playClientBlock = new CountDownLatch(1);
    }

    public void setPlayClient(NetHandlerPlayClient playClient) {
        if (playClientBlock == null)
            playClientBlock = new CountDownLatch(1);

        playClientBlock.countDown();
        currentPlayClient = new WeakReference<NetHandlerPlayClient>(playClient);
    }

    public INetHandler getClientPlayHandler() {
        return currentPlayClient == null ? null : currentPlayClient.get();
    }

    public void waitForPlayClient() {
        boolean gotIt = false;

        try {
            gotIt = playClientBlock.await(5L, TimeUnit.SECONDS);
        } catch (InterruptedException exception) {
        }

        if (!gotIt)
            throw new RuntimeException("Timeout waiting for client thread to catch up!");
    }

    public void fireNetRegistrationEvent(NetworkManager manager) {
        waitForPlayClient();
        NetHandlerPlayClient.class.cast(manager.func_150729_e());
    }

    public static void main(String[] arguments) throws InterruptedException {
        String first = connect();
        String second = connect();

        refuse();

        String afterRefusal = connect();
        String cancelled = cancel();

        System.out.println("first connection: " + first + "; second connection: " + second + "; after a refused login: " + afterRefusal + "; cancelled connection: " + cancelled);
        System.exit(first.equals("joined") && second.equals("joined") && afterRefusal.equals("joined") && cancelled.equals("let go") ? 0 : 1);
    }

    // A connection whose server refuses the login, as for a banned player: the network thread never reaches Login Success, and the client thread never sets a
    // play handler up, so nothing counts its latch down.
    private static void refuse() throws InterruptedException {
        Handshake refused = new Handshake(null);

        INSTANCE.connectToServer(refused);
        refused.thread.join();
    }

    // A connection whose server answers, and which the client cancels while the network thread waits for the play client, as the connecting screen's cancel does;
    // the client thread never ticks it. Its network thread is let go when it ends within a second of the cancel.
    private static String cancel() throws InterruptedException {
        NetworkManager manager = new NetworkManager();
        Handshake handshake = new Handshake(new NetworkDispatcher(manager));

        INSTANCE.connectToServer(handshake);
        manager.func_150718_a(new net.minecraft.util.IChatComponent() {
        });

        long cancelled = System.nanoTime();

        handshake.thread.join(10000);

        long held = TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - cancelled);

        return held < 1000 ? "let go" : "held " + (handshake.thread.isAlive() ? "over 10 s" : held + " ms");
    }

    // One join on the client thread: the connecting screen starts the connection, whose server answers at once, and the client thread's next network tick
    // after it has shown the screen sets the play handler up.
    private static String connect() throws InterruptedException {
        NetworkManager manager = new NetworkManager();
        Handshake handshake = new Handshake(new NetworkDispatcher(manager));

        INSTANCE.connectToServer(handshake);
        Thread.sleep(100);
        manager.func_74428_b();
        handshake.thread.join();

        return handshake.result;
    }

    // The connecting screen starts the connection, and showing it keeps the client thread busy a while, as a slow or loaded game's does.
    private void showGuiScreen(Handshake connecting) {
        connecting.thread.start();

        try {
            Thread.sleep(BUSY_MILLISECONDS);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }

    // The network thread's part of a join: the server's Login Success, then its REGISTER, and a check that the handshake took this connection's play client.
    static final class Handshake implements Runnable {
        private final NetworkDispatcher dispatcher;
        private final Thread thread = new Thread(this, "Netty Client IO #1");
        private volatile String result;

        Handshake(NetworkDispatcher dispatcher) {
            this.dispatcher = dispatcher;
        }

        @Override
        public void run() {
            if (dispatcher == null) {
                result = "refused";
                return;
            }

            try {
                dispatcher.handleLoginSuccessThenRegister();

                INetHandler handler = dispatcher.getNetHandler();
                boolean own = handler instanceof NetHandlerPlayClient && ((NetHandlerPlayClient) handler).func_147298_b() == dispatcher.manager;

                result = own ? "joined" : "joined with another connection's play client";
            } catch (RuntimeException exception) {
                result = exception.toString();
            }
        }
    }
}
