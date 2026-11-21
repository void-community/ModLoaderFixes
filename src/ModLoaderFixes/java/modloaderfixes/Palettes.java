package modloaderfixes;

import java.util.function.IntFunction;

/**
 * Reads a block from a Minecraft 1.18 and later PalettedContainer again when its palette doesn't have the block's entry yet. A thread that sets a block of a
 * new kind adds the kind to the section's palette and then stores its index, on plain fields and without a lock, and the read takes none either. On ARM another
 * thread can see the index before the entry, so the light engine, which reads blocks that world generation still sets, gets a MissingPaletteEntryException or a
 * null block, its task ends, and the server never finishes the chunk. The entry is on its way whenever the index is there, so the read is made again until it
 * comes; a read that still finds nothing ends as it did, with the first exception or with null. It names no game type.
 */
public final class Palettes {
    // The entry arrives within the time the writer's processor takes to finish two stores; a container whose data really lacks it waits a hundred yields.
    private static final int ATTEMPTS = 100;

    private Palettes() {
    }

    /** What the container's own read gives for the index, read again while it throws or gives null. */
    public static Object read(IntFunction<Object> read, int index) {
        RuntimeException missing = null;

        try {
            Object value = read.apply(index);

            if (value != null)
                return value;
        } catch (RuntimeException exception) {
            missing = exception;
        }

        return readAgain(read, index, missing);
    }

    private static Object readAgain(IntFunction<Object> read, int index, RuntimeException first) {
        RuntimeException missing = first;

        for (int attempt = 0; attempt < ATTEMPTS; attempt++) {
            Thread.yield();

            try {
                Object value = read.apply(index);

                if (value != null)
                    return value;
            } catch (RuntimeException exception) {
                if (missing == null)
                    missing = exception;
            }
        }

        if (missing != null)
            throw missing;

        return null;
    }
}
