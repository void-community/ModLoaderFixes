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

/**
 * Reloads as Minecraft 1.16 to 1.18.2 do, by Mojang's names, which Forge from 1.17 runs: the constructor keeps the listeners still preparing in a set from
 * Sets.newHashSet, and each listener's barrier hands the game executor a task that removes the listener and completes the barrier once the set is empty. main
 * reloads again and again as a dedicated server's startup does, with Runnable::run as the game executor, so each worker removes the listeners it prepared from the
 * set while the others remove theirs; a reload whose set loses a removal never completes, as the server never finishes loading its data packs. A reload has many
 * more listeners than a vanilla server, as a modded one does, so the workers race in every reload and a lost removal comes within a few.
 */
public class SimpleReloadInstance {
    private static final int LISTENERS = 256;
    private static final int RELOADS = 1000;
    private static final int WORKERS = 4;

    final Set<Object> preparingListeners;
    final CompletableFuture<Void> allPreparations = new CompletableFuture<Void>();
    final CompletableFuture<Void> allDone;

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
                    gameExecutor.execute(() -> {
                        preparingListeners.remove(listener);

                        if (preparingListeners.isEmpty())
                            allPreparations.complete(null);
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

    public static void main(String[] arguments) throws Exception {
        ExecutorService workers = Executors.newFixedThreadPool(WORKERS);

        try {
            for (int reload = 1; reload <= RELOADS; reload++) {
                List<Object> listeners = new ArrayList<Object>();

                for (int index = 0; index < LISTENERS; index++)
                    listeners.add(new Object());

                SimpleReloadInstance instance = new SimpleReloadInstance(workers, Runnable::run, listeners);

                try {
                    instance.allDone.get(5, TimeUnit.SECONDS);
                } catch (TimeoutException exception) {
                    System.out.println("HUNG at reload " + reload + " with " + instance.preparingListeners.size() + " listeners still preparing");
                    System.exit(1);
                }

                if (reload == 1)
                    System.out.println("SET " + instance.preparingListeners.getClass().getName());
            }

            System.out.println("RELOADED " + RELOADS + " times");
        } finally {
            workers.shutdownNow();
        }
    }
}
