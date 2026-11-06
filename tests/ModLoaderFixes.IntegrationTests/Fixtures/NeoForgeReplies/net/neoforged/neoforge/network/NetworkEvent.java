package net.neoforged.neoforge.network;

import java.util.concurrent.atomic.AtomicReference;

/** NeoForge's network event, whose context reaches the channel's FML_HANDSHAKE_HANDLER attribute. */
public class NetworkEvent {
    static final AtomicReference<HandshakeHandler> ATTRIBUTE = new AtomicReference<>();

    /** The context of a packet. */
    public static final class Context {
        AtomicReference<HandshakeHandler> attr() {
            return ATTRIBUTE;
        }
    }
}
