package net.minecraft.network;

import java.util.Queue;
import java.util.concurrent.ConcurrentLinkedQueue;

import connecting.Connection;
import connecting.Wire;
import io.netty.channel.SimpleChannelInboundHandler;
import io.netty.util.concurrent.GenericFutureListener;

/**
 * Forge 1.7's NetworkManager, by FML's names: a send queues the packet while there is no channel, and otherwise flushes the queue and dispatches the packet,
 * which reads the channel's state on the calling thread and queues the write, switching the state first when the packet's differs.
 */
public class NetworkManager extends SimpleChannelInboundHandler implements Connection {
    private final Queue<Packet> outboundPacketsQueue = new ConcurrentLinkedQueue<Packet>();
    private Wire channel;

    @Override
    public void open(Wire wire) {
        channel = wire;
    }

    @Override
    public void send(Packet packet) {
        func_150725_a(packet);
    }

    @Override
    public void tick() {
        func_74428_b();
    }

    /** scheduleOutboundPacket. */
    public void func_150725_a(Packet packet, GenericFutureListener... listeners) {
        if (channel != null) {
            func_150733_h();
            func_150732_b(packet, listeners);
        } else {
            outboundPacketsQueue.add(packet);
        }
    }

    /** dispatchPacket. */
    private void func_150732_b(final Packet packet, GenericFutureListener[] listeners) {
        final String current = channel.state();
        final Wire wire = channel;

        wire.execute(new Runnable() {
            @Override
            public void run() {
                if (!packet.state().equals(current))
                    wire.setState(packet.state());

                wire.write(packet);
            }
        });
    }

    /** flushOutboundQueue. */
    private void func_150733_h() {
        if (channel != null) {
            while (!outboundPacketsQueue.isEmpty())
                func_150732_b(outboundPacketsQueue.poll(), null);
        }
    }

    /** processReceivedPackets, which starts with a flush of the queue. */
    public void func_74428_b() {
        func_150733_h();
    }
}
