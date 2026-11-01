package cpw.mods.fml.client;

import java.util.concurrent.CountDownLatch;

/** Forge 1.7's FMLClientHandler, with the method PlayClientWait adds to it: playClientBlock is private there. */
public class FMLClientHandler {
    public static FMLClientHandler instance() {
        throw new UnsupportedOperationException();
    }

    /** playClientBlock, the latch that the client thread counts down once it has set the play handler up. */
    public CountDownLatch modloaderfixes$playClientBlock() {
        throw new UnsupportedOperationException();
    }
}
