package connecting;

import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

/**
 * The manager's queue, whose first two checks for queued packets meet: a flush that finds packets waits up to a second for a second flush to find them too, as
 * two flushes of a client may, so that both go on to poll.
 */
public final class MeetingQueue<E> extends ConcurrentLinkedQueue<E> {
    private final CountDownLatch flushes = new CountDownLatch(2);

    @Override
    public boolean isEmpty() {
        boolean empty = super.isEmpty();

        if (!empty && flushes.getCount() > 0) {
            flushes.countDown();

            try {
                flushes.await(1, TimeUnit.SECONDS);
            } catch (InterruptedException exception) {
                Thread.currentThread().interrupt();
            }
        }

        return empty;
    }
}
