package io.netty.channel.socket;

/** The part of a TCP channel's options that says whether Nagle's algorithm is off. */
public interface SocketChannelConfig {
    boolean isTcpNoDelay();
}
