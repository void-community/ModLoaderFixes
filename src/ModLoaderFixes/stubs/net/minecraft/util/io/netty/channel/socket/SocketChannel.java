package net.minecraft.util.io.netty.channel.socket;

import net.minecraft.util.io.netty.channel.Channel;
import net.minecraft.util.io.netty.channel.ChannelFuture;

/** The part of Netty's TCP channel that the helpers use, under CraftBukkit 1.7's package. */
public interface SocketChannel extends Channel {
    SocketChannelConfig config();

    ChannelFuture shutdownOutput();
}
