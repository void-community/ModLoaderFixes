package io.netty.channel;

/** The part of Netty's channel that Minecraft 1.7 and the agent's helper use. */
public interface Channel {
    boolean isOpen();

    ChannelPipeline pipeline();

    EventLoop eventLoop();
}
