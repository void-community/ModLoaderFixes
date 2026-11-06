package net.neoforged.neoforge.network;

import java.util.List;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.IntSupplier;

import it.unimi.dsi.fastutil.ints.IntArrayList;
import net.neoforged.neoforge.network.NetworkEvent.Context;
import net.neoforged.neoforge.network.simple.MessageFunctions.MessageConsumer;

/**
 * Handles the client's indexed replies as NeoForge's handshake handler does for Minecraft 1.20.2: indexFirst composes the removal of a reply's index from
 * sentMessages with the reply's own handler through consumerFor and MessageConsumer.andThen, each half looks the connection's handler up in the channel's
 * attribute, and the server thread's tickServer sets the attribute to null once no reply is outstanding. The network thread waits after the removal for the server
 * thread's tick, as a busy network thread can, so the tick lands between the halves.
 */
public class HandshakeHandler {
    private static final List<String> events = new CopyOnWriteArrayList<>();
    private static final CountDownLatch removed = new CountDownLatch(1);
    private static final CountDownLatch completed = new CountDownLatch(1);

    private final IntArrayList sentMessages = new IntArrayList();

    @FunctionalInterface
    public interface HandshakeConsumer<MSG extends IntSupplier> {
        void accept(HandshakeHandler handler, MSG message, Context context);
    }

    /** The client's acknowledgement of the message with an index. */
    static final class C2SAcknowledge implements IntSupplier {
        private final int index;

        C2SAcknowledge(int index) {
            this.index = index;
        }

        @Override
        public int getAsInt() {
            return index;
        }
    }

    public static <MSG extends IntSupplier> MessageConsumer<MSG> consumerFor(HandshakeConsumer<MSG> consumer) {
        return (m, c) -> consumer.accept(getHandshake(c), m, c);
    }

    public static <MSG extends IntSupplier> MessageConsumer<MSG> indexFirst(HandshakeConsumer<MSG> next) {
        MessageConsumer<MSG> loginIndexedMessageSupplierBiConsumer = consumerFor(HandshakeHandler::handleIndexedMessage);
        return loginIndexedMessageSupplierBiConsumer.andThen(consumerFor(next));
    }

    private static HandshakeHandler getHandshake(Context contextSupplier) {
        return contextSupplier.attr().get();
    }

    <MSG extends IntSupplier> void handleIndexedMessage(MSG message, Context c) {
        sentMessages.removeIf(i -> i == message.getAsInt());
        events.add("removed " + message.getAsInt());
        removed.countDown();
        await(completed);
    }

    void handleClientAck(C2SAcknowledge message, Context c) {
        events.add("acknowledged " + message.getAsInt());
    }

    boolean tickServer() {
        if (!sentMessages.isEmpty())
            return false;

        NetworkEvent.ATTRIBUTE.set(null);
        events.add("complete");
        return true;
    }

    public static void main(String[] arguments) throws InterruptedException {
        final HandshakeHandler handler = new HandshakeHandler();
        final MessageConsumer<C2SAcknowledge> reply = indexFirst(HandshakeHandler::handleClientAck);
        final Context context = new Context();
        final AtomicReference<Throwable> dropped = new AtomicReference<>();

        NetworkEvent.ATTRIBUTE.set(handler);
        handler.sentMessages.add(19);

        Thread network = new Thread(() -> reply.handle(new C2SAcknowledge(19), context), "Netty Epoll Server IO #4");

        network.setUncaughtExceptionHandler((thread, exception) -> dropped.set(exception));
        network.start();
        await(removed);
        handler.tickServer();
        completed.countDown();
        network.join();

        if (dropped.get() != null) {
            System.out.println("DROPPED " + dropped.get());
            System.exit(1);
        }

        System.out.println("EVENTS " + events);
    }

    private static void await(CountDownLatch latch) {
        try {
            latch.await(5, TimeUnit.SECONDS);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }
}
