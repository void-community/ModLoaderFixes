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
 * setPlayClient counts the latch down and then stores the play client. main connects twice, as a game that joins a server again does, and each time the server
 * answers while the client thread is still busy showing the screen, so the network thread waits for the play client before the latch is replaced: on the first
 * connection there is no latch yet, and on the second it is the first connection's, which is counted down already. A REGISTER then casts the connection's
 * handler to NetHandlerPlayClient, as FML's CustomPacketRegistrationEvent does.
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

        System.out.println("first connection: " + first + "; second connection: " + second);
        System.exit(first.equals("joined") && second.equals("joined") ? 0 : 1);
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
