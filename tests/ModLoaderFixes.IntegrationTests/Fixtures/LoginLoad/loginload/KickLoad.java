package loginload;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.DataInputStream;
import java.io.EOFException;
import java.io.IOException;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.locks.LockSupport;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.zip.DataFormatException;

/**
 * Kicks one player after another from a Minecraft 1.7 or 1.8 server in offline mode, and prints how the kicks ended for the players. Each player logs in, as a
 * Forge client with the server's own mods when a 1.7 server's status lists FML's, and then sends Player packets, the packet that an idle client sends every tick,
 * several thousand a second, so that the server often has one unread when it closes the connection. The server's remote console then says something and kicks the
 * player with a reason of its own. A kick counts as received when its Disconnect packet reaches the player and the connection then ends, as reset when the
 * connection is reset instead of ended, and as lost when the Disconnect packet never comes.
 * Usage: KickLoad host port protocol kicks rconPort rconPassword
 */
public final class KickLoad {
    private static final int LOGIN_DISCONNECT = 0x00;
    private static final int LOGIN_SUCCESS = 0x02;
    private static final int SET_COMPRESSION = 0x03;
    private static final int POSITION = 0x08;
    private static final int PLUGIN_MESSAGE = 0x3F;
    private static final int DISCONNECT = 0x40;
    private static final int PLAYER = 0x03;
    private static final int SERVER_PLUGIN_MESSAGE = 0x17;
    // Minecraft 1.8's protocol, from which Forge lets a client without FML's marker in as a vanilla one.
    private static final int VANILLA_CLIENT_PROTOCOL = 47;
    private static final String FML_HANDSHAKE = "FML|HS";
    private static final int SERVER_HELLO = 0;
    private static final int CLIENT_HELLO = 1;
    private static final int MOD_LIST = 2;
    private static final int MOD_ID_DATA = 3;
    private static final int HANDSHAKE_ACK = 0xFF;
    private static final Pattern MOD = Pattern.compile("\\{\"modid\":\"([^\"]+)\",\"version\":\"([^\"]+)\"\\}");
    private static final int TIMEOUT_MILLISECONDS = 30000;
    private static final long PLAYER_PACKET_NANOSECONDS = TimeUnit.MICROSECONDS.toNanos(100);
    private static final String RECEIVED = "received";

    private KickLoad() {
    }

    public static void main(String[] arguments) throws Exception {
        String host = arguments[0];
        int port = Integer.parseInt(arguments[1]);
        int protocol = Integer.parseInt(arguments[2]);
        int kicks = Integer.parseInt(arguments[3]);
        Map<String, Integer> outcomes = new TreeMap<String, Integer>();
        List<String[]> mods = mods(host, port, protocol);
        RemoteConsole console = new RemoteConsole(host, Integer.parseInt(arguments[4]), arguments[5]);

        for (int index = 0; index < kicks; index++) {
            String outcome = kick(host, port, protocol, mods, console, index);
            Integer count = outcomes.get(outcome);

            outcomes.put(outcome, count == null ? 1 : count + 1);
        }

        System.out.println("KICKS " + outcomes);
        System.exit(outcomes.keySet().equals(java.util.Collections.singleton(RECEIVED)) ? 0 : 1);
    }

    private static String kick(String host, int port, int protocol, List<String[]> mods, RemoteConsole console, int index) throws Exception {
        String reason = "kicked-" + index + "-" + Long.toHexString(System.nanoTime());
        Player player = new Player(host, port, protocol, "kick" + index, reason, mods);

        player.start();

        if (!player.joined.await(TIMEOUT_MILLISECONDS, TimeUnit.MILLISECONDS) || player.outcome != null) {
            player.end();
            player.join();

            return "no join: " + player.outcome;
        }

        Thread packets = player.sendPlayerPackets();

        // The server has sent the world by then, and the kick comes at another moment of the player's packets each time.
        Thread.sleep(250 + index % 7 * 13);
        console.run("say before-" + reason, "kick " + player.getName() + " " + reason);
        player.join(TIMEOUT_MILLISECONDS);
        player.end();
        player.join();
        packets.join();

        return player.outcome;
    }

    // The mods that a Forge 1.7 server's status lists, which its handshake wants back from a client, or none for another server. Forge 1.8 lists its mods too, but
    // lets a client in as a vanilla one when its handshake packet lacks FML's marker, as this client's does.
    private static List<String[]> mods(String host, int port, int protocol) throws IOException, DataFormatException {
        List<String[]> mods = new ArrayList<String[]>();

        if (protocol >= VANILLA_CLIENT_PROTOCOL)
            return mods;

        try (Socket socket = new Socket()) {
            socket.connect(new InetSocketAddress(host, port), TIMEOUT_MILLISECONDS);
            socket.setSoTimeout(TIMEOUT_MILLISECONDS);

            OutputStream output = socket.getOutputStream();

            LoginLoad.writePacket(output, -1, 0x00, handshake(host, port, protocol, 1));
            LoginLoad.writePacket(output, -1, 0x00, new byte[0]);

            byte[] status = LoginLoad.readPacket(new DataInputStream(socket.getInputStream()), -1);
            Matcher matcher = MOD.matcher(LoginLoad.readString(new DataInputStream(new ByteArrayInputStream(status, 1, status.length - 1))));

            while (matcher.find())
                mods.add(new String[] { matcher.group(1), matcher.group(2) });
        }

        return mods;
    }

