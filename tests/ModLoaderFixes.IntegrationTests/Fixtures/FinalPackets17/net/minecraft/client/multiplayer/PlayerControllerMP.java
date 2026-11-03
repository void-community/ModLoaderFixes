package net.minecraft.client.multiplayer;

import net.minecraft.client.network.NetHandlerPlayClient;
import net.minecraft.network.NetworkManager;

/**
 * Ticks the client's connection as Minecraft 1.7's PlayerControllerMP.updateController does: it processes the received packets while the connection is open and
 * otherwise reports the disconnect, so a chat that the network thread queued before a Disconnect closed the connection is never processed. The network thread
 * reads the chat, the Disconnect and the server's close one after the other, as it does when a server explains a kick.
 */
public class PlayerControllerMP {
    private final NetHandlerPlayClient field_78774_b;
    private int field_78777_l;

    public PlayerControllerMP(NetHandlerPlayClient client) {
        this.field_78774_b = client;
    }

    /** updateController. */
    public void func_78765_e() {
        this.func_78750_j();

        if (this.field_78774_b.func_147298_b().func_150724_d()) {
            this.field_78774_b.func_147298_b().func_74428_b();
        } else if (this.field_78774_b.func_147298_b().func_150730_f() != null) {
            this.field_78774_b.onDisconnect(this.field_78774_b.func_147298_b().func_150730_f());
        } else {
            this.field_78774_b.onDisconnect("Disconnected from server");
        }
    }

    /** syncCurrentPlayItem. */
    private void func_78750_j() {
        this.field_78777_l++;
    }

    public static void main(String[] arguments) throws InterruptedException {
        final NetworkManager manager = new NetworkManager();
        final NetHandlerPlayClient client = new NetHandlerPlayClient(manager);
        PlayerControllerMP controller = new PlayerControllerMP(client);

        manager.receive(chat(client, "Welcome"));
        controller.func_78765_e();

        Thread network = new Thread(new Runnable() {
            @Override
            public void run() {
                manager.receive(chat(client, "Stopping proxy..."));
                client.handleDisconnect("Proxy is shutting down");
                manager.func_150718_a("disconnect.endOfStream");
            }
        }, "Netty Client IO #0");

        network.start();
        network.join();
        controller.func_78765_e();

        String result = client.shown + " " + client.disconnectedReason;

        if (!client.shown.contains("[CHAT] Stopping proxy...")) {
            System.out.println("DROPPED " + result);
            System.exit(1);
        }

        System.out.println("SHOWN " + result);
    }

    private static Runnable chat(final NetHandlerPlayClient client, final String message) {
        return new Runnable() {
            @Override
            public void run() {
                client.handleChat(message);
            }
        };
    }
}
