package unionrace;

import java.lang.reflect.Method;
import java.net.URI;
import java.nio.file.FileSystem;
import java.nio.file.FileSystemNotFoundException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.spi.FileSystemProvider;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Races the real UnionFileSystemProvider of a securejarhandler or securemodules jar on the class path: one thread looks a union file system up that is
 * registered all along, as a class load from its jar does, while the main thread registers new ones, as mod discovery does. The provider is reached by
 * reflection, since this is compiled without the jar and the type of the filter that a registration takes changed between releases.
 */
public final class UnionRace {
    private static final int FILE_SYSTEMS = 250;
    private static final int ROUNDS = 3000;

    private UnionRace() {
    }

    public static void main(String[] arguments) throws Exception {
        Class<?> type = Class.forName("cpw.mods.niofs.union.UnionFileSystemProvider");
        Method register = null;

        for (Method method : type.getMethods()) {
            if (method.getName().equals("newFileSystem") && method.getParameterCount() == 2 && method.getParameterTypes()[1] == Path[].class)
                register = method;
        }

        Path directory = Files.createTempDirectory("union-race");

        try {
            for (int round = 1; round <= ROUNDS; round++) {
                FileSystemProvider provider = (FileSystemProvider) type.getConstructor().newInstance();
                FileSystem registered = (FileSystem) register.invoke(provider, null, new Path[] { directory });
                URI address = registered.getPath("/").toUri();
                AtomicBoolean done = new AtomicBoolean();
                long[] failures = new long[2];

                Thread loader = new Thread(() -> {
                    while (!done.get()) {
                        try {
                            if (provider.getFileSystem(address) != registered)
                                failures[1]++;
                        } catch (FileSystemNotFoundException missed) {
                            failures[0]++;
                        }
                    }
                });

                loader.start();

                for (int added = 0; added < FILE_SYSTEMS; added++)
                    register.invoke(provider, null, new Path[] { directory });

                done.set(true);
                loader.join();

                if (failures[0] > 0 || failures[1] > 0) {
                    System.out.println("MISSED the registered file system in round " + round + ": " + failures[0] + " lookups threw FileSystemNotFoundException and "
                        + failures[1] + " returned nothing");
                    Files.delete(directory);
                    System.exit(1);
                }
            }
        } finally {
            Files.delete(directory);
        }

        System.out.println("FOUND the registered file system in every lookup of " + ROUNDS + " rounds");
    }
}