    private static byte[] handshake(String host, int port, int protocol, int nextState) throws IOException {
        ByteArrayOutputStream handshake = new ByteArrayOutputStream();

        LoginLoad.writeVarInt(handshake, protocol);
        LoginLoad.writeString(handshake, host);
        handshake.write(port >> 8);
        handshake.write(port & 0xff);
        LoginLoad.writeVarInt(handshake, nextState);

        return handshake.toByteArray();
    }

    // One player's connection: its thread logs in and reads what the server sends until the connection ends.
    private static final class Player extends Thread {
        private final CountDownLatch joined = new CountDownLatch(1);
        private final Socket socket = new Socket();
        private final String host;
        private final int port;
        private final int protocol;
        private final String reason;
        private final List<String[]> mods;
        private OutputStream output;
        private int threshold = -1;
        private int acknowledged;
        private volatile boolean ended;
        private volatile String outcome;

        Player(String host, int port, int protocol, String name, String reason, List<String[]> mods) {
            super(name);
            this.host = host;
            this.port = port;
            this.protocol = protocol;
            this.reason = reason;
            this.mods = mods;
        }

        @Override
        public void run() {
            boolean received = false;

            try {
                socket.connect(new InetSocketAddress(host, port), TIMEOUT_MILLISECONDS);
                socket.setSoTimeout(TIMEOUT_MILLISECONDS);
                socket.setTcpNoDelay(true);
                output = socket.getOutputStream();

                DataInputStream input = new DataInputStream(socket.getInputStream());
                ByteArrayOutputStream loginStart = new ByteArrayOutputStream();

                LoginLoad.writeString(loginStart, getName());
                send(0x00, handshake(host, port, protocol, 2));
                send(0x00, loginStart.toByteArray());

                for (byte[] packet = LoginLoad.readPacket(input, threshold); packet[0] != LOGIN_SUCCESS; packet = LoginLoad.readPacket(input, threshold)) {
                    DataInputStream fields = fields(packet);

                    if (packet[0] == LOGIN_DISCONNECT)
                        throw new IOException("disconnected at login: " + LoginLoad.readString(fields));

                    if (packet[0] == SET_COMPRESSION)
                        threshold = LoginLoad.readVarInt(fields);
                }

                while (true) {
                    byte[] packet = LoginLoad.readPacket(input, threshold);
                    int id = packet[0] & 0xff;

                    if (id == PLUGIN_MESSAGE && !mods.isEmpty())
                        answerHandshake(fields(packet));
                    else if (id == POSITION)
                        // The server has put the player in its world. The player never answers the position, so the server takes its Player packets for those
                        // of a client that hasn't moved yet, and neither moves the player nor checks how it moves.
                        joined.countDown();
                    else if (id == DISCONNECT)
                        received |= LoginLoad.readString(fields(packet)).contains(reason);
                }
            } catch (EOFException end) {
                outcome = received ? RECEIVED : "lost";
            } catch (IOException | DataFormatException exception) {
                // The client's own close ends a player that the server never let in or never kicked.
                String how = ended ? "still connected" : String.valueOf(exception.getMessage()).contains("reset") ? "reset" : "failed: " + exception.getMessage();

                outcome = (received ? RECEIVED : "lost") + ", " + how;
            } finally {
                joined.countDown();
                end();
            }
        }

        void end() {
            ended = true;

            try {
                socket.close();
            } catch (IOException exception) {
                // The connection has ended either way.
            }
        }

        // Sends Player packets, with a pause that the thread's wake-up about doubles, until the connection ends.
        Thread sendPlayerPackets() {
            Thread packets = new Thread(getName() + " packets") {
                @Override
                public void run() {
                    try {
                        while (!ended) {
                            send(PLAYER, new byte[] { 1 });
                            LockSupport.parkNanos(PLAYER_PACKET_NANOSECONDS);
                        }
                    } catch (IOException exception) {
                        // The server closed the connection.
                    }
                }
            };

            packets.start();

            return packets;
        }

        private static DataInputStream fields(byte[] packet) {
            return new DataInputStream(new ByteArrayInputStream(packet, 1, packet.length - 1));
        }

        private synchronized void send(int id, byte[] fields) throws IOException {
            LoginLoad.writePacket(output, threshold, id, fields);
        }

