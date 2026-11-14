package org.bukkit;

import java.util.logging.Logger;

/** The part of Bukkit's entry point that the helpers use: the server's logger and whether the caller is the server thread. */
public final class Bukkit {
    private Bukkit() {
    }

    public static Logger getLogger() {
        throw new UnsupportedOperationException();
    }

    public static boolean isPrimaryThread() {
        throw new UnsupportedOperationException();
    }
}
