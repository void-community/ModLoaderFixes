package connecting;

import java.util.List;

/** What the connecting screen does with a NetworkManager, under any of its names. */
public interface Connection {
    /** channelActive, which gives the manager its channel. */
    void open();

    /** A send of a packet. */
    void send(String packet);

    /** The client thread's network tick, which the connecting screen runs. */
    void tick();

    /** The packets written to the channel so far. */
    List<String> written();
}