        // Forge 1.7 lets a player in only once its client has answered FML's handshake: the server's hello with the client's channels, hello and mods, which
        // must be the server's own, and the server's mods, ids and acknowledgements each with an acknowledgement of the client's next state.
        private void answerHandshake(DataInputStream message) throws IOException {
            String channel = LoginLoad.readString(message);
            int length = message.readUnsignedShort();

            // Forge writes the length of a long message, such as the ids, in three bytes.
            if ((length & 0x8000) != 0)
                message.readUnsignedByte();

            if (!channel.equals(FML_HANDSHAKE))
                return;

            int kind = message.readUnsignedByte();

            if (kind == SERVER_HELLO) {
                ByteArrayOutputStream modList = new ByteArrayOutputStream();

                modList.write(MOD_LIST);
                LoginLoad.writeVarInt(modList, mods.size());

                for (String[] mod : mods) {
                    LoginLoad.writeString(modList, mod[0]);
                    LoginLoad.writeString(modList, mod[1]);
                }

                sendMessage("REGISTER", "FML|HS\0FML\0FML|MP\0FML\0FORGE".getBytes(StandardCharsets.UTF_8));
                sendMessage(FML_HANDSHAKE, new byte[] { CLIENT_HELLO, message.readByte() });
                sendMessage(FML_HANDSHAKE, modList.toByteArray());
            } else if (kind == MOD_LIST)
                sendMessage(FML_HANDSHAKE, new byte[] { (byte) HANDSHAKE_ACK, 2 });
            else if (kind == MOD_ID_DATA)
                sendMessage(FML_HANDSHAKE, new byte[] { (byte) HANDSHAKE_ACK, 3 });
            else if (kind == HANDSHAKE_ACK)
                sendMessage(FML_HANDSHAKE, new byte[] { (byte) HANDSHAKE_ACK, (byte) (3 + ++acknowledged) });
        }

        private void sendMessage(String channel, byte[] data) throws IOException {
            ByteArrayOutputStream fields = new ByteArrayOutputStream();

            LoginLoad.writeString(fields, channel);
            fields.write(data.length >> 8);
            fields.write(data.length & 0xff);
            fields.write(data);
            send(SERVER_PLUGIN_MESSAGE, fields.toByteArray());
        }
    }

    // The server's remote console, which runs one command for each packet and answers it. A server now and then ends a connection it has only just accepted
    // before it answers the login: it starts the connection's thread before it marks it running. So each run of commands logs in anew, and again when the
    // connection ends before the last answer.
    private static final class RemoteConsole {
        private static final int LOGIN = 3;
        private static final int COMMAND = 2;
        private static final int ATTEMPTS = 20;
        private static final int PAUSE_MILLISECONDS = 50;

        private final String host;
        private final int port;
        private final String password;
        private int requests;

        RemoteConsole(String host, int port, String password) {
            this.host = host;
            this.port = port;
            this.password = password;
        }

        // Runs the commands one after another over one connection. A command that ran before the connection ended runs again with the next connection, which
        // the say and the kick of a player that has left by then both take.
        void run(String... commands) throws IOException, InterruptedException {
            IOException ended = null;

            for (int attempt = 0; attempt < ATTEMPTS; attempt++) {
                try (Socket socket = new Socket()) {
                    socket.connect(new InetSocketAddress(host, port), TIMEOUT_MILLISECONDS);
                    socket.setSoTimeout(TIMEOUT_MILLISECONDS);
                    socket.setTcpNoDelay(true);

                    DataInputStream input = new DataInputStream(socket.getInputStream());
                    OutputStream output = socket.getOutputStream();

                    if (request(input, output, LOGIN, password) < 0)
                        throw new IllegalStateException("The remote console refused the password");

                    for (String command : commands)
                        request(input, output, COMMAND, command);

                    return;
                } catch (IOException exception) {
                    ended = exception;
                    Thread.sleep(PAUSE_MILLISECONDS);
                }
            }

            throw ended;
        }

        // Sends one request in one write, as the server reads each with one read, and returns the id that the server answers it with, which is -1 for a refused
        // password.
        private int request(DataInputStream input, OutputStream output, int type, String text) throws IOException {
            byte[] payload = text.getBytes(StandardCharsets.UTF_8);
            ByteBuffer request = ByteBuffer.allocate(14 + payload.length).order(ByteOrder.LITTLE_ENDIAN);

            request.putInt(10 + payload.length).putInt(++requests).putInt(type).put(payload).put((byte) 0).put((byte) 0);
            output.write(request.array());
            output.flush();

            byte[] header = new byte[4];

            input.readFully(header);

            byte[] answer = new byte[ByteBuffer.wrap(header).order(ByteOrder.LITTLE_ENDIAN).getInt()];

            input.readFully(answer);

            return ByteBuffer.wrap(answer).order(ByteOrder.LITTLE_ENDIAN).getInt();
        }
    }
}
