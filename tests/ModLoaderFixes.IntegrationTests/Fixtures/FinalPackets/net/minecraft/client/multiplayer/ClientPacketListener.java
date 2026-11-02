package net.minecraft.client.multiplayer;

import java.util.List;
import java.util.concurrent.CopyOnWriteArrayList;

import net.minecraft.client.Minecraft;
import net.minecraft.network.Connection;
import net.minecraft.network.protocol.game.ClientboundDisconnectPacket;

/**
 * Handles a server's packets as Minecraft's ClientPacketListener does from 1.17 to 1.20.1, and its predecessor from 1.14.4: chat goes to the Render thread,
 * which drops it once the connection is closed, as PacketUtils.ensureRunningOnSameThread's task does, while the Disconnect closes the connection at once on the
 * network thread. The network thread reads the chat, the Disconnect and the server's close one after the other, as it does when a server explains a kick.
 */
public class ClientPacketListener {
    private static final List<String> shown = new CopyOnWriteArrayList<>();

    private final Minecraft minecraft;
    private final Connection connection;

    public ClientPacketListener(Minecraft minecraft, Connection connection) {
        this.minecraft = minecraft;
        this.connection = connection;
    }

    public void handleChat(String message) {
        this.minecraft.execute(() -> {
            if (this.connection.isConnected())
                shown.add("[CHAT] " + message);
            else
                shown.add("Ignoring packet due to disconnection: " + message);
        });
    }

    public void handleDisconnect(ClientboundDisconnectPacket packet) {
        this.connection.disconnect(packet.getReason());
    }

    public static void main(String[] arguments) throws InterruptedException {
        Minecraft minecraft = new Minecraft();
        Connection connection = new Connection();
        ClientPacketListener listener = new ClientPacketListener(minecraft, connection);

        minecraft.renderThread.start();

        Thread network = new Thread(() -> {
            listener.handleChat("Stopping proxy...");
            listener.handleDisconnect(new ClientboundDisconnectPacket("Proxy is shutting down"));
            connection.disconnect("disconnect.endOfStream");
        }, "Netty Client IO #0");

        network.start();
        network.join();
        Thread.sleep(200);
        minecraft.stop();

        String result = shown + " " + connection.getDisconnectedReason();

        if (!shown.contains("[CHAT] Stopping proxy...")) {
            System.out.println("DROPPED " + result);
            System.exit(1);
        }

        System.out.println("SHOWN " + result);
    }
}
