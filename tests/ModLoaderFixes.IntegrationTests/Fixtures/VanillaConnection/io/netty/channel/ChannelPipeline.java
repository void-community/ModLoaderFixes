package io.netty.channel;

/** The part of Netty's pipeline that Minecraft 1.7 and the agent's helper use. */
public interface ChannelPipeline {
    ChannelPipeline addLast(String name, ChannelHandler handler);

    Channel channel();
}
