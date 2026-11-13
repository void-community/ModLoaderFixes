package net.minecraft.client.renderer.entity;

import net.minecraft.client.renderer.ActiveRenderInfo;
import net.minecraft.entity.Entity;
import net.minecraft.world.World;

/**
 * Minecraft 1.14's entity render dispatcher, under MCP's names: setWorld clears the camera when the client leaves a level, only prepare sets it, and renderEntity
 * draws an entity whose renderer, after it draws, asks how far the entity is from the camera for its shadow. main is the client: it leaves a server, joins
 * another, and in the new level's first frame, which the level renderer's entity pass skips, a pickup particle draws the item the player picked up; then the
 * next frame's entity pass prepares the dispatcher and the particle draws it again.
 */
public class EntityRendererManager {
    private World world;
    private ActiveRenderInfo info;
    private int drawn;

    public void func_217781_a(World world, ActiveRenderInfo info) {
        this.world = world;
        this.info = info;
    }

    public void func_78717_a(World world) {
        this.world = world;
        if (world == null)
            this.info = null;
    }

    public void func_188391_a(Entity entity, double x, double y, double z, float yaw, float partialTicks, boolean debug) {
        drawn++;
        double distance = func_78714_a(entity.posX, entity.posY, entity.posZ);

        if (distance < 0)
            throw new IllegalStateException();
    }

    public double func_78714_a(double x, double y, double z) {
        double[] view = this.info.getProjectedView();
        double dx = x - view[0];
        double dy = y - view[1];
        double dz = z - view[2];

        return dx * dx + dy * dy + dz * dz;
    }

    public static void main(String[] arguments) {
        EntityRendererManager dispatcher = new EntityRendererManager();
        World first = new World();
        Entity item = new Entity();

        dispatcher.func_78717_a(first);
        dispatcher.func_217781_a(first, new ActiveRenderInfo());
        dispatcher.func_188391_a(item, 4, 64, 4, 0, 0, false);
        // The client leaves the first server, and joins the next.
        dispatcher.func_78717_a(null);
        dispatcher.func_78717_a(new World());

        String firstFrame = draw(dispatcher, item);

        dispatcher.func_217781_a(dispatcher.world, new ActiveRenderInfo());

        String nextFrame = draw(dispatcher, item);

        System.out.println("first frame: " + firstFrame + "; next frame: " + nextFrame);
        System.exit(firstFrame.equals("skipped") && nextFrame.equals("drawn") ? 0 : 1);
    }

    // What the pickup particle's draw of the item did: drew it, drew nothing, or threw.
    private static String draw(EntityRendererManager dispatcher, Entity item) {
        int before = dispatcher.drawn;

        try {
            dispatcher.func_188391_a(item, item.posX, item.posY, item.posZ, 0, 0.5F, false);
        } catch (RuntimeException exception) {
            return exception.toString();
        }

        return dispatcher.drawn > before ? "drawn" : "skipped";
    }
}
