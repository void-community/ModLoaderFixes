package com.mojang.patchy;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.net.URL;
import java.net.URLConnection;
import java.nio.charset.StandardCharsets;
import java.util.HashSet;
import java.util.Set;

/** patchy 1.3.9's list of blocked servers, which its static initializer reads from the address in blockedservers.address, and leaves empty when it can't. */
public class BlockedServers {
    static final Set<String> BLOCKED_SERVERS = new HashSet<String>();

    public static boolean isBlockedServer(String server) {
        return BLOCKED_SERVERS.contains(server);
    }

    static {
        try {
            URLConnection urlConnection = new URL(System.getProperty("blockedservers.address")).openConnection();

            try (InputStream is = urlConnection.getInputStream()) {
                BufferedReader reader = new BufferedReader(new InputStreamReader(is, StandardCharsets.ISO_8859_1));

                reader.lines().forEach(BLOCKED_SERVERS::add);
            }
        } catch (IOException exception) {
        }
    }
}
