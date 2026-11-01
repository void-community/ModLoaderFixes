import java.util.Queue;
import java.util.concurrent.ConcurrentLinkedQueue;

import connecting.Connection;
import connecting.Wire;
import io.netty.channel.SimpleChannelInboundHandler;
import io.netty.util.concurrent.GenericFutureListener;
import net.minecraft.network.Packet;

/**
 * Vanilla 1.7's NetworkManager, under a name of its own as each obfuscated release has, and with the same code as Forge's: a send queues the packet while there
 * is no channel, and otherwise flushes the queue and dispatches the packet.
 */
public class PacketConnection extends SimpleChannelInboundHandler implements Connection {
    private final Queue<Packet> l = new ConcurrentLinkedQueue<Packet>();
    private Wire m;

    @Override
    public void open(Wire wire) {
        m = wire;
    }

    @Override
    public void send(Packet packet) {
        a(packet);
    }

    @Override
    public void tick() {
        a();
    }

    /** The send. */
    public void a(Packet packet, GenericFutureListener... listeners) {
        if (m != null) {
            i();
            b(packet, listeners);
        } else {
            l.add(packet);
        }
    }

    /** The dispatch. */
    private void b(final Packet packet, GenericFutureListener[] listeners) {
        final String current = m.state();
        final Wire wire = m;

        wire.execute(new Runnable() {
            @Override
            public void run() {
                if (!packet.state().equals(current))
                    wire.setState(packet.state());

                wire.write(packet);
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
