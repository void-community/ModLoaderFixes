package net.minecraft.client.gui.screens;

import java.util.concurrent.atomic.AtomicReference;

import com.mojang.patchy.MojangBlockListSupplier;
import net.minecraft.client.multiplayer.resolver.ServerNameResolver;

/**
 * Minecraft 1.17 and later's connecting screen. main connects twice from a connector thread that catches exceptions only, as ConnectScreen's does, and resolves
 * the address through ServerNameResolver's default instance, while the list's server cuts its answer off.
 */
public class ConnectScreen {
    public static void main(String[] arguments) throws Exception {
        MojangBlockListSupplier.address = ResetServer.start();

        String first = connect();
        String second = connect();

        System.out.println("first connection: " + first + "; second connection: " + second);
        System.exit(first.startsWith("connected") && second.startsWith("connected") ? 0 : 1);
    }

    // The connector reports an exception to the screen, but an error ends the thread and the screen never learns.
    private static String connect() throws InterruptedException {
        final AtomicReference<String> result = new AtomicReference<String>();
        Thread connector = new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    result.set("connected, " + (ServerNameResolver.DEFAULT.hasBlockList() ? "with a list" : "with no list"));
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
