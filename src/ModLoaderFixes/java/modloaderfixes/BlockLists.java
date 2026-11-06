package modloaderfixes;

import java.io.BufferedReader;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import java.util.stream.Stream;

/**
 * Reads Mojang's list of blocked servers for patchy so that a read that fails part way is reported as an IOException. patchy reads the list from
 * sessionserver.mojang.com with BufferedReader.lines() inside a try that catches IOException, so that a list it can't fetch is no list. lines() reports a failed
 * read, such as a connection reset in the middle of the answer, as an UncheckedIOException instead, which escapes the static initializer that asks for the list
 * as an ExceptionInInitializerError and kills the thread connecting to a server. This reads every line before patchy sees any, so a failed read throws the
 * IOException that patchy catches. It names no game type.
 */
public final class BlockLists {
    private BlockLists() {
    }

    /** Stands in for reader.lines() in patchy: the lines of the whole list, read before they are returned. */
    public static Stream<String> lines(BufferedReader reader) throws IOException {
        List<String> lines = new ArrayList<String>();
        String line;

        while ((line = reader.readLine()) != null)
            lines.add(line);

        return lines.stream();
    }
}
