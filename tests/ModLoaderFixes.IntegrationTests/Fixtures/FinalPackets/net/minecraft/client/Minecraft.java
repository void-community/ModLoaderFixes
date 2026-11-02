package net.minecraft.client;

import java.util.Queue;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.Executor;

/** The client, whose Render thread runs the tasks queued for it once a frame, as Minecraft's BlockableEventLoop does. */
public class Minecraft implements Executor {
    private final Queue<Runnable> tasks = new ConcurrentLinkedQueue<>();
    private volatile boolean running = true;

    public final Thread renderThread = new Thread(() -> {
        while (this.running) {
            try {
                Thread.sleep(50);
            } catch (InterruptedException exception) {
                return;
            }

            for (Runnable task = this.tasks.poll(); task != null; task = this.tasks.poll())
                task.run();
        }
    }, "Render thread");

    @Override
    public void execute(Runnable task) {
        this.tasks.add(task);
    }

    public void stop() throws InterruptedException {
        this.running = false;
        this.renderThread.join();
    }
}
