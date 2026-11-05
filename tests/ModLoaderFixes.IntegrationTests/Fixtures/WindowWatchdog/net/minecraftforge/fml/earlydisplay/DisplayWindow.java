package net.minecraftforge.fml.earlydisplay;

import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Creates the game's window as Forge's early loading window does from 1.20.1-47.0.19: initWindow schedules the render tick and a watchdog that ends the game
 * with "Timed out trying to setup the Game Window" after 10 seconds, then tries each OpenGL version in turn until one gives a window. Each try here takes 6
 * seconds and the first fails, as creating a context does on a busy machine that renders in software, so the window comes after 12 seconds, while the game is
 * still working on it.
 */
public class DisplayWindow {
    private static final String[] GL_VERSIONS = {"4.6", "4.5"};
    private static final long TRY_MILLISECONDS = 6000;

    private final ScheduledExecutorService renderScheduler = Executors.newSingleThreadScheduledExecutor(runnable -> {
        Thread thread = new Thread(runnable, "fmlearlywindow");

        thread.setDaemon(true);
        return thread;
    });

    private String glVersion;

    public void initWindow(String mcVersion) {
        renderScheduler.schedule(() -> {
        }, 50L, TimeUnit.MILLISECONDS);

        AtomicBoolean successfulWindow = new AtomicBoolean(false);
        ScheduledFuture<?> windowFailFuture = renderScheduler.schedule(() -> {
            if (!successfulWindow.get())
                crashElegantly("Timed out trying to setup the Game Window.");
        }, 10L, TimeUnit.SECONDS);
        long window = 0;
        int versidx = 0;

        do {
            System.out.println("Trying GL version " + GL_VERSIONS[versidx]);
            window = createWindow(versidx);
            versidx++;
        } while (window == 0 && versidx < GL_VERSIONS.length);

        successfulWindow.set(true);

        if (!windowFailFuture.cancel(true))
            throw new IllegalStateException("We died but didn't somehow?");

        glVersion = GL_VERSIONS[versidx - 1];
    }

    public static void main(String[] arguments) {
        long started = System.nanoTime();
        DisplayWindow display = new DisplayWindow();

        display.initWindow("1.20.1");
        System.out.println("WINDOW with GL " + display.glVersion + " after " + TimeUnit.NANOSECONDS.toSeconds(System.nanoTime() - started) + " seconds");
    }

    // Stands in for glfwCreateWindow: each try takes its time, and only the last version gives a window.
    private static long createWindow(int versidx) {
        try {
            Thread.sleep(TRY_MILLISECONDS);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }

        return versidx == GL_VERSIONS.length - 1 ? 1 : 0;
    }

    private void crashElegantly(String errorDetails) {
        System.out.println("ERROR DISPLAY\nFailed to initialize the mod loading system and display.\n\nFailure details:\n" + errorDetails);
        System.exit(1);
    }
}
