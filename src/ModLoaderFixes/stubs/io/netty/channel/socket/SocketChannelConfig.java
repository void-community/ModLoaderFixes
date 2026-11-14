package io.netty.channel.socket;

/** The part of a TCP channel's options that the helpers use. */
public interface SocketChannelConfig {
    boolean isTcpNoDelay();
}
