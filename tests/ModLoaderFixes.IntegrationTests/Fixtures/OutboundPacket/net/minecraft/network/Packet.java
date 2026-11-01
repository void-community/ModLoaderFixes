package net.minecraft.network;

/** A packet with the connection state it belongs to, which the encoder needs the channel to be in to find the packet's id. */
public final class Packet {
    public static final String HANDSHAKING = "handshaking";
    public static final Packet HANDSHAKE = new Packet("handshake", HANDSHAKING);
    public static final Packet LOGIN_START = new Packet("login start", "login");

    private final String name;
    private final String state;

    private Packet(String name, String state) {
        this.name = name;
        this.state = state;
    }

    public String name() {
        return name;
    }

    public String state() {
        return state;
    }
}
