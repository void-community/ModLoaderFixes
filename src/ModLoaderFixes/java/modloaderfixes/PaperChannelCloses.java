package modloaderfixes;

import net.minecraft.util.io.netty.channel.Channel;
import net.minecraft.util.io.netty.channel.ChannelFuture;
import net.minecraft.util.io.netty.channel.socket.SocketChannel;

/**
 * Sends what a PaperSpigot 1.7.10 connection still has to send before it closes, as ChannelCloses does for Forge and vanilla: PaperSpigot's NetworkManager.close
 * closes its channel here, and a socket with Nagle's algorithm on has its output shut down first. CraftBukkit 1.7 relocates Netty, so this names the channel
 * by that package.
 */
public final class PaperChannelCloses {
    private PaperChannelCloses() {
    }

    /** Stands in for channel.close() in NetworkManager.close. */
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
