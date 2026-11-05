package modloaderfixes;

import java.util.HashSet;

/**
 * Gives a Minecraft 1.16 to 1.18.2 reload the set of the listeners still preparing as one that several threads can remove from at once. Each listener's
 * preparation, when it reaches the reload's barrier, hands the barrier's game executor a task that removes the listener from the set and completes the barrier
 * once the set is empty. A dedicated server's startup reload passes Runnable::run as that executor, so the task runs on whichever worker thread reached the
 * barrier, and two workers removing from a plain HashSet at once can lose a removal: the set never empties, the barrier never completes and the server waits
 * for its data packs forever. The set's constructor takes the listeners before the reload starts, and from then on each method locks the set. It names no game type.
 */
public final class ReloadListeners {
    private ReloadListeners() {
    }

    /** Stands in for the reload's Sets.newHashSet(listeners). */
    public static <E> HashSet<E> newHashSet(Iterable<? extends E> listeners) {
        LockedSet<E> set = new LockedSet<E>();

        for (E listener : listeners)
            set.add(listener);

        return set;
    }

    /** A HashSet whose methods that the reload and Guava call lock the set, so a removal on one thread and a removal or emptiness check on another don't race. */
    private static final class LockedSet<E> extends HashSet<E> {
        private static final long serialVersionUID = 1L;

        @Override
        public synchronized boolean add(E element) {
            return super.add(element);
        }

        @Override
        public synchronized boolean remove(Object element) {
            return super.remove(element);
        }

        @Override
        public synchronized boolean contains(Object element) {
            return super.contains(element);
        }

        @Override
        public synchronized boolean isEmpty() {
            return super.isEmpty();
        }

        @Override
        public synchronized int size() {
            return super.size();
        }

        @Override
        public synchronized void clear() {
            super.clear();
        }
    }
}
