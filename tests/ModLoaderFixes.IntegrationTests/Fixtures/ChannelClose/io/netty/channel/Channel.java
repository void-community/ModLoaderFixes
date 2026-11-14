package io.netty.channel;

/** The part of Netty's channel that the connection uses. */
public interface Channel {
    boolean isOpen();

    ChannelFuture close();
}
