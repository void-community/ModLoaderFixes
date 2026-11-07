package modloaderfixes;

import java.util.concurrent.TimeUnit;

/**
 * Lets a Minecraft 1.7 to 1.13.2 client reload its sound system only once the sound system it is still loading has loaded. SoundManager.loadSoundSystem starts
 * a "Sound Library Loader" thread that builds the sound system and only then marks it loaded, and reloadSoundSystem unloads a sound system only once it is marked
 * loaded. A reload during the load, as Forge's second resource reload at startup can be, unloads nothing and starts a second loader, whose sound system can't get
 * OpenAL's one context, and the client crashes once that loader gives up. This remembers the loader thread that loadSoundSystem starts, and reloadSoundSystem
 * waits for it before it runs, so its unload sees the loaded sound system and cleans it up before the next load. It names no game type.
 */
public final class SoundLoaders {
    // Longer than the 30 seconds paulscode's sound system waits for its library to start, so a loader that gives up has finished by then.
    private static final long LIMIT_MILLISECONDS = TimeUnit.SECONDS.toMillis(40);

    private static volatile Thread loader;

    private SoundLoaders() {
    }

    /** Stands in for the loader thread's start() in loadSoundSystem, and remembers the thread. */
    public static void start(Thread thread) {
        loader = thread;
        thread.start();
    }

    /** Runs before reloadSoundSystem: waits for the loader thread that is still building a sound system, for at most 40 seconds. */
    public static void awaitLoader(Object manager) {
        Thread running = loader;

        if (running == null || running == Thread.currentThread())
            return;

        try {
            running.join(LIMIT_MILLISECONDS);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }
}
