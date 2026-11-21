package cpw.mods.niofs.union;

import java.nio.file.FileSystemNotFoundException;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * securejarhandler's provider of union file systems, as far as it keeps them: it registers and removes a file system under the lock of a plain HashMap, and
 * looks one up without it. main looks a file system up that is registered all along, as every class load from a jar does, while the main thread registers
 * more, as mod discovery does for each mod and library it finds, in rounds that each grow the map past the sizes it resizes at.
 */
public class UnionFileSystemProvider {
    private static final int FILE_SYSTEMS = 250;
    private static final int ROUNDS = 30000;

    private final Map<String, Object> fileSystems = new HashMap<>();
    private int index;

    public Object newFileSystem(String path) {
        String key = makeKey(path);

        synchronized (fileSystems) {
            Object fileSystem = new Object();

            fileSystems.put(key, fileSystem);

            return fileSystem;
        }
    }

    public Object getFileSystem(String key) {
        if (!fileSystems.containsKey(key))
            throw new FileSystemNotFoundException();

        return fileSystems.get(key);
    }

    void removeFileSystem(String key) {
        synchronized (fileSystems) {
            fileSystems.remove(key);
        }
    }

    private synchronized String makeKey(String path) {
        return path + "%23" + index++;
    }

    public static void main(String[] arguments) throws Exception {
        for (int round = 1; round <= ROUNDS; round++) {
            UnionFileSystemProvider provider = new UnionFileSystemProvider();
            Object registered = provider.newFileSystem("/mods/first.jar");
            AtomicBoolean done = new AtomicBoolean();
            // The registrations wait for the loader's first lookup: a thread takes longer to start than 250 registrations take, on Windows much longer, so
            // without the wait the loader may look nothing up while the map grows in any of the rounds.
            CountDownLatch looking = new CountDownLatch(1);
            long[] failures = new long[2];

            Thread loader = new Thread(() -> {
                while (!done.get()) {
                    looking.countDown();

                    try {
                        if (provider.getFileSystem("/mods/first.jar%230") != registered)
                            failures[1]++;
                    } catch (FileSystemNotFoundException missed) {
                        failures[0]++;
                    }
                }
            });

            loader.start();
            looking.await();

            for (int added = 0; added < FILE_SYSTEMS; added++)
                provider.newFileSystem("/mods/next.jar");

            done.set(true);
            loader.join();

            if (failures[0] > 0 || failures[1] > 0) {
                System.out.println("MISSED the registered file system in round " + round + ": " + failures[0] + " lookups threw FileSystemNotFoundException and "
                    + failures[1] + " returned nothing; the map is a " + provider.fileSystems.getClass().getSimpleName());
                System.exit(1);
            }
        }

        System.out.println("FOUND the registered file system in every lookup of " + ROUNDS + " rounds; the map is a "
            + new UnionFileSystemProvider().fileSystems.getClass().getSimpleName());
    }
}
