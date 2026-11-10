package modloaderfixes;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Iterator;
import java.util.List;

/**
 * Makes the children of an EventBus listener list, before EventBus 4.0.0, safe to add to while they are iterated, as 4.0.0 does. ListenerListInst keeps the
 * listener lists of its event's subclasses in a plain ArrayList, which addChild fills as a subclass's list is first built and forceRebuild iterates after a
 * listener registers, without a lock, so two mods that construct at once on the mod-loading workers can make the iteration throw
 * ConcurrentModificationException, and FML fails one of the mods. addChild creates the list here, synchronized, and forceRebuild iterates a copy taken under its
 * lock. It names no game type.
 */
public final class ListenerChildren {
    private ListenerChildren() {
    }

    /** Stands in for addChild's new ArrayList(): a list whose methods lock it, as EventBus 4.0.0's is. */
    public static <E> List<E> newChildren() {
        return Collections.synchronizedList(new ArrayList<E>(2));
    }

    /** Stands in for children.iterator() in forceRebuild: an iterator over a copy of the children, taken under the list's lock. */
    public static <E> Iterator<E> iterator(List<E> children) {
        synchronized (children) {
            return new ArrayList<E>(children).iterator();
        }
    }
}
