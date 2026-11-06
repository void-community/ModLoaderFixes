package net.minecraft.client.multiplayer;

import java.util.concurrent.atomic.AtomicReference;

import com.mojang.patchy.BlockedServers;

/**
 * Minecraft 1.12 to 1.16.5's connecting screen, whose thread connects through patchy's Bootstrap, which checks the address against BlockedServers. main connects
 * twice from a thread that catches exceptions only, as the screen's does, while the list's server cuts its answer off.
 */
public class GuiConnecting {
    public static void main(String[] arguments) throws Exception {
        System.setProperty("blockedservers.address", ResetServer.start());

        String first = connect();
        String second = connect();

        System.out.println("first connection: " + first + "; second connection: " + second);
        System.exit(first.startsWith("connected") && second.startsWith("connected") ? 0 : 1);
    }

    // The screen's thread: it checks the address as patchy's Bootstrap.connect does, and reports an exception, but an error ends the thread.
    private static String connect() throws InterruptedException {
        final AtomicReference<String> result = new AtomicReference<String>();
        Thread connector = new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    result.set("connected, " + (BlockedServers.isBlockedServer("127.0.0.1") ? "blocked" : "not blocked"));
                } catch (Exception exception) {
                    result.set("failed: " + exception);
                }
            }
        }, "Server Connector");

        connector.setUncaughtExceptionHandler((thread, error) -> result.set("connector died: " + error + (error.getCause() == null ? "" : " from " + error.getCause())));
        connector.start();
        connector.join();
        return result.get();
    }
}
