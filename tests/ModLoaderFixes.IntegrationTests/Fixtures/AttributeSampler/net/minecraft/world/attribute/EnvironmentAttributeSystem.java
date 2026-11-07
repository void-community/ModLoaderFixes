package net.minecraft.world.attribute;

import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;

/**
 * Samples a dimension's environment attributes as Minecraft 1.21.11 and later do: ValueSampler.getDimensionValue checks the value it cached for the tick and reads
 * it again to return it, while the server thread clears the cache at the start of each tick. main ticks on the server thread while world generation workers read
 * the moon phase as they spawn a chunk's mobs, and reports the reads that came back null, as the one that crashed getMoonBrightness did.
 */
public class EnvironmentAttributeSystem {
    private static final long RUN_MILLISECONDS = 3000;
    private static final int WORKERS = 3;

    /** The moon phase, which every attribute value here is. */
    public enum MoonPhase {
        FULL_MOON;

        public int index() {
            return ordinal();
        }
    }

    /** One attribute's sampler, with the cache of its value for the current tick. */
    public static final class ValueSampler<Value> {
        private final Value baseValue;
        private Value cachedTickValue;

        ValueSampler(Value baseValue) {
            this.baseValue = baseValue;
        }

        public void invalidateTickCache() {
            this.cachedTickValue = null;
        }

        public Value getDimensionValue() {
            if (this.cachedTickValue != null) {
                return this.cachedTickValue;
            } else {
                Value result = this.computeValueNotPositional();
                this.cachedTickValue = result;
                return result;
            }
        }

        private Value computeValueNotPositional() {
            return baseValue;
        }
    }

    public static void main(String[] arguments) throws InterruptedException {
        final ValueSampler<MoonPhase> moonPhase = new ValueSampler<MoonPhase>(MoonPhase.FULL_MOON);
        final AtomicBoolean running = new AtomicBoolean(true);
        final AtomicInteger nulls = new AtomicInteger();
        final AtomicReference<String> crash = new AtomicReference<String>();
        Thread[] workers = new Thread[WORKERS];

        for (int index = 0; index < WORKERS; index++) {
            workers[index] = new Thread(() -> {
                while (running.get()) {
                    try {
                        moonPhase.getDimensionValue().index();
                    } catch (NullPointerException exception) {
                        nulls.incrementAndGet();
                        crash.compareAndSet(null, exception.toString());
                    }
                }
            }, "Worker-Main-" + index);
            workers[index].start();
        }

        long end = System.currentTimeMillis() + RUN_MILLISECONDS;

        while (System.currentTimeMillis() < end)
            moonPhase.invalidateTickCache();

        running.set(false);

        for (Thread worker : workers)
            worker.join();

        if (nulls.get() > 0) {
            System.out.println("NULL moon phase " + nulls.get() + " times: " + crash.get());
            System.exit(1);
        }

        System.out.println("READ the moon phase every time");
    }
}
