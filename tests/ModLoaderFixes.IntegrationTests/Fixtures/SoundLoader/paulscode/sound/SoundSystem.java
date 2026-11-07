package paulscode.sound;

import java.util.concurrent.atomic.AtomicReference;

/**
 * Stands in for paulscode's sound system on LWJGL 2's OpenAL: starting the library takes a while, LWJGL lets one sound system hold OpenAL's context at a time, and
 * a sound system that can't get it gives up and destroys OpenAL, after which the one that holds the context finds its functions unbound.
 */
public class SoundSystem {
    private static final AtomicReference<SoundSystem> CONTEXT = new AtomicReference<SoundSystem>();
    private static final long START_MILLISECONDS = 300;

    public SoundSystem() {
        try {
            Thread.sleep(START_MILLISECONDS);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }

        if (!CONTEXT.compareAndSet(null, this)) {
            System.out.println("java.lang.IllegalStateException: Only one OpenAL context may be instantiated at any one time.");
            CONTEXT.set(null);
        }
    }

    public boolean playing(String source) {
        if (CONTEXT.get() != this)
            throw new UnsatisfiedLinkError("org.lwjgl.openal.AL10.nalGetSourcei(II)I");

        return false;
    }

    public void cleanup() {
        CONTEXT.compareAndSet(this, null);
    }
}
