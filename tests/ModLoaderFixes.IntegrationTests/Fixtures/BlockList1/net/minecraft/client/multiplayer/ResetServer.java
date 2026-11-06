package net.minecraft.client.multiplayer;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;

/** Stands in for sessionserver.mojang.com: it answers each request with the first chunk of a list and then resets the connection, as a cut-off answer does. */
final class ResetServer implements Runnable {
    private final ServerSocket socket;

    private ResetServer(ServerSocket socket) {
        this.socket = socket;
    }

    /** Starts the server on a free port and gives the address of its list. */
    static String start() throws Exception {
        ServerSocket socket = new ServerSocket(0, 50, InetAddress.getLoopbackAddress());
        Thread thread = new Thread(new ResetServer(socket), "Reset server");

        thread.setDaemon(true);
        thread.start();
        return "http://127.0.0.1:" + socket.getLocalPort() + "/blockedservers";
    }

    @Override
    public void run() {
        try {
            while (true) {
                Socket client = socket.accept();
                BufferedReader request = new BufferedReader(new InputStreamReader(client.getInputStream(), StandardCharsets.ISO_8859_1));
                String line;

                while ((line = request.readLine()) != null && !line.isEmpty()) {
                }

                String chunk = "6f9a1ef0c5d3b8c4f1f0e2d9b7a6c5d4e3f2a1b0\n";
                OutputStream out = client.getOutputStream();

                out.write(("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nTransfer-Encoding: chunked\r\n\r\n" + Integer.toHexString(chunk.length()) + "\r\n" + chunk + "\r\n")
                    .getBytes(StandardCharsets.ISO_8859_1));
                out.flush();
                Thread.sleep(200);
                // A zero linger closes with a reset, so the client's read of the next chunk fails with "Connection reset".
                client.setSoLinger(true, 0);
                client.close();
            }
        } catch (Exception exception) {
        }
    }
}
