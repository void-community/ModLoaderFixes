package io.netty.channel.socket;

import io.netty.channel.Channel;
import io.netty.channel.ChannelFuture;

/** The part of Netty's TCP channel that the helpers use. */
public interface SocketChannel extends Channel {
    SocketChannelConfig config();

    ChannelFuture shutdownOutput();
}
