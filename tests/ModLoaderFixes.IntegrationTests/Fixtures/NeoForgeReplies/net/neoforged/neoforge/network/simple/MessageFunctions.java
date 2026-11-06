package net.neoforged.neoforge.network.simple;

import net.neoforged.neoforge.network.NetworkEvent.Context;

/** NeoForge's functional types of SimpleChannel messages. */
public final class MessageFunctions {
    private MessageFunctions() {
    }

    /** A message's handler, which andThen composes with another to run after it. */
    @FunctionalInterface
    public interface MessageConsumer<MSG> {
        void handle(MSG message, Context context);

        default MessageConsumer<MSG> andThen(MessageConsumer<MSG> other) {
            return (message, context) -> {
                handle(message, context);
                other.handle(message, context);
            };
        }
    }
}
