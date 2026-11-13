package modloaderfixes;

/**
 * Lets a Minecraft 1.14 client's entity render dispatcher draw an entity only once it has a camera. EntityRendererManager.setWorld clears the camera when the
 * client leaves a level, and only prepare, which the level renderer's entity pass calls, sets it again; that pass skips the first two frames of a new level, which
 * still draw their particles. An item pickup particle draws its item through the dispatcher, whose renderer then asks how far the item is from the camera, and
 * the game crashes with a NullPointerException in Post-rendering entity in world. The patched render hands its camera here with its own body, which runs only
 * when there is a camera; without one there is no view to draw the entity in. It names no game type.
 */
public final class EntityRenders {
    private EntityRenders() {
    }

    /** Runs the dispatcher's render when it has a camera. */
    public static void renderWithCamera(Object camera, Runnable render) {
        if (camera != null)
            render.run();
    }
}
