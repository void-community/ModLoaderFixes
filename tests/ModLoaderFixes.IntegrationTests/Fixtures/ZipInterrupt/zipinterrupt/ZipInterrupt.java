package zipinterrupt;

import java.io.InputStream;
import java.nio.file.FileSystem;
import java.nio.file.FileSystems;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.zip.ZipEntry;
import java.util.zip.ZipOutputStream;

/**
 * Reads a jar through the JDK's zip file system, as Forge's module loader reads each jar of its layer: one thread reads entries until it is interrupted, as a
 * worker that an executor stops does while it loads a class, and then another thread reads an entry it hadn't read yet, as the render thread's next class load
 * does.
 */
public final class ZipInterrupt {
    private static final int ENTRIES = 64;

    private ZipInterrupt() {
    }

    public static void main(String[] arguments) throws Exception {
        Path jar = Files.createTempFile("zipinterrupt", ".jar");

        try (ZipOutputStream out = new ZipOutputStream(Files.newOutputStream(jar))) {
            byte[] data = new byte[1 << 20];

            for (int index = 0; index < ENTRIES; index++) {
                out.putNextEntry(new ZipEntry("e" + index));
                out.write(data);
                out.closeEntry();
            }
        }

        final FileSystem zip = FileSystems.newFileSystem(jar, (ClassLoader) null);
        final StringBuilder interrupted = new StringBuilder();
        Thread reader = new Thread(() -> {
            byte[] buffer = new byte[8192];

            try {
                for (int round = 0; ; round++) {
                    try (InputStream in = Files.newInputStream(zip.getPath("e" + (round % (ENTRIES - 1))))) {
                        while (in.read(buffer) >= 0) {
                        }
                    }
                }
            } catch (Exception exception) {
                interrupted.append(exception);
            }
        }, "modloading-worker-0");

        reader.start();
        Thread.sleep(200);
        reader.interrupt();
        reader.join();

        String next;

        try (InputStream in = Files.newInputStream(zip.getPath("e" + (ENTRIES - 1)))) {
            next = in.read() >= 0 ? "read" : "empty";
        } catch (Exception exception) {
            next = exception.toString();
        }

        System.out.println("interrupted reader: " + interrupted + "; next entry: " + next);
        System.exit(next.equals("read") ? 0 : 1);
    }
}
