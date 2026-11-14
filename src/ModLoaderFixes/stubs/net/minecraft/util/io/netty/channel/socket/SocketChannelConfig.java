package net.minecraft.util.io.netty.channel.socket;

/** The part of a TCP channel's options that the helpers use, under CraftBukkit 1.7's package. */
public interface SocketChannelConfig {
    boolean isTcpNoDelay();
}
