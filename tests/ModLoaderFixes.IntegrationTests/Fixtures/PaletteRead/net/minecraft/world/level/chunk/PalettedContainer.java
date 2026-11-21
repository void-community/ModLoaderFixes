package net.minecraft.world.level.chunk;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

/**
 * Keeps the blocks of a chunk section as Minecraft from 1.18 does: each block is an index in a storage, and a palette says which kind of block an index
 * stands for. get(int) takes no lock and is Mojang's code. A writer that sets a block of a new kind adds the kind to the palette and then stores its index,
 * and on ARM a reader can see the index while the palette's new size, or its new value, is still on its way; the light engine reads the blocks of chunks
 * that generation still writes to. main has a generation worker set such a block while the light engine's thread reads it, once with the size late and once
 * with the value late, and reports the reads that threw or came back null, as the one that stopped a server's spawn area did.
 */
public class PalettedContainer<T> {
    private static final int BLOCKS = 4096;
    private static final long WAIT_SECONDS = 60;

    /** The exception of Mojang's palettes for an index they don't have. */
    public static final class MissingPaletteEntryException extends RuntimeException {
        public MissingPaletteEntryException(int index) {
            super("Missing Palette entry for index " + index + ".");
        }
    }

    /** What stands for the kinds of blocks. */
    public interface Palette<T> {
        int idFor(T value);

        T valueFor(int id);
    }

    /** The indexes of the section's blocks. */
    public interface BitStorage {
        int get(int index);

        void set(int index, int value);
    }

    /** The storage and the palette that go together. */
    static final class Data<T> {
        final BitStorage storage;
        final Palette<T> palette;

        Data(BitStorage storage, Palette<T> palette) {
            this.storage = storage;
            this.palette = palette;
        }
    }

    /** A storage whose every index is visible to every thread as soon as it is set. */
    static final class SimpleBitStorage implements BitStorage {
        private final int[] indexes = new int[BLOCKS];

        @Override
        public synchronized int get(int index) {
            return this.indexes[index];
        }

        @Override
        public synchronized void set(int index, int value) {
            this.indexes[index] = value;
        }
    }

    /**
     * Mojang's LinearPalette as a reader on ARM can see it. idFor gives the index of a new kind while one of its two stores is still on its way: the new
     * size, without which valueFor throws, or the new value, without which valueFor returns null. A look that misses the entry waits until the writer
     * delivered it, so that the reader's next look finds the entry whatever the speed of either thread.
     */
    static final class LinearPalette<T> implements Palette<T> {
        private final T[] values;
        private final boolean sizeIsLate;
        private final CountDownLatch missed = new CountDownLatch(1);
        private final CountDownLatch delivered = new CountDownLatch(1);
        private volatile int size;
        private volatile T late;

        @SuppressWarnings("unchecked")
        LinearPalette(T first, boolean sizeIsLate) {
            this.values = (T[]) new Object[16];
            this.values[0] = first;
            this.size = 1;
            this.sizeIsLate = sizeIsLate;
        }

        @Override
        public synchronized int idFor(T value) {
            for (int id = 0; id < this.size; id++)
                if (this.values[id] == value)
                    return id;

            int id = this.size;

            this.late = value;

            if (!this.sizeIsLate)
                this.size = id + 1;

            return id;
        }

        @Override
        public T valueFor(int id) {
            T value;
            boolean has;

            synchronized (this) {
                has = id >= 0 && id < this.size;
                value = has ? this.values[id] : null;
            }

            if (has && value != null)
                return value;

            this.missed.countDown();
            await(this.delivered, "the generation worker to deliver the palette's entry");

            if (has)
                return null;

            throw new MissingPaletteEntryException(id);
        }

        /** Waits for a look that misses the entry, then delivers the store that was on its way. */
        void deliver() {
            await(this.missed, "the light engine to look the entry up");

            synchronized (this) {
                int id = this.sizeIsLate ? this.size : this.size - 1;

                this.values[id] = this.late;
                this.size = id + 1;
            }

            this.delivered.countDown();
        }
    }

    private volatile Data<T> data;

    PalettedContainer(T first, boolean sizeIsLate) {
        this.data = new Data<T>(new SimpleBitStorage(), new LinearPalette<T>(first, sizeIsLate));
    }

    public void set(int index, T value) {
        Data<T> data = this.data;
        int id = data.palette.idFor(value);

        data.storage.set(index, id);
    }

    protected T get(int index) {
        Data<T> data = this.data;

        return data.palette.valueFor(data.storage.get(index));
    }

    public static void main(String[] arguments) throws InterruptedException {
        boolean failed = false;

        for (boolean sizeIsLate : new boolean[] { true, false })
            failed |= !read(sizeIsLate);

        if (failed)
            System.exit(1);

        System.out.println("READ every block");
    }

    // A generation worker sets a block of a new kind, and the light engine reads that block once its index is in the storage and before its entry is in
    // the palette.
    private static boolean read(boolean sizeIsLate) throws InterruptedException {
        final PalettedContainer<String> container = new PalettedContainer<String>("minecraft:air", sizeIsLate);
        final LinearPalette<String> palette = (LinearPalette<String>) container.data.palette;
        final CountDownLatch stored = new CountDownLatch(1);
        final AtomicReference<String> result = new AtomicReference<String>();
        String entry = sizeIsLate ? "size" : "value";

        Thread worker = new Thread(() -> {
            container.set(7, "minecraft:stone");
            stored.countDown();
            palette.deliver();
        }, "Worker-Main-1");

        Thread light = new Thread(() -> {
            try {
                await(stored, "the generation worker to store the block's index");

                String block = container.get(7);

                result.set(block == null ? "NULL block while the palette's " + entry + " was on its way" : block.equals("minecraft:stone") ? null : "WRONG block " + block);
            } catch (MissingPaletteEntryException exception) {
                result.set("MISSED the block while the palette's " + entry + " was on its way: " + exception);
            } catch (RuntimeException exception) {
                result.set("FAILED: " + exception);
            }
        }, "light");

        worker.start();
        light.start();
        light.join();
        worker.join();

        if (result.get() == null)
            return true;

        System.out.println(result.get());

        return false;
    }

    private static void await(CountDownLatch latch, String awaited) {
        try {
            if (!latch.await(WAIT_SECONDS, TimeUnit.SECONDS))
                throw new IllegalStateException("Waited " + WAIT_SECONDS + " seconds for " + awaited + ".");
        } catch (InterruptedException exception) {
            throw new IllegalStateException(exception);
        }
    }
}
