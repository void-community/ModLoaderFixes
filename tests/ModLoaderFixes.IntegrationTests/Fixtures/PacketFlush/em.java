import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Queue;
import java.util.concurrent.locks.ReentrantReadWriteLock;

import connecting.Connection;
import connecting.MeetingQueue;
import io.netty.channel.SimpleChannelInboundHandler;
import io.netty.util.concurrent.GenericFutureListener;

/**
 * Vanilla 1.9.3's NetworkManager, under the names 1.9.3 gives it: a send queues the packet under the write lock while there is no channel, and otherwise flushes
 * the queue and dispatches the packet. The flush takes the queued packets off under the read lock, which two threads can hold at once.
 */
public class em extends SimpleChannelInboundHandler implements Connection {
    private final Queue<a> i = new MeetingQueue<a>();
    private final ReentrantReadWriteLock j = new ReentrantReadWriteLock();
    private final List<String> written = Collections.synchronizedList(new ArrayList<String>());
    private Object k;

    @Override
    public void open() {
        k = new Object();
    }

    @Override
    public void send(String packet) {
        a(new fh(packet));
    }

    @Override
    public void tick() {
        a();
    }

    @Override
    public List<String> written() {
        return written;
    }

    /** The send. */
    public void a(fh packet) {
        if (g()) {
            m();
            a(packet, null);
        } else {
            j.writeLock().lock();

            try {
                i.add(new a(packet, null));
            } finally {
                j.writeLock().unlock();
            }
        }
    }

    /** The dispatch, which writes the packet to the channel. */
    private void a(fh packet, GenericFutureListener[] listeners) {
        written.add(packet.name);
    }

    /** The flush. */
    private void m() {
        if (k == null)
            return;

        j.readLock().lock();

        try {
            while (!i.isEmpty()) {
                a queued = i.poll();

                a(queued.a, queued.b);
            }
        } finally {
            j.readLock().unlock();
        }
    }

    /** The network tick, which starts with a flush of the queue. */
    public void a() {
        m();
    }

    /** Whether the channel is open. */
    public boolean g() {
        return k != null;
    }

    /** A queued packet with its listeners, whose fields the flush reads through accessors that throw on a null entry. */
    static class a {
        private final fh a;
        private final GenericFutureListener[] b;

        a(fh packet, GenericFutureListener[] listeners) {
            a = packet;
            b = listeners;
        }
    }
}
