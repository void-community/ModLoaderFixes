package net.minecraftforge.network;

import java.util.List;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.BiConsumer;
import java.util.function.IntSupplier;
import java.util.function.Supplier;

/**
 * Handles the client's indexed replies as Forge's handshake handler does from Minecraft 1.18 to 1.20.1: indexFirst composes the removal of a reply's index from
 * sentMessages with the reply's own handler, each half looks the connection's handler up in the channel's attribute, and the server thread's tickServer sets the
 * attribute to null once no reply is outstanding. The list is a CopyOnWriteArrayList already, as the agent makes it, so the halves' lookups are the race left. The
 * network thread waits after the removal for the server thread's tick, as a busy network thread can, so the tick lands between the halves.
 */
public class HandshakeHandler {
    private static final AtomicReference<HandshakeHandler> attribute = new AtomicReference<>();
    private static final List<String> events = new CopyOnWriteArrayList<>();
    private static final CountDownLatch removed = new CountDownLatch(1);
    private static final CountDownLatch completed = new CountDownLatch(1);

    private final List<Integer> sentMessages = new CopyOnWriteArrayList<>();

    interface HandshakeConsumer<MSG extends IntSupplier> {
        void accept(HandshakeHandler handler, MSG message, Supplier<Context> context);
    }

    /** The context of a packet, which reaches the channel's FML_HANDSHAKE_HANDLER attribute. */
    static final class Context {
        AtomicReference<HandshakeHandler> attr() {
            return attribute;
        }
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

    public static <MSG extends IntSupplier> BiConsumer<MSG, Supplier<Context>> biConsumerFor(HandshakeConsumer<MSG> consumer) {
        return (m, c) -> consumer.accept(getHandshake(c), m, c);
    }

    public static <MSG extends IntSupplier> BiConsumer<MSG, Supplier<Context>> indexFirst(HandshakeConsumer<MSG> next) {
        final BiConsumer<MSG, Supplier<Context>> initial = biConsumerFor(HandshakeHandler::handleIndexedMessage);
        return initial.andThen(biConsumerFor(next));
    }

    private static HandshakeHandler getHandshake(Supplier<Context> contextSupplier) {
        return contextSupplier.get().attr().get();
    }

    <MSG extends IntSupplier> void handleIndexedMessage(MSG message, Supplier<Context> c) {
        sentMessages.removeIf(i -> i == message.getAsInt());
        events.add("removed " + message.getAsInt());
        removed.countDown();
        await(completed);
    }

    void handleClientAck(C2SAcknowledge message, Supplier<Context> c) {
        events.add("acknowledged " + message.getAsInt());
    }

    boolean tickServer() {
        if (!sentMessages.isEmpty())
            return false;

        attribute.set(null);
        events.add("complete");
        return true;
    }

    public static void main(String[] arguments) throws InterruptedException {
        final HandshakeHandler handler = new HandshakeHandler();
        final BiConsumer<C2SAcknowledge, Supplier<Context>> reply = indexFirst(HandshakeHandler::handleClientAck);
        final Context context = new Context();
        final AtomicReference<Throwable> dropped = new AtomicReference<>();

        attribute.set(handler);
        handler.sentMessages.add(18);

        Thread network = new Thread(() -> reply.accept(new C2SAcknowledge(18), () -> context), "Netty Server IO #2");

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
