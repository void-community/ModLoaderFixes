package net.minecraft.client.network;

import java.util.List;
import java.util.concurrent.CopyOnWriteArrayList;

import net.minecraft.client.Minecraft;
import net.minecraft.network.NetworkManager;
import net.minecraft.network.play.server.SPacketDisconnect;

/**
 * Handles a server's packets as Minecraft 1.13's NetHandlerPlayClient does: chat goes to the client thread through the client's own queue, while the Disconnect
 * closes the connection at once on the network thread. Once the client's tick finds the connection closed, the client leaves the world and drops the tasks still
 * queued. The network thread reads the chat, the Disconnect and the server's close one after the other, as it does when a server explains a kick, while the
 * client is in a tick.
 */
public class NetHandlerPlayClient {
    private static final List<String> shown = new CopyOnWriteArrayList<String>();

    private Minecraft field_147299_f;
    private final NetworkManager field_147302_e;
    private volatile String disconnectedReason;

    public NetHandlerPlayClient(Minecraft client, NetworkManager manager) {
        this.field_147299_f = client;
        this.field_147302_e = manager;
    }

    public void handleChat(final String message) {
        this.field_147299_f.func_152344_a(new Runnable() {
            @Override
            public void run() {
                shown.add("[CHAT] " + message);
            }
        });
    }

    /** handleDisconnect. */
    public void func_147253_a(SPacketDisconnect packet) {
        this.field_147302_e.func_150718_a(packet.getReason());
    }

    /** onDisconnect, which leaves the world. */
    public void func_147231_a(String reason) {
        this.field_147299_f.func_71403_a();
        this.disconnectedReason = reason;
    }

    public static void main(String[] arguments) throws InterruptedException {
        final Minecraft client = new Minecraft();
        final NetworkManager manager = new NetworkManager();
        final NetHandlerPlayClient listener = new NetHandlerPlayClient(client, manager);

        Thread network = new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    client.ticking.await();
                } catch (InterruptedException exception) {
                    return;
                }

                listener.handleChat("Stopping proxy...");
                listener.func_147253_a(new SPacketDisconnect("Proxy is shutting down"));
                manager.func_150718_a("disconnect.endOfStream");
            }
        }, "Netty Client IO #0");

        network.start();

        Runnable tick = new Runnable() {
            @Override
            public void run() {
                if (!manager.func_150724_d())
                    listener.func_147231_a(manager.func_150730_f());
            }
        };

        for (int frame = 0; frame < 50 && listener.disconnectedReason == null; frame++)
            client.frame(tick);

        network.join();

        String result = shown + " " + listener.disconnectedReason;

        if (!shown.contains("[CHAT] Stopping proxy...")) {
            System.out.println("DROPPED " + result);
            System.exit(1);
        }

        System.out.println("SHOWN " + result);
    }
}
