package com.mojang.patchy;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.net.URL;
import java.net.URLConnection;
import java.nio.charset.StandardCharsets;
import java.util.function.Predicate;
import java.util.stream.Collectors;

/** patchy 2.2.10's supplier, which reads the list from the address the fixture's server gives, and gives no list when it can't fetch it. */
public class MojangBlockListSupplier {
    public static volatile String address;

    public Predicate<String> createBlockList() {
        try {
            URLConnection urlConnection = new URL(address).openConnection();

            try (InputStream is = urlConnection.getInputStream()) {
                BufferedReader reader = new BufferedReader(new InputStreamReader(is, StandardCharsets.ISO_8859_1));

                return new BlockedServers(reader.lines().collect(Collectors.toSet()));
            }
        } catch (IOException exception) {
            return null;
        }
    }
}
