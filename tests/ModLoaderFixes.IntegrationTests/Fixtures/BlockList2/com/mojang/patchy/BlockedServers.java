package com.mojang.patchy;

import java.util.Collection;
import java.util.HashSet;
import java.util.Set;
import java.util.function.Predicate;

/** patchy 2's list of blocked servers, which the supplier builds from the hashes it reads. */
public class BlockedServers implements Predicate<String> {
    private final Set<String> blockedServers;

    public BlockedServers(Collection<String> blockedServers) {
        this.blockedServers = new HashSet<String>(blockedServers);
    }

    @Override
    public boolean test(String server) {
        return blockedServers.contains(server);
    }
}
