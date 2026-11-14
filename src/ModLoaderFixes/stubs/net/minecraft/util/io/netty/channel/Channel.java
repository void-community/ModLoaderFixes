package net.minecraft.util.io.netty.channel;

/** The part of Netty's channel that the helpers use, under the package that CraftBukkit 1.7 relocates Netty to. */
public interface Channel {
    ChannelFuture close();
}
