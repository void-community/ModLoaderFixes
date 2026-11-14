package io.netty.channel.socket;

import io.netty.channel.Channel;
import io.netty.channel.ChannelFuture;

/** The part of Netty's TCP channel that closing a connection gracefully takes. */
public interface SocketChannel extends Channel {
    SocketChannelConfig config();

    ChannelFuture shutdownOutput();
}
