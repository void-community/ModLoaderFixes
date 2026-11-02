package net.minecraft.client.sounds;

import java.util.Collections;
import java.util.IdentityHashMap;
import java.util.List;
import java.util.Set;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executor;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.function.Consumer;

/**
 * Releases sound channels as Minecraft's ChannelAccess does from 1.14 to 1.20.4: clear releases each channel on the thread that calls it, the Render thread as the
 * client leaves a world, while the other methods hand their work to the sound executor. A streamed sound that a worker thread finished opening has queued its
 * first buffers on the sound thread, as SoundEngine.play's callback does, and the sound thread is decoding them when the Render thread clears. The stream stands
 * in for OggAudioStream, whose close frees the stb_vorbis decoder: a decode that finds its stream freed when it finishes is the read that crashes the JVM.
 */
public class ChannelAccess {
    private static final List<String> events = new CopyOnWriteArrayList<>();
    private static final CountDownLatch decoding = new CountDownLatch(1);
    private static final CountDownLatch closed = new CountDownLatch(1);

    private final Set<ChannelHandle> channels = Collections.newSetFromMap(new IdentityHashMap<>());
    private final Executor executor;

    public ChannelAccess(Executor executor) {
        this.executor = executor;
    }

    public void clear() {
        this.channels.forEach(ChannelHandle::release);
        this.channels.clear();
    }

    /** A channel that streams a sound, as com.mojang.blaze3d.audio.Channel does. */
    static final class Channel {
        private Stream stream;

        void attachBufferStream(Stream stream) {
            this.stream = stream;
            stream.read();
        }

        void destroy() {
            if (this.stream != null) {
                this.stream.close();
                this.stream = null;
            }
        }
    }

    /** OggAudioStream's decoder, which close frees. A decode takes long enough for the Render thread to clear while it runs. */
    static final class Stream {
        private volatile boolean freed;

        void read() {
            events.add("decoding");
            decoding.countDown();
            await(closed, 1);
            events.add(this.freed ? "decoded freed memory" : "decoded");
        }

        void close() {
            this.freed = true;
            events.add("closed");
            closed.countDown();
        }
    }

    final class ChannelHandle {
        private Channel channel;

        ChannelHandle(Channel channel) {
            this.channel = channel;
        }

        void execute(Consumer<Channel> action) {
            ChannelAccess.this.executor.execute(() -> {
                if (this.channel != null)
                    action.accept(this.channel);
            });
        }

        void release() {
            this.channel.destroy();
            this.channel = null;
        }
    }

    public static void main(String[] arguments) throws InterruptedException {
        ExecutorService sound = Executors.newSingleThreadExecutor(runnable -> new Thread(runnable, "Sound engine"));
        ChannelAccess access = new ChannelAccess(sound);
        ChannelHandle handle = access.new ChannelHandle(new Channel());

        access.channels.add(handle);
        handle.execute(channel -> channel.attachBufferStream(new Stream()));
        await(decoding, 5);
        access.clear();
        sound.shutdown();
        sound.awaitTermination(5, TimeUnit.SECONDS);

        if (events.contains("decoded freed memory")) {
            System.out.println("CRASHED " + events);
            System.exit(1);
        }

        System.out.println("EVENTS " + events);
    }

    private static void await(CountDownLatch latch, int seconds) {
        try {
            latch.await(seconds, TimeUnit.SECONDS);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }
}
