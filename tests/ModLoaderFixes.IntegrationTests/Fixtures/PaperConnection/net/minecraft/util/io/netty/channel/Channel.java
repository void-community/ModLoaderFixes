package net.minecraft.util.io.netty.channel;

/** The part of Netty's channel, which PaperSpigot relocates, that its NetworkManager asks before it ticks a connection. */
public interface Channel {
    boolean isOpen();
}
