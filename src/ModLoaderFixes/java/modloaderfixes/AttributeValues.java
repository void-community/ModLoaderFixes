package modloaderfixes;

import java.util.function.Supplier;

/**
 * Gives a Minecraft 1.21.11 and later dimension's environment attribute its value of the tick from one read of the cache. ValueSampler.getDimensionValue
 * checks cachedTickValue and then reads it again to return it, while the server thread clears it at the start of each tick, so a world generation worker that
 * reads between the two gets null, and the chunk's generation fails with a NullPointerException in getMoonBrightness. The patched method reads the field once and
 * hands the value here, with the method the patch adds that computes the value and caches it. It names no game type.
 */
public final class AttributeValues {
    private AttributeValues() {
    }

    /** The cached value, or the value computed and cached when there is none, as getDimensionValue gives. */
    public static Object cachedOrComputed(Object cached, Supplier<Object> compute) {
        return cached != null ? cached : compute.get();
    }
}
