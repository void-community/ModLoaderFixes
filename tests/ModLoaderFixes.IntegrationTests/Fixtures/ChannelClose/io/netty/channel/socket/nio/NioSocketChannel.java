package io.netty.channel.socket.nio;

import java.io.IOException;
import java.net.SocketException;

import io.netty.channel.ChannelFuture;
import io.netty.channel.socket.SocketChannel;
import io.netty.channel.socket.SocketChannelConfig;

/**
 * Netty's NIO channel, as far as closing it goes: close closes the JDK's channel, which is registered with the event loop's selector, shutdownOutput shuts the
 * socket's output down, and the options read the socket's.
 */
public final class NioSocketChannel implements SocketChannel {
    private static final ChannelFuture DONE = new ChannelFuture() {
    };

    private final java.nio.channels.SocketChannel javaChannel;

    public NioSocketChannel(java.nio.channels.SocketChannel javaChannel) {
        this.javaChannel = javaChannel;
    }

    @Override
    public boolean isOpen() {
        return javaChannel.isOpen();
    }

    @Override
    public ChannelFuture close() {
        try {
            javaChannel.close();
        } catch (IOException exception) {
            throw new IllegalStateException(exception);
        }

        return DONE;
    }

    @Override
    public ChannelFuture shutdownOutput() {
        try {
            javaChannel.socket().shutdownOutput();
        } catch (IOException failed) {
            // Netty fails the future instead.
        }

        return DONE;
    }

    @Override
    public SocketChannelConfig config() {
        return new SocketChannelConfig() {
            @Override
            public boolean isTcpNoDelay() {
                try {
                    return javaChannel.socket().getTcpNoDelay();
                } catch (SocketException exception) {
                    throw new IllegalStateException(exception);
                }
            }
        };
    }
}
