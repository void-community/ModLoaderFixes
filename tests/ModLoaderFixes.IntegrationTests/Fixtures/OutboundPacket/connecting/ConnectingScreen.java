package connecting;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import net.minecraft.network.Packet;

/**
 * Minecraft 1.7's connecting screen with the NetworkManager its argument names: the connector thread sends the handshake before the channel is active, so it
 * waits in the queue, and the login start after. The client thread's tick of the screen flushes the queue meanwhile; the test lets the login start go out right
 * after the tick has read the channel's state for the handshake, as the connector thread may.
 */
public final class ConnectingScreen {
    private ConnectingScreen() {
    }

    public static void main(String[] arguments) throws Exception {
        final Connection connection = (Connection) Class.forName(arguments[0]).newInstance();
        final Wire wire = new Wire();
        final CountDownLatch handshakeQueued = new CountDownLatch(1);
        final CountDownLatch loginStartDue = new CountDownLatch(1);
        final CountDownLatch loginStartSent = new CountDownLatch(1);

        Thread connector = new Thread("Server Connector #1") {
            @Override
            public void run() {
                connection.send(Packet.HANDSHAKE);
                handshakeQueued.countDown();
                awaitUninterruptibly(loginStartDue, 10);
                connection.send(Packet.LOGIN_START);
                loginStartSent.countDown();
            }
        };

        connector.start();
        handshakeQueued.await();
        connection.open(wire);
        wire.afterStateRead(new Runnable() {
            @Override
            public void run() {
                loginStartDue.countDown();
                awaitUninterruptibly(loginStartSent, 1);
            }
        });
        connection.tick();
        connector.join();

        String result = arguments[0] + " " + wire.drain();

        System.out.println(result);
        System.exit(result.endsWith(" sent [handshake, login start]") ? 0 : 1);
    }

    private static void awaitUninterruptibly(CountDownLatch latch, int seconds) {
        try {
            latch.await(seconds, TimeUnit.SECONDS);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }
}
