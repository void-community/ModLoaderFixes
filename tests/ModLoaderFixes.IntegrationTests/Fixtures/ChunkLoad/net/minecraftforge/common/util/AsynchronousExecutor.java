package net.minecraftforge.common.util;

/** Forge's executor of loads in stages, as far as a load that skips the queue goes: it returns what the first stage gave. */
public final class AsynchronousExecutor<P, T> {
    /** The stages of a load. */
    public interface CallBackProvider<P, T> {
        T callStage1(P parameter);

        void callStage2(P parameter, T object);
    }

    private final CallBackProvider<P, T> provider;

    public AsynchronousExecutor(CallBackProvider<P, T> provider) {
        this.provider = provider;
    }

    public T getSkipQueue(P parameter) {
        T object = provider.callStage1(parameter);

        provider.callStage2(parameter, object);

        return object;
    }
}
