package net.minecraft.server.v1_7_R4;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Iterator;
import java.util.List;

import net.minecraft.util.io.netty.channel.Channel;

/**
 * PaperSpigot 1.7.10's network tick, by CraftBukkit's names: it keeps a connection whose channel isn't open while Spigot's preparing flag is set, drops the
 * other closed ones and processes the rest. Its main method ticks while a new connection's channelActive runs between the tick's check of the channel and its
 * read of the flag, then lets the connection's login start arrive and ticks again.
 */
public class ServerConnection {
    private final List<NetworkManager> f = Collections.synchronizedList(new ArrayList<NetworkManager>());

    public void c() {
        synchronized (f) {
            Iterator<NetworkManager> iterator = f.iterator();

            while (iterator.hasNext()) {
                NetworkManager manager = iterator.next();

                if (!manager.isConnected()) {
                    if (manager.preparing)
                        continue;

                    iterator.remove();
                    manager.disconnected();
                } else {
                    manager.a();
                }
            }
        }
    }

    public static void main(String[] arguments) {
        ServerConnection connection = new ServerConnection();
        NetworkManager joining = new NetworkManager();

        connection.f.add(joining);
        joining.activateAfterCheck(new Channel() {
            @Override
            public boolean isOpen() {
                return true;
            }
        });
        connection.c();
        joining.finishActivation();
        joining.receive("login start");
        connection.c();

        String result = joining.describe("joining");

        System.out.println(result);
        System.exit(result.equals("joining kept, answered [login start]") ? 0 : 1);
    }
}
