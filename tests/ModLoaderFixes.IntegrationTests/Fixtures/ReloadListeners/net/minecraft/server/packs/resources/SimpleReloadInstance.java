package net.minecraft.server.packs.resources;

import com.google.common.collect.Sets;

import java.util.ArrayList;
import java.util.List;
import java.util.Set;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executor;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Reloads as Minecraft 1.16 to 1.18.2 do, by Mojang's names, which Forge from 1.17 runs: the constructor keeps the listeners still preparing in a set from
 * Sets.newHashSet, and each listener's barrier hands the game executor a task that removes the listener and completes the barrier once the set is empty. main
 * reloads again and again as a dedicated server's startup does, with Runnable::run as the game executor, so each worker removes the listeners it prepared from the
 * set while the others remove theirs; a reload whose set loses a removal never completes, as the server never finishes loading its data packs. A reload has many
 * more listeners than a vanilla server, as a modded one does, so the workers race in every reload, and one of the 20000 reloads loses a removal.
 *
 * A reload that doesn't complete in time is the set's doing only when every listener reached the barrier and every removal ran, and the set still holds a
 * listener: that is HUNG, and ends the run. On 32-bit ARM, Java 8 now and then never runs a stage of a future, with every worker idle, whatever the set is; such a
 * reload is STALLED, which the set can't cause, and the run goes on with the next.
 */
public class SimpleReloadInstance {
    private static final int LISTENERS = 256;
    private static final int RELOADS = 20000;
    private static final int STALLS = 10;
    private static final int WORKERS = 4;

    final Set<Object> preparingListeners;
    final CompletableFuture<Void> allPreparations = new CompletableFuture<Void>();
    final CompletableFuture<Void> allDone;
    // How many listeners reached the barrier, and how many of their removals ran to their end.
    final AtomicInteger reached = new AtomicInteger();
    final AtomicInteger removals = new AtomicInteger();

    /** The barrier each listener's preparation waits at until every listener has prepared, as PreparableReloadListener.PreparationBarrier is. */
    interface PreparationBarrier {
        <T> CompletableFuture<T> wait(T value);
    }

    SimpleReloadInstance(Executor backgroundExecutor, final Executor gameExecutor, List<Object> listeners) {
        this.preparingListeners = Sets.newHashSet(listeners);
        List<CompletableFuture<?>> done = new ArrayList<CompletableFuture<?>>();
        CompletableFuture<?> previous = CompletableFuture.completedFuture(null);

        for (final Object listener : listeners) {
            final CompletableFuture<?> before = previous;
            PreparationBarrier barrier = new PreparationBarrier() {
                @Override
                public <T> CompletableFuture<T> wait(final T value) {
                    reached.incrementAndGet();
                    gameExecutor.execute(() -> {
                        preparingListeners.remove(listener);

                        if (preparingListeners.isEmpty())
                            allPreparations.complete(null);

                        removals.incrementAndGet();
                    });

                    return allPreparations.thenCombine(before, (preparations, previousDone) -> value);
                }
            };
            CompletableFuture<Object> future = CompletableFuture.supplyAsync(() -> listener, backgroundExecutor)
                .thenCompose(barrier::wait)
                .thenApplyAsync(value -> value, gameExecutor);

            done.add(future);
            previous = future;
        }

        this.allDone = CompletableFuture.allOf(done.toArray(new CompletableFuture<?>[0]));
    }

    /** Says what every thread does when a reload hangs: a worker that stands in a listener's task, or none, which tells a stalled worker from a lost removal. */
    private static void printThreads() {
        for (java.util.Map.Entry<Thread, StackTraceElement[]> entry : Thread.getAllStackTraces().entrySet()) {
            System.out.println("THREAD " + entry.getKey().getName() + " " + entry.getKey().getState());

            for (StackTraceElement element : entry.getValue())
                System.out.println("    at " + element);
        }
    }

    public static void main(String[] arguments) throws Exception {
        ExecutorService workers = Executors.newFixedThreadPool(WORKERS);

        int stalled = 0;

        try {
            for (int reload = 1; reload <= RELOADS; reload++) {
                List<Object> listeners = new ArrayList<Object>();

                for (int index = 0; index < LISTENERS; index++)
                    listeners.add(new Object());

                SimpleReloadInstance instance = new SimpleReloadInstance(workers, Runnable::run, listeners);

                try {
                    instance.allDone.get(5, TimeUnit.SECONDS);
                } catch (TimeoutException exception) {
                    int preparing = instance.preparingListeners.size();
                    int reached = instance.reached.get();
                    int removals = instance.removals.get();

                    if (reached == LISTENERS && removals == LISTENERS && preparing != 0) {
                        System.out.println("HUNG at reload " + reload + " with " + preparing + " listeners still preparing: every listener's removal ran, and the set kept them");
                        printThreads();
                        System.exit(1);
                    }

                    System.out.println("STALLED at reload " + reload + ": " + reached + " of " + LISTENERS + " listeners reached the barrier, " + removals
                        + " removals ran and the set holds " + preparing + ", so a stage of a future never ran or never ended; the set lost nothing");
                    printThreads();

                    if (++stalled > STALLS) {
                        System.out.println("STALLED more than " + STALLS + " times");
                        System.exit(1);
                    }
                }

                if (reload == 1)
                    System.out.println("SET " + instance.preparingListeners.getClass().getName());
            }

            System.out.println("RELOADED " + RELOADS + " times, " + stalled + " of them stalled without the set losing a listener");
        } finally {
            workers.shutdownNow();
        }
    }
}
