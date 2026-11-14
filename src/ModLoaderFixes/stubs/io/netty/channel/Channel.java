package io.netty.channel;

/** The part of Netty's channel that the helpers use. */
public interface Channel {
    ChannelFuture close();

    EventLoop eventLoop();

    ChannelPipeline pipeline();
}
