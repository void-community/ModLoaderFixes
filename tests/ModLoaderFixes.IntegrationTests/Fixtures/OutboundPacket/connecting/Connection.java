package connecting;

import net.minecraft.network.Packet;

/** What the connecting screen does with a NetworkManager, under any of its names. */
public interface Connection {
    /** channelActive, which gives the manager its channel. */
    void open(Wire wire);

    /** The connector thread's send of a packet. */
    void send(Packet packet);

    /** The client thread's network tick, which the connecting screen runs. */
    void tick();
}
