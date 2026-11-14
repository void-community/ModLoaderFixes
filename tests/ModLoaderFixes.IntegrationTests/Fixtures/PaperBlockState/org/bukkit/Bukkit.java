package org.bukkit;

import java.util.logging.Logger;

/** Bukkit's entry point as far as the agent's helper uses it. The fixture's main thread is the server thread until it says otherwise. */
public final class Bukkit {
    private static volatile Thread primary = Thread.currentThread();

    private Bukkit() {
    }

    public static Logger getLogger() {
        return Logger.getLogger("Minecraft");
    }

    public static boolean isPrimaryThread() {
        return Thread.currentThread() == primary;
    }

    /** Makes another thread the server thread, so the caller reads as a plugin's own thread does. */
    public static void setPrimaryThread(Thread thread) {
        primary = thread;
    }
}
