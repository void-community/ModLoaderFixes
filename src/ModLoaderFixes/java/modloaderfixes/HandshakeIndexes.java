package modloaderfixes;

import java.util.function.IntPredicate;

import it.unimi.dsi.fastutil.ints.IntArrayList;

/**
 * NeoForge 1.20.2's list of the handshake messages that wait for the client's answer, as one that the server thread and the network thread can change at once.
 * HandshakeHandler keeps their indexes in a plain IntArrayList: the server thread adds each message it sends in tickServer and ends the handshake once the list is
 * empty, while the network thread removes each one the client answers with removeIf. A removal that runs during an add can lose the add, so the handshake ends
 * without the last answer and the player is dropped with "Badly compressed packet". Each method the handshake calls locks the list; fastutil's removeIf of its own
 * predicate is final and ends in the removeIf of Java's, which this locks. It is defined in the handler's loader.
 */
public final class HandshakeIndexes extends IntArrayList {
    private static final long serialVersionUID = 1L;

    @Override
    public synchronized boolean add(int index) {
        return super.add(index);
    }

    @Override
    public synchronized boolean contains(int index) {
        return super.contains(index);
    }

    @Override
    public synchronized boolean isEmpty() {
        return super.isEmpty();
    }

    @Override
    public synchronized boolean removeIf(IntPredicate filter) {
        return super.removeIf(filter);
    }

    @Override
    public synchronized int size() {
        return super.size();
    }
}
