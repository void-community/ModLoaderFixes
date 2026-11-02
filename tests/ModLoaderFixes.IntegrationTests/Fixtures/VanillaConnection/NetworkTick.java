import java.util.ArrayList;
import java.util.Collections;
import java.util.Iterator;
import java.util.List;

/**
 * Vanilla Minecraft 1.7's network tick: it drops each connection whose channel isn't open and processes the rest. Its main method runs Netty's registration of a
 * new channel, which calls the initializer and then channelActive in one task of the event loop, with a tick of the server thread between the two, then runs the
 * loop's later tasks, lets the connection's login start arrive and ticks again.
 */
public class NetworkTick {
    final List<Connection> connections = Collections.synchronizedList(new ArrayList<Connection>());

    public void tick() {
        synchronized (connections) {
            Iterator<Connection> iterator = connections.iterator();

            while (iterator.hasNext()) {
                Connection connection = iterator.next();

                if (!connection.isOpen()) {
                    iterator.remove();
                    connection.disconnected();
                } else {
                    connection.process();
                }
            }
        }
    }

    public static void main(String[] arguments) {
        NetworkTick tick = new NetworkTick();
        TestChannel channel = new TestChannel();

        new nd(tick).initChannel(channel);
        tick.tick();
        channel.activate();
        channel.runTasks();

        Connection joining = channel.connection();

        joining.receive("login start");
        tick.tick();

        String result = joining.describe("joining") + ", ticked " + tick.connections.size();

        System.out.println(result);
        System.exit(result.equals("joining kept, answered [login start], ticked 1") ? 0 : 1);
    }
}
