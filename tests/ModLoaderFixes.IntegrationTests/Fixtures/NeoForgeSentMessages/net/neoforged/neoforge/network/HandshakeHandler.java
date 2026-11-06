package net.neoforged.neoforge.network;

import java.util.concurrent.atomic.AtomicInteger;

import it.unimi.dsi.fastutil.ints.IntArrayList;

/**
 * Keeps the handshake messages that wait for the client's answer as NeoForge's handshake handler does for Minecraft 1.20.2: in an IntArrayList that the server
 * thread adds each message it sends to, in tickServer, while the network thread removes each one the client answers, in handleIndexedMessage. main runs many
 * handshakes whose client answers each message as soon as it is sent, and counts the answers whose index the list had lost, as a lost add makes NeoForge end the
 * handshake before the last answer.
 */
public class HandshakeHandler {
    private static final int HANDSHAKES = 20000;
    private static final int MESSAGES = 20;

    private final IntArrayList sentMessages = new IntArrayList();
    private final AtomicInteger sent = new AtomicInteger();

    void tickServer(int packetPosition) {
        sentMessages.add(packetPosition);
        sent.incrementAndGet();
    }

    boolean handleIndexedMessage(int index) {
        return sentMessages.removeIf(i -> i == index);
    }

    public static void main(String[] arguments) throws InterruptedException {
        int lost = 0;

        for (int handshake = 0; handshake < HANDSHAKES; handshake++) {
            final HandshakeHandler handler = new HandshakeHandler();
            final int[] unexpected = new int[1];
            Thread network = new Thread(() -> {
                for (int index = 0; index < MESSAGES; index++) {
                    while (handler.sent.get() <= index)
                        Thread.onSpinWait();

                    if (!handler.handleIndexedMessage(index))
                        unexpected[0]++;
                }
            }, "Netty Server IO #4");

            network.start();

            for (int position = 0; position < MESSAGES; position++)
                handler.tickServer(position);

            network.join();
            lost += unexpected[0] + handler.sentMessages.size();
        }

        if (lost > 0) {
            System.out.println("LOST " + lost + " of " + HANDSHAKES * MESSAGES + " handshake messages");
            System.exit(1);
        }

        System.out.println("ANSWERED " + HANDSHAKES * MESSAGES + " handshake messages with " + new HandshakeHandler().sentMessages.getClass().getName());
    }
}
