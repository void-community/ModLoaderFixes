package io.netty.channel;

/** The part of Netty's pipeline that the helpers use. */
public interface ChannelPipeline {
    ChannelPipeline addLast(String name, ChannelHandler handler);

    Channel channel();
}
