package modloaderfixes;

import io.netty.channel.Channel;
import io.netty.channel.ChannelFuture;
import io.netty.channel.socket.SocketChannel;

/**
 * Sends what a Minecraft 1.7.2 to 1.8 connection still has to send before it closes. Those releases leave Nagle's algorithm on, so the kernel holds a small
 * packet back while an earlier one is unacknowledged, and a kick writes its Disconnect packet, closes the channel as soon as the write completes and stops
 * reading. Java before 11 closes the socket at once, and when the player's next packets are unread by then, Linux resets the connection and throws the held
 * packet away: the player is kicked without the reason. NetworkManager.closeChannel closes its channel here instead: a socket with Nagle's algorithm on has its
 * output shut down first, which sends what is held together with the end of the stream, as Java from 11 does itself when it closes such a channel. A socket
 * with the algorithm off, as Minecraft from 1.8.1 sets it, holds nothing back and is closed as before, and so is a channel in memory. It names no class of
 * Minecraft, whose names change in every release.
 */
public final class ChannelCloses {
    private ChannelCloses() {
    }

    /** Stands in for channel.close() in NetworkManager.closeChannel. */
    public static ChannelFuture close(Channel channel) {
        if (channel instanceof SocketChannel) {
            SocketChannel socket = (SocketChannel) channel;

            try {
                // Netty fails the future it returns when the output can't be shut down, as for a socket that never connected.
                if (!socket.config().isTcpNoDelay())
                    socket.shutdownOutput();
            } catch (RuntimeException closed) {
                // The socket closed in the meantime, so its options can't be read and it has nothing left to send.
            }
        }

        return channel.close();
    }
}
