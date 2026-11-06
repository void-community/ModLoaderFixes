package it.unimi.dsi.fastutil.ints;

import java.util.function.IntPredicate;

/** fastutil's list of ints, as Minecraft 1.20.2 to 1.20.4 ship it in 8.5.12: the methods NeoForge's handshake calls on its sentMessages, and the one removeIf ends in. */
public class IntArrayList {
    public IntArrayList() {
    }

    public boolean add(int k) {
        throw new UnsupportedOperationException();
    }

    public boolean contains(int k) {
        throw new UnsupportedOperationException();
    }

    public boolean isEmpty() {
        throw new UnsupportedOperationException();
    }

    /** The removal that the final removeIf of fastutil's own predicate hands its predicate to, through IntCollection's default. */
    public boolean removeIf(IntPredicate filter) {
        throw new UnsupportedOperationException();
    }

    public int size() {
        throw new UnsupportedOperationException();
    }
}
