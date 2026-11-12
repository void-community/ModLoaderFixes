package sun.nio.ch;

import java.nio.channels.FileChannel;

/** The JDK's FileChannel of the default file system, of which the helpers use the switch that Java 9 and later have to keep an interrupt from closing it. */
public abstract class FileChannelImpl extends FileChannel {
    public void setUninterruptible() {
    }
}
