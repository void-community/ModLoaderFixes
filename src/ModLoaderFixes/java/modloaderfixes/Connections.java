package modloaderfixes;

import net.minecraft.network.NetworkManager;

/**
 * Lets Minecraft 1.7's network tick leave a connection alone until it has a channel, as Minecraft 1.8 does. NetworkSystem's channel initializer adds each
 * new NetworkManager to the list the server thread ticks before channelActive gives it its channel, and the tick takes a manager without one for a closed
 * connection: it drops the manager from the list without a word, and the login that the network thread goes on to start is never answered or timed out.
 * It is defined in the loader of the class that ticks: Forge 1.7's NetworkSystem, whose manager it calls by the names FML gives it, or PaperSpigot 1.7.10's
 * ServerConnection, whose manager it calls by CraftBukkit's names and whose channel it reads through the getter the patch adds.
 */
public final class Connections {
    private Connections() {
    }

    /** Stands in for manager.isChannelOpen() in the tick: a manager that has no channel yet counts as open, so the tick keeps it. */
    public static boolean isOpenOrPending(NetworkManager manager) {
        return manager.channel() == null || manager.func_150724_d();
    }

    /** Stands in for manager.processReceivedPackets() in the tick, which reads the channel at once: a manager that has no channel yet waits for a later tick. */
    public static void processIfActive(NetworkManager manager) {
        if (manager.channel() != null)
            manager.func_74428_b();
    }

    /** Stands in for manager.isConnected() in PaperSpigot's tick: a manager that has no channel yet counts as open, decided on one read of the channel. */
    public static boolean isOpenOrPending(net.minecraft.server.v1_7_R4.NetworkManager manager) {
        return manager.modloaderfixes$channel() == null || manager.isConnected();
    }

    /** Stands in for manager.a(), which processes its packets, in PaperSpigot's tick: a manager that has no channel yet waits for a later tick. */
    public static void processIfActive(net.minecraft.server.v1_7_R4.NetworkManager manager) {
        if (manager.modloaderfixes$channel() != null)
            manager.a();
    }
}
