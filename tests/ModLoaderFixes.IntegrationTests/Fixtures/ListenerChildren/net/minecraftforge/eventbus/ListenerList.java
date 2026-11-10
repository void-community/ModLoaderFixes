package net.minecraftforge.eventbus;

import java.util.ArrayList;
import java.util.ConcurrentModificationException;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Keeps the listener lists of an event's subclasses as EventBus 3.0.3 does: ListenerListInst's addChild creates and fills a plain ArrayList, and forceRebuild
 * iterates it without a lock after a listener registers. main has two mod-loading workers build the lists of new subclasses of one event while two others
 * register listeners for that event, as mods that construct at once do, and counts the adds and iterations that threw, ConcurrentModificationException among them,
 * and the children that were lost.
 */
public class ListenerList {
    private static final int ROUNDS = 200;
    private static final int CHILDREN_PER_WORKER = 500;

    /** One event class's listener list on one bus, with the lists of its subclasses. */
    public static class ListenerListInst {
        private boolean rebuild = true;
        private List<ListenerListInst> children;

        ListenerListInst() {
        }

        ListenerListInst(ListenerListInst parent) {
            parent.addChild(this);
        }

        protected void forceRebuild() {
            this.rebuild = true;

            if (this.children != null) {
                for (ListenerListInst child : this.children)
                    child.forceRebuild();
            }
        }

        private void addChild(ListenerListInst child) {
            if (this.children == null)
                this.children = new ArrayList<ListenerListInst>();

            this.children.add(child);
        }

        int children() {
            return children == null ? 0 : children.size();
        }


        List<ListenerListInst> list() {
            return children;
        }
    }

    public static void main(String[] arguments) throws InterruptedException {
        final AtomicInteger modifications = new AtomicInteger();
        final AtomicInteger failures = new AtomicInteger();
        final AtomicInteger lost = new AtomicInteger();

        for (int round = 0; round < ROUNDS; round++) {
            final ListenerListInst parent = new ListenerListInst();
            final CountDownLatch start = new CountDownLatch(1);
            final AtomicInteger building = new AtomicInteger(2);
            List<Thread> workers = new ArrayList<Thread>();

            for (int index = 0; index < 2; index++) {
                workers.add(new Thread(() -> {
                    await(start);

                    try {
                        for (int child = 0; child < CHILDREN_PER_WORKER; child++)
                            new ListenerListInst(parent);
                    } catch (RuntimeException exception) {
                        failures.incrementAndGet();
                    } finally {
                        building.decrementAndGet();
                    }
                }, "modloading-worker-" + index));
                workers.add(new Thread(() -> {
                    await(start);

                    while (building.get() > 0) {
                        try {
                            parent.forceRebuild();
                        } catch (ConcurrentModificationException exception) {
                            modifications.incrementAndGet();
                        } catch (RuntimeException exception) {
                            failures.incrementAndGet();
                        }
                    }
                }, "modloading-worker-" + (index + 2)));
            }

            for (Thread worker : workers)
                worker.start();

            start.countDown();

            for (Thread worker : workers)
                worker.join();

            lost.addAndGet(2 * CHILDREN_PER_WORKER - parent.children());
        }

        if (modifications.get() > 0 || failures.get() > 0 || lost.get() > 0) {
            System.out.println("FAILED " + modifications.get() + " rebuilds with ConcurrentModificationException and " + failures.get() + " others, LOST " + lost.get() + " children");
            System.exit(1);
        }

        ListenerListInst parent = new ListenerListInst();

        new ListenerListInst(parent);
        System.out.println("REBUILT every list while its children grew, kept in " + parent.list().getClass().getName());
    }

    private static void await(CountDownLatch latch) {
        try {
            latch.await();
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }
}
