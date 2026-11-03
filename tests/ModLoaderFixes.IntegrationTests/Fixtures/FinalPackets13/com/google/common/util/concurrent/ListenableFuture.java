package com.google.common.util.concurrent;

import java.util.concurrent.Future;

/** Guava's future, which Minecraft 1.13's client returns for each task it queues; only the type matters here. */
public interface ListenableFuture<V> extends Future<V> {
}
