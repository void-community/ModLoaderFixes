package modloaderfixes;

import java.io.IOException;
import java.nio.ByteBuffer;
import java.nio.MappedByteBuffer;
import java.nio.channels.ClosedByInterruptException;
import java.nio.channels.ClosedChannelException;
import java.nio.channels.FileChannel;
import java.nio.channels.FileLock;
import java.nio.channels.ReadableByteChannel;
import java.nio.channels.SeekableByteChannel;
import java.nio.channels.WritableByteChannel;
import java.nio.file.Files;
import java.nio.file.OpenOption;
import java.nio.file.Path;
import java.util.Arrays;
import java.util.HashSet;
import java.util.Set;

import sun.nio.ch.FileChannelImpl;

/**
 * Keeps a zip file system's jar readable when a thread reading it is interrupted. The JDK's ZipFileSystem reads every entry through one FileChannel, which an
 * interrupt during a read closes for every thread, so Forge's module loader, which reads each jar through one, finds nothing more in it and fails each class it
 * hadn't loaded with NoClassDefFoundError. The zip file system's constructor opens its channel here instead. On Java 9 and later the JDK's own channel comes back
 * uninterruptible, as SecureJarHandler from 2.1.9 makes it itself, casting it to FileChannelImpl, so its cast still holds; the agent exports sun.nio.ch to this
 * class for it. Java 8 has no such switch, and there a FileChannel of the default file system comes back wrapped: a read that finds the channel closed by an
 * interrupt opens it again and reads again, except on the interrupted thread, which gets its ClosedByInterruptException as before. Channels of other file
 * systems, such as a jar inside another jar, come back as they are. It names no game type.
 */
public final class ZipChannels {
    private ZipChannels() {
    }

    /** Stands in for Files.newByteChannel(path, options) in ZipFileSystem's constructor. */
    public static SeekableByteChannel newByteChannel(Path path, OpenOption... options) throws IOException {
        SeekableByteChannel channel = Files.newByteChannel(path, options);

        if (!(channel instanceof FileChannel))
            return channel;

        try {
            if (channel instanceof FileChannelImpl) {
                ((FileChannelImpl) channel).setUninterruptible();
                return channel;
            }
        } catch (NoSuchMethodError java8) {
            return new Reopening(path, options, (FileChannel) channel);
        } catch (IllegalAccessError unexported) {
            // Without the export the JDK's channel stays as it is, which the loaders that cast it expect.
            return channel;
        }

        return new Reopening(path, options, (FileChannel) channel);
    }

    /** A FileChannel that opens its file again when an interrupt on another thread closed it, and keeps its own position for the reads that use one. */
    private static final class Reopening extends FileChannel {
        private final Path path;
        private final Set<OpenOption> options;
        private volatile FileChannel delegate;
        private long position;

        Reopening(Path path, OpenOption[] options, FileChannel delegate) {
            this.path = path;
            this.options = new HashSet<OpenOption>(Arrays.asList(options));
            this.delegate = delegate;
        }

        @Override
        public int read(ByteBuffer destination) throws IOException {
            synchronized (this) {
                int read = read(destination, position);

                if (read > 0)
                    position += read;

                return read;
            }
        }

        @Override
        public int read(ByteBuffer destination, long at) throws IOException {
            int start = destination.position();

            while (true) {
                FileChannel channel = delegate;

                try {
                    return channel.read(destination, at);
                } catch (ClosedByInterruptException exception) {
                    reopen(channel);
                    throw exception;
                } catch (ClosedChannelException exception) {
                    // AsynchronousCloseException is one too: another thread's interrupt closed the channel during this read, or before it.
                    if (!isOpen())
                        throw exception;

                    reopen(channel);
                    destination.position(start);
                }
            }
        }

        @Override
        public long read(ByteBuffer[] destinations, int offset, int length) throws IOException {
            long total = 0;

            for (int index = offset; index < offset + length; index++) {
                int read = read(destinations[index]);

                if (read < 0)
                    return total == 0 ? -1 : total;

                total += read;

                if (destinations[index].hasRemaining())
                    break;
            }

            return total;
        }

        @Override
        public synchronized long position() {
            return position;
        }

        @Override
        public synchronized FileChannel position(long newPosition) {
            position = newPosition;
            return this;
        }

        @Override
        public long size() throws IOException {
            while (true) {
                FileChannel channel = delegate;

                try {
                    return channel.size();
                } catch (ClosedByInterruptException exception) {
                    reopen(channel);
                    throw exception;
                } catch (ClosedChannelException exception) {
                    if (!isOpen())
                        throw exception;

                    reopen(channel);
                }
            }
        }

        @Override
        public int write(ByteBuffer source) throws IOException {
            synchronized (this) {
                int written = delegate.write(source, position);

                position += written;
                return written;
            }
        }

        @Override
        public long write(ByteBuffer[] sources, int offset, int length) throws IOException {
            long total = 0;

            for (int index = offset; index < offset + length; index++)
                total += write(sources[index]);

            return total;
        }

        @Override
        public int write(ByteBuffer source, long at) throws IOException {
            return delegate.write(source, at);
        }

        @Override
        public FileChannel truncate(long size) throws IOException {
            delegate.truncate(size);
            return this;
        }

        @Override
        public void force(boolean metaData) throws IOException {
            delegate.force(metaData);
        }

        @Override
        public long transferTo(long at, long count, WritableByteChannel target) throws IOException {
            return delegate.transferTo(at, count, target);
        }

        @Override
        public long transferFrom(ReadableByteChannel source, long at, long count) throws IOException {
            return delegate.transferFrom(source, at, count);
        }

        @Override
        public MappedByteBuffer map(MapMode mode, long at, long size) throws IOException {
            return delegate.map(mode, at, size);
        }

        @Override
        public FileLock lock(long at, long size, boolean shared) throws IOException {
            return delegate.lock(at, size, shared);
        }

        @Override
        public FileLock tryLock(long at, long size, boolean shared) throws IOException {
            return delegate.tryLock(at, size, shared);
        }

        @Override
        protected void implCloseChannel() throws IOException {
            delegate.close();
        }

        // Opens the file again in place of a channel an interrupt closed, once, whichever thread finds it closed first.
        private synchronized void reopen(FileChannel closed) throws IOException {
            if (delegate == closed && isOpen())
                delegate = FileChannel.open(path, options);
        }
    }
}
