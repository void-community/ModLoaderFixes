package it.unimi.dsi.fastutil.ints;

import java.util.Arrays;

/**
 * fastutil 8.5.12's list of ints, as far as NeoForge's handshake uses it: add, contains, isEmpty and size touch the array and its size without a lock, and the
 * removeIf of fastutil's own predicate is final and hands the predicate to the removeIf of Java's, which removes each match by shifting the array down, as
 * IntCollection's default does through the list's iterator.
 */
public class IntArrayList {
    private int[] a = new int[10];
    private int size;

    public boolean add(int k) {
        if (size == a.length)
            a = Arrays.copyOf(a, a.length * 2);

        a[size++] = k;
        return true;
    }

    public boolean contains(int k) {
        for (int index = 0; index < size; index++) {
            if (a[index] == k)
                return true;
        }

        return false;
    }

    public boolean isEmpty() {
        return size == 0;
    }

    public final boolean removeIf(IntPredicate filter) {
        return removeIf((java.util.function.IntPredicate) filter);
    }

    public boolean removeIf(java.util.function.IntPredicate filter) {
        boolean removed = false;
        int index = 0;

        while (index < size) {
            if (filter.test(a[index])) {
                removeInt(index);
                removed = true;
            } else {
                index++;
            }
        }

        return removed;
    }

    public int removeInt(int index) {
        int old = a[index];

        size--;

        if (index != size)
            System.arraycopy(a, index + 1, a, index, size - index);

        return old;
    }

    public int size() {
        return size;
    }
}
