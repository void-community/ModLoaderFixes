package net.minecraft.client;

import java.util.Queue;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.FutureTask;

import com.google.common.util.concurrent.ListenableFuture;

/**
 * The client as Minecraft 1.13 runs it, which is no executor: each frame runs the tasks queued for it and then a tick, and leaving the world drops the tasks still
 * queued, as loadWorld(null) does. This one's ticks are slow, as a busy client's are, and the first one tells the server to go ahead.
 */
public class Minecraft {
    public final CountDownLatch ticking = new CountDownLatch(1);

    private final Queue<FutureTask<?>> scheduledTasks = new ConcurrentLinkedQueue<FutureTask<?>>();

    /** addScheduledTask, which queues a task for the client thread and returns its future. */
    public ListenableFuture<Object> func_152344_a(Runnable task) {
        Scheduled scheduled = new Scheduled(task);

        this.scheduledTasks.add(scheduled);

        return scheduled;
    }

    /** One frame: the queued tasks, then a tick that takes a while and then runs the given check. */
    public void frame(Runnable tick) throws InterruptedException {
        for (FutureTask<?> task = this.scheduledTasks.poll(); task != null; task = this.scheduledTasks.poll())
            task.run();

        this.ticking.countDown();
        Thread.sleep(100);
        tick.run();
    }

    /** loadWorld(null), which leaves the world and drops the tasks still queued. */
    public void func_71403_a() {
        this.scheduledTasks.clear();
    }

    private static final class Scheduled extends FutureTask<Object> implements ListenableFuture<Object> {
        Scheduled(Runnable task) {
            super(task, null);
        }
    }
}
