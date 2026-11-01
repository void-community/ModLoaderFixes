package io.netty.channel;

/** Netty's channel initializer, which sets a new channel's pipeline up when the channel registers, before channelActive. */
public abstract class ChannelInitializer implements ChannelHandler {
    protected abstract void initChannel(Channel channel) throws Exception;
}
