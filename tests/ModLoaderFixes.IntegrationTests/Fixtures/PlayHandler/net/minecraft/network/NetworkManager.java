package net.minecraft.network;

import cpw.mods.fml.client.FMLClientHandler;
import net.minecraft.client.network.NetHandlerPlayClient;

/**
 * Minecraft 1.7's connection by FML's names: the network thread switches its state, and only the client thread's network tick reports the switch to the handler,
 * which for the play state sets the play handler up, as FML's patch of NetHandlerLoginClient.onConnectionStateTransition does, and then tells FMLClientHandler.
 * closeChannel stores the exit message on the thread that closes it, and the channel stays open to the network thread, which would run the close itself.
 */
public class NetworkManager {
    private volatile INetHandler netHandler = new net.minecraft.client.network.NetHandlerLoginClient();
    private volatile EnumConnectionState state = EnumConnectionState.LOGIN;
    private EnumConnectionState reportedState = EnumConnectionState.LOGIN;
    private volatile net.minecraft.util.IChatComponent exitMessage;

    /** closeChannel, as the connecting screen's cancel calls it. */
    public void func_150718_a(net.minecraft.util.IChatComponent reason) {
        exitMessage = reason;
    }

    /** getExitMessage. */
    public net.minecraft.util.IChatComponent func_150730_f() {
        return exitMessage;
    }

    /** isChannelOpen. */
    public boolean func_150724_d() {
        return true;
    }

    /** getNetHandler. */
    public INetHandler func_150729_e() {
        return netHandler;
    }

    /** setNetHandler. */
    public void func_150719_a(INetHandler handler) {
        netHandler = handler;
    }

    /** setConnectionState, on the network thread. */
    public void func_150723_a(EnumConnectionState newState) {
        state = newState;
    }

    /** processReceivedPackets, the client thread's network tick, which reports a switch of state. */
    public void func_74428_b() {
        EnumConnectionState current = state;

        if (current != reportedState && current == EnumConnectionState.PLAY) {
            NetHandlerPlayClient playClient = new NetHandlerPlayClient(this);

            func_150719_a(playClient);
            FMLClientHandler.instance().setPlayClient(playClient);
        }

        reportedState = current;
    }
}
