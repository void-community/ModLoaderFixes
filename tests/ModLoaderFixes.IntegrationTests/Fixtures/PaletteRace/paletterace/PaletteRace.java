package paletterace;

import java.lang.invoke.MethodHandle;
import java.lang.invoke.MethodHandles;
import java.lang.reflect.Constructor;
import java.lang.reflect.Field;
import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import java.util.Arrays;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Supplier;

/**
 * Sets blocks of new kinds in the real PalettedContainer of a Minecraft release on one thread while another reads the same blocks, as generation and the
 * light engine do. The release's names come in system properties: container, its three-int get and four-argument set, the strategy of a section's blocks
 * with the field (strategyField, up to 1.21.8) or the factory (strategyFactory, from 1.21.9) that gives it, the interface of the kinds' ids (idMap), and the
 * exception for a missing palette entry (missing). Each round is one meeting: the reader is reading the container's blocks before the writer sets the first.
 */
public final class PaletteRace {
    private static final int KINDS = 16;

    private PaletteRace() {
    }

    public static void main(String[] arguments) throws Throwable {
        int rounds = Integer.getInteger("rounds", 20000);
        Class<?> containerClass = Class.forName(System.getProperty("container"));
        Class<?> idMapClass = Class.forName(System.getProperty("idMap"));
        Class<?> strategyClass = Class.forName(System.getProperty("strategy"));
        String missing = System.getProperty("missing");
        String[] kinds = new String[KINDS];

        for (int index = 0; index < KINDS; index++)
            kinds[index] = "kind" + index;

        Object idMap = Proxy.newProxyInstance(containerClass.getClassLoader(), new Class<?>[] { idMapClass }, ids(kinds));
        Supplier<Object> containers = containers(containerClass, idMapClass, strategyClass, idMap, kinds[0]);
        Method setMethod = containerClass.getDeclaredMethod(System.getProperty("set"), int.class, int.class, int.class, Object.class);
        Method getMethod = containerClass.getDeclaredMethod(System.getProperty("get"), int.class, int.class, int.class);

        setMethod.setAccessible(true);
        getMethod.setAccessible(true);

        MethodHandle set = MethodHandles.lookup().unreflect(setMethod);
        MethodHandle get = MethodHandles.lookup().unreflect(getMethod);
        long reads = 0;

        for (int round = 1; round <= rounds; round++) {
            Object container = containers.get();

            // Every block is of kind 1 before the threads meet, and the writer then sets the block at each index to the kind of that index.
            for (int index = 0; index < KINDS; index++)
                set.invoke(container, index, 0, 0, (Object) kinds[1]);

            AtomicBoolean done = new AtomicBoolean();
            AtomicReference<Throwable> failure = new AtomicReference<Throwable>();
            CountDownLatch reading = new CountDownLatch(1);
            long[] count = new long[1];

            Thread reader = new Thread(() -> {
                try {
                    while (!done.get()) {
                        for (int index = 2; index < KINDS; index++) {
                            Object read = get.invoke(container, index, 0, 0);

                            count[0]++;

                            if (read != kinds[1] && read != kinds[index])
                                throw new IllegalStateException("WRONG block: read " + read + " where " + kinds[1] + " or " + kinds[index] + " is");
                        }

                        reading.countDown();
                    }
                } catch (Throwable thrown) {
                    failure.set(thrown);
                    reading.countDown();
                }
            }, "light");

            reader.start();
            reading.await();

            for (int index = 2; index < KINDS; index++)
                set.invoke(container, index, 0, 0, (Object) kinds[index]);

            done.set(true);
            reader.join();
            reads += count[0];

            Throwable thrown = failure.get();

            if (thrown != null) {
                String message = String.valueOf(thrown.getMessage());
                boolean missed = thrown.getClass().getName().equals(missing) || message.startsWith("WRONG");

                System.out.println((missed ? "MISSED a block" : "FAILED") + " in round " + round + " after " + reads + " reads: " + thrown.getClass().getName() + ": " + message);
                System.exit(missed ? 1 : 2);
            }
        }

        System.out.println("READ every block in " + rounds + " rounds");
    }

    // The container of a section's blocks, which takes the ids of the kinds, the kind it starts with and the strategy; from 1.21.9 the strategy holds the ids.
    private static Supplier<Object> containers(Class<?> containerClass, Class<?> idMapClass, Class<?> strategyClass, Object idMap, Object first) throws ReflectiveOperationException {
        String field = System.getProperty("strategyField", "");

        if (!field.isEmpty()) {
            Field strategyField = strategyClass.getDeclaredField(field);

            strategyField.setAccessible(true);

            Object strategy = strategyField.get(null);
            Constructor<?> constructor = containerClass.getConstructor(idMapClass, Object.class, strategyClass);

            return () -> create(constructor, idMap, first, strategy);
        }

        Method factory = strategyClass.getDeclaredMethod(System.getProperty("strategyFactory"), idMapClass);

        factory.setAccessible(true);

        Object strategy = factory.invoke(null, idMap);
        Constructor<?> constructor = containerClass.getConstructor(Object.class, strategyClass);

        return () -> create(constructor, first, strategy);
    }

    private static Object create(Constructor<?> constructor, Object... arguments) {
        try {
            return constructor.newInstance(arguments);
        } catch (ReflectiveOperationException failed) {
            throw new IllegalStateException(failed);
        }
    }

    // The ids of the kinds, as the game's registry of block states gives them: a kind's id is its place among the kinds.
    private static InvocationHandler ids(String[] kinds) {
        return (proxy, method, parameters) -> {
            Class<?>[] types = method.getParameterTypes();

            if (method.isDefault())
                return InvocationHandler.invokeDefault(proxy, method, parameters);
            if (types.length == 1 && types[0] == Object.class && method.getReturnType() == int.class)
                return Arrays.asList(kinds).indexOf(parameters[0]);
            if (types.length == 1 && types[0] == int.class && method.getReturnType() == Object.class)
                return (int) parameters[0] >= 0 && (int) parameters[0] < kinds.length ? kinds[(int) parameters[0]] : null;
            if (method.getName().equals("hashCode"))
                return System.identityHashCode(proxy);
            if (method.getName().equals("equals"))
                return proxy == parameters[0];
            if (method.getName().equals("iterator"))
                return Arrays.asList(kinds).iterator();
            if (types.length == 0 && method.getReturnType() == int.class)
                return kinds.length;

            return "kinds";
        };
    }
}
