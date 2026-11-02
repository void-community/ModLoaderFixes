import java.util.Queue;
import java.util.concurrent.ConcurrentLinkedQueue;

import connecting.Connection;
import connecting.Wire;
import io.netty.channel.SimpleChannelInboundHandler;
import io.netty.util.concurrent.GenericFutureListener;
import net.minecraft.network.Packet;

/**
 * Vanilla 1.7's NetworkManager, under the names 1.7.10 gives it and its packet, and with the same code as Forge's: a send queues the packet while there is no
 * channel, and otherwise flushes the queue and dispatches the packet.
 */
public class ej extends SimpleChannelInboundHandler implements Connection {
    private final Queue<ft> l = new ConcurrentLinkedQueue<ft>();
    private Wire m;

    @Override
    public void open(Wire wire) {
        m = wire;
    }

    @Override
    public void send(Packet packet) {
        a(new ft(packet));
    }

    @Override
    public void tick() {
        a();
    }

    /** The send. */
    public void a(ft packet, GenericFutureListener... listeners) {
        if (m != null) {
            i();
            b(packet, listeners);
        } else {
            l.add(packet);
        }
    }

    /** The dispatch. */
    private void b(final ft packet, GenericFutureListener[] listeners) {
        final String current = m.state();
        final Wire wire = m;

        wire.execute(new Runnable() {
            @Override
            public void run() {
                if (!packet.packet.state().equals(current))
                    wire.setState(packet.packet.state());

                wire.write(packet.packet);
            }
        });
    }

    /** The flush. */
    private void i() {
        if (m != null) {
            while (!l.isEmpty())
                b(l.poll(), null);
        }
    }

    /** The network tick, which starts with a flush of the queue. */
    public void a() {
        i();
    }
}
