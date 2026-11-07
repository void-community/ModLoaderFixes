package net.minecraft.client.audio;

import paulscode.sound.SoundSystem;

/**
 * Loads the sound system as Minecraft 1.7.10's SoundManager does, by Forge's names: loadSoundSystem starts a "Sound Library Loader" thread that builds the sound
 * system and only then marks it loaded, and reloadSoundSystem unloads it, which it does only once it is marked loaded, and loads it again. main reloads as the
 * game starts and again while the first sound system still loads, as Forge's second resource reload at startup can, and then plays a sound as MusicTicker does.
 */
public class SoundManager {
    private volatile SoundSystem sndSystem;
    private volatile boolean loaded;
    private int loads;

    /** reloadSoundSystem. */
    public void func_148596_a() {
        func_148613_b();
        func_148608_i();
    }

    /** unloadSoundSystem. */
    public void func_148613_b() {
        if (loaded) {
            sndSystem.cleanup();
            loaded = false;
        }
    }

    /** loadSoundSystem. */
    private synchronized void func_148608_i() {
        if (!loaded) {
            loads++;
            new Thread(new Runnable() {
                @Override
                public void run() {
                    sndSystem = new SoundSystem();
                    loaded = true;
                }
            }, "Sound Library Loader").start();
        }
    }

    public static void main(String[] arguments) throws InterruptedException {
        SoundManager manager = new SoundManager();

        manager.func_148596_a();
        Thread.sleep(50);
        manager.func_148596_a();
        Thread.sleep(1000);

        try {
            manager.sndSystem.playing("music");
            System.out.println("PLAYING after " + manager.loads + " loads, one at a time");
        } catch (UnsatisfiedLinkError error) {
            System.out.println("CRASHED " + error);
            System.exit(1);
        }
    }
}
