package net.minecraft.network;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.net.StandardSocketOptions;
import java.nio.ByteBuffer;
import java.nio.channels.SelectionKey;
import java.nio.channels.Selector;
import java.nio.channels.ServerSocketChannel;
import java.nio.channels.SocketChannel;

import io.netty.channel.Channel;
import io.netty.channel.socket.nio.NioSocketChannel;
import net.minecraft.util.IChatComponent;

/**
 * Minecraft 1.7's connection, by FML's names, as far as a kick goes: closeChannel closes the channel when it is open. main is a server that kicks a player the
 * way NetHandlerPlayServer.kickPlayerFromServer does, over real connections on the loopback interface, each a non-blocking channel registered with a selector
 * as Netty's are. The player and the server first exchange a few small packets, so the player's kernel delays its acknowledgements as on any connection in
 * play, and the player then sends its next movement packet, which the server leaves unread, as it stops reading a player it kicks. The server writes a chat
 * packet and then the Disconnect packet, and closes the connection as soon as that is written. The first connections have Nagle's algorithm on, as Minecraft
 * 1.7.2 to 1.8 set it, and the rest have it off, as Minecraft from 1.8.1 does; each player reports whether the Disconnect packet reached it and whether its
 * connection ended in a reset.
 */
public class NetworkManager {
    private static final int CHAT = 31;
    private static final int DISCONNECT = 60;
    private static final int EXCHANGES = 8;
    private static final int KICKS = 20;
    private static final int TIMEOUT_MILLISECONDS = 20000;

    private final Channel field_150746_k;
    private IChatComponent field_150742_o;

    public NetworkManager(Channel channel) {
        field_150746_k = channel;
    }

    /** closeChannel. */
    public void func_150718_a(IChatComponent reason) {
        if (this.field_150746_k.isOpen()) {
            this.field_150746_k.close();
            this.field_150742_o = reason;
        }
    }

    public static void main(String[] arguments) throws Exception {
        ServerSocketChannel listener = ServerSocketChannel.open();

        listener.bind(new InetSocketAddress("127.0.0.1", 0));

        int port = ((InetSocketAddress) listener.getLocalAddress()).getPort();
        Selector selector = Selector.open();
        int[] held = kick(listener, selector, port, false);
        int[] sent = kick(listener, selector, port, true);

        System.out.println("Nagle on: reason received " + held[0] + " of " + KICKS + ", reset " + held[1] + " times; Nagle off: reason received " + sent[0] + " of "
            + KICKS + ", reset " + sent[1] + " times");
        System.exit(held[0] == KICKS && sent[0] == KICKS ? 0 : 1);
    }

    // Kicks one player after another, and returns how many got the Disconnect packet and how many saw their connection reset.
    private static int[] kick(ServerSocketChannel listener, Selector selector, int port, boolean noDelay) throws Exception {
        int[] totals = new int[2];

        for (int kick = 0; kick < KICKS; kick++) {
            Player player = new Player(port);

            player.start();

            SocketChannel accepted = listener.accept();

            accepted.configureBlocking(false);
            accepted.setOption(StandardSocketOptions.TCP_NODELAY, noDelay);

            SelectionKey key = accepted.register(selector, SelectionKey.OP_READ);
            ByteBuffer packet = ByteBuffer.allocate(1);

            for (int exchange = 0; exchange < EXCHANGES; ) {
                if (selector.select(TIMEOUT_MILLISECONDS) == 0)
                    throw new IllegalStateException("The player sent nothing");

                selector.selectedKeys().clear();
                packet.clear();

                if (accepted.read(packet) == 1) {
                    packet.flip();
                    accepted.write(packet);
                    exchange++;
                }
            }

            // The player's next movement packet arrives, and stays unread.
            if (selector.select(TIMEOUT_MILLISECONDS) == 0)
                throw new IllegalStateException("The player's last packet didn't arrive");

            selector.selectedKeys().clear();

            NetworkManager manager = new NetworkManager(new NioSocketChannel(accepted));

            accepted.write(ByteBuffer.allocate(CHAT));
            accepted.write(ByteBuffer.allocate(DISCONNECT));
            manager.func_150718_a(new IChatComponent() {
            });
            // Netty's event loop selects again after the close, which is when a JDK that puts the close of a registered channel off closes its socket.
            selector.selectNow();
            player.join();

            if (key.isValid() || manager.field_150742_o == null)
                throw new IllegalStateException("The connection wasn't closed");

            totals[0] += player.received == CHAT + DISCONNECT ? 1 : 0;
            totals[1] += player.reset ? 1 : 0;
        }

        return totals;
    }

    // The kicked player: its client reads everything the server sends until the connection ends.
    private static final class Player extends Thread {
        private final int port;
        private volatile int received;
        private volatile boolean reset;

        Player(int port) {
            super("Netty Client IO");
            this.port = port;
        }

        @Override
        public void run() {
            try (Socket socket = new Socket("127.0.0.1", port)) {
                socket.setTcpNoDelay(true);
                socket.setSoTimeout(TIMEOUT_MILLISECONDS);

                InputStream in = socket.getInputStream();
                OutputStream out = socket.getOutputStream();

                for (int exchange = 0; exchange < EXCHANGES; exchange++) {
                    out.write(1);
                    in.read();
                }

                out.write(new byte[] {2, 3, 1});

                byte[] buffer = new byte[4096];
                int total = 0;

                try {
                    for (int read = in.read(buffer); read >= 0; read = in.read(buffer))
                        total += read;
                } catch (IOException exception) {
                    reset = true;
                }

                received = total;
            } catch (IOException exception) {
                received = -1;
            }
        }
    }
}
