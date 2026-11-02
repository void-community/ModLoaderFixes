package connecting;

import java.util.concurrent.atomic.AtomicReference;

/**
 * Minecraft 1.9's connecting screen with the NetworkManager its argument names: the connector thread sends the handshake before the channel is active, so it
 * waits in the queue, and the login start once it is, which flushes the queue; the client thread's tick of the screen flushes it at the same time.
 */
public final class ConnectingScreen {
    private ConnectingScreen() {
    }

    public static void main(String[] arguments) throws Exception {
        final Connection connection = (Connection) Class.forName(arguments[0]).newInstance();
        final AtomicReference<Throwable> failure = new AtomicReference<Throwable>();

        connection.send("handshake");
        connection.open();

        Thread connector = new Thread("Server Connector #1") {
            @Override
            public void run() {
                try {
                    connection.send("login start");
                } catch (Throwable throwable) {
                    failure.compareAndSet(null, throwable);
                }
            }
        };

        connector.start();

        try {
            connection.tick();
        } catch (Throwable throwable) {
            failure.compareAndSet(null, throwable);
        }

        connector.join();

        String result = arguments[0] + (failure.get() == null ? " wrote " + connection.written() : " crashed with " + failure.get());

        System.out.println(result);
        System.exit(result.endsWith(" wrote [handshake, login start]") ? 0 : 1);
    }
}
