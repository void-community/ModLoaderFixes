package com.google.common.collect;

import java.util.HashSet;

/** Guava's Sets, whose newHashSet the reload creates its set of listeners with; it fills a plain HashSet as Guava's does. */
public final class Sets {
    private Sets() {
    }

    public static <E> HashSet<E> newHashSet(Iterable<? extends E> elements) {
        HashSet<E> set = new HashSet<E>();

        for (E element : elements)
            set.add(element);

        return set;
    }
}
