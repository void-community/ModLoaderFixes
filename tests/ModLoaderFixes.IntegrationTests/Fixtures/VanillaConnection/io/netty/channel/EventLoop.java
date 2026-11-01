package io.netty.channel;

import java.util.concurrent.Executor;

/** Netty's event loop, which runs a channel's tasks in order. */
public interface EventLoop extends Executor {
    boolean inEventLoop();
}
