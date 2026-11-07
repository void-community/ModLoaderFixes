# 🔧 ModLoaderFixes

[![CI](https://img.shields.io/github/actions/workflow/status/void-community/ModLoaderFixes/runnable-commit.yml?branch=main&style=for-the-badge)](https://github.com/void-community/ModLoaderFixes/actions/workflows/runnable-commit.yml)
[![Release](https://img.shields.io/github/v/release/void-community/ModLoaderFixes?style=for-the-badge)](https://github.com/void-community/ModLoaderFixes/releases/latest)
[![License](https://img.shields.io/github/license/void-community/ModLoaderFixes?style=for-the-badge)](LICENSE)

**Fix Forge, NeoForge and Minecraft servers and clients that break on current Java, under load or behind a proxy.** ☕

A native Java agent that repairs these as the game loads. It changes no game files and needs no .NET. ✨

## 🩹 What it fixes

**Forge 1.7.2 servers stop before Minecraft loads on Java 8u20 and later:**

```text
[main/INFO] [LaunchWrapper]: Calling tweak class cpw.mods.fml.common.launcher.FMLInjectionAndSortingTweaker
[main/ERROR] [LaunchWrapper]: Unable to launch
java.util.ConcurrentModificationException
	at java.util.ArrayList$Itr.checkForComodification(ArrayList.java:911) ~[?:1.8.0_504]
	at java.util.ArrayList$Itr.remove(ArrayList.java:875) ~[?:1.8.0_504]
	at net.minecraft.launchwrapper.Launch.launch(Launch.java:117) [launchwrapper-1.9.jar:?]
```

**Forge 1.7.2 and 1.7.10, PaperSpigot 1.7.10 and vanilla 1.7.2 to 1.7.10 servers now and then never answer a login**, and the client or proxy waits until it gives up:

```text
[Netty IO #0/DEBUG] [net.minecraft.network.NetworkManager]: Set listener of net.minecraft.network.NetworkManager@1ab07dd4 to net.minecraft.server.network.NetHandlerLoginServer@2bcfb381
[Netty IO #0/DEBUG] [net.minecraft.util.MessageDeserializer]:  IN: [LOGIN:0] net.minecraft.network.login.client.C00PacketLoginStart[]
```

**Forge 1.7.2 and 1.7.10 clients sometimes drop their connection as they join, while the game is busy:**

```text
[Netty Client IO #0] [ERROR] java.lang.RuntimeException: Timeout waiting for client thread to catch up!
at cpw.mods.fml.client.FMLClientHandler.waitForPlayClient(FMLClientHandler.java:843)
at cpw.mods.fml.common.FMLCommonHandler.waitForPlayClient(FMLCommonHandler.java:584)
at cpw.mods.fml.common.network.handshake.NetworkDispatcher.clientListenForServerHandshake(NetworkDispatcher.java:154)
at cpw.mods.fml.common.network.handshake.FMLHandshakeClientState$1.accept(FMLHandshakeClientState.java:33)
at cpw.mods.fml.common.network.handshake.FMLHandshakeClientState$1.accept(FMLHandshakeClientState.java:27)
at cpw.mods.fml.common.network.handshake.HandshakeMessageHandler.userEventTriggered(HandshakeMessageHandler.java:40)
...
at cpw.mods.fml.common.network.handshake.NetworkDispatcher.handlerAdded(NetworkDispatcher.java:132)
...
at cpw.mods.fml.common.network.handshake.NetworkDispatcher.insertIntoChannel(NetworkDispatcher.java:119)
at cpw.mods.fml.common.network.handshake.NetworkDispatcher.clientToServerHandshake(NetworkDispatcher.java:124)
at cpw.mods.fml.common.network.internal.FMLNetworkHandler.fmlClientHandshake(FMLNetworkHandler.java:66)
at net.minecraft.client.network.NetHandlerLoginClient.func_147390_a(NetHandlerLoginClient.java:97)
```

**Forge 1.7.2 and 1.7.10 clients now and then drop a connection as they join a server again**, when the server answers before the game has set the connection up:

```text
[Netty Client IO #1/ERROR]: ChannelRegistrationHandler exception
java.lang.ClassCastException: Cannot cast net.minecraft.client.network.NetHandlerLoginClient to net.minecraft.client.network.NetHandlerPlayClient
	at java.lang.Class.cast(Class.java:3369)
	at cpw.mods.fml.common.network.FMLNetworkEvent$CustomPacketRegistrationEvent.<init>(FMLNetworkEvent.java:93)
	at cpw.mods.fml.client.FMLClientHandler.fireNetRegistrationEvent(FMLClientHandler.java:887)
```

**Forge and vanilla clients from 1.7 to 1.12.2 sometimes crash with their sound system when its resources reload while it still loads**, as Forge's second reload at startup can:

```text
[Thread-8] java.lang.IllegalStateException: Only one OpenAL context may be instantiated at any one time.
	at org.lwjgl.openal.AL.create(AL.java:113)
	at paulscode.sound.libraries.LibraryLWJGLOpenAL.init(LibraryLWJGLOpenAL.java:164)
[Sound Library Loader]         SoundSystem did not load after 30 seconds.
[Thread-7] java.lang.UnsatisfiedLinkError: org.lwjgl.openal.AL10.nalGetSourcei(II)I
```

**Forge 1.7.2 and 1.7.10 and vanilla 1.7.2 to 1.7.10 clients sometimes drop their connection a few milliseconds after they open it:**

```text
[Client thread] [INFO] Connecting to localhost, 25565
Internal Exception: io.netty.handler.codec.EncoderException: java.io.IOException: Can't serialize unregistered packet
```

**Forge 1.12 to 1.12.2 and 1.13.2 and vanilla 1.8 to 1.14.3 clients sometimes crash as they start to connect:**

```text
[Client thread] [INFO] Connecting to localhost, 25565
Description: Ticking screen
java.lang.NullPointerException: Ticking screen
	at em$a.a(SourceFile:387)
	at em.m(SourceFile:220)
	at em.a(SourceFile:229)
	at beg.e(SourceFile:91)
```

**Minecraft clients from 1.12 sometimes stay on their connecting screen forever when Mojang's list of blocked servers is cut off**, and every later connection of the game fails too:

```text
[Server Connector #1/ERROR]: Caught previously unhandled exception :
java.lang.ExceptionInInitializerError: null
Caused by: java.io.UncheckedIOException: java.net.SocketException: Connection reset
	at java.io.BufferedReader$1.hasNext(BufferedReader.java:574) ~[?:?]
	at com.mojang.patchy.MojangBlockListSupplier.createBlockList(MojangBlockListSupplier.java:25) ~[patchy-2.2.10.jar:?]
```

**Forge 1.12 servers drop players at login under load:**

```text
[Server thread/INFO] [minecraft/NetHandlerPlayServer]: Player lost connection: Internal Exception: io.netty.handler.codec.EncoderException: java.io.IOException: Can't serialize unregistered packet
```

**Forge 1.12 and 1.12.1 clients crash when a proxy such as BungeeCord or Void moves them to another server:**

```text
[Netty Epoll Client IO #1] [INFO] Injecting existing registry data into this client instance
[main] [FATAL] net.minecraft.util.ReportedException: Ticking entity
	at net.minecraft.world.World.func_72939_s(World.java:1766)
	at net.minecraft.client.Minecraft.func_71407_l(Minecraft.java:1841)
Caused by: java.lang.NullPointerException
	at net.minecraft.entity.player.EntityPlayer.func_192028_j(EntityPlayer.java:589)
	at net.minecraft.entity.player.EntityPlayer.func_70636_d(EntityPlayer.java:574)
```

**Forge 1.13.2 to 1.20.1 and NeoForge 1.20.1 servers drop players at login under load:**

```text
[Server thread/INFO] [minecraft/ServerLoginNetHandler]: com.mojang.authlib.GameProfile@6ac532df[id=<null>,name=Player,properties={},legacy=false] (/172.17.0.1:43658) lost connection: Internal Exception: java.util.ConcurrentModificationException
```

**NeoForge 1.20.2 and 1.20.3 servers, and the first betas for 1.20.4, now and then end a player's login handshake before its last answer and drop the player:**

```text
[Server thread/DEBUG] [net.neoforged.neoforge.network.HandshakeHandler/FMLHANDSHAKE]: Handshake complete!
[Netty Epoll Server IO #4/DEBUG] [net.minecraft.network.Connection/]: Failed to sent packet
io.netty.handler.codec.DecoderException: Badly compressed packet - size of 2 is below server threshold of 256
	at net.minecraft.network.CompressionDecoder.decode(CompressionDecoder.java:45)
```

**Forge 1.13.2 to 1.20.1 and NeoForge 1.20.1 to 1.20.3 servers, and the first NeoForge betas for 1.20.4, now and then drop a player just as its login handshake completes:**

```text
[Server thread/DEBUG] [net.minecraftforge.network.HandshakeHandler]: Handshake complete!
[Netty Epoll Server IO #2/DEBUG] [net.minecraft.network.Connection]: Failed to sent packet
java.lang.NullPointerException: null
	at net.minecraftforge.network.HandshakeHandler.lambda$biConsumerFor$1(HandshakeHandler.java:154)
	at java.util.function.BiConsumer.lambda$andThen$0(Unknown Source)
```

**Forge 1.13.2 to 1.15.2 now and then crashes while it loads a world or its mods:**

```text
java.lang.NullPointerException: null
	at cpw.mods.modlauncher.TransformerClassWriter.getCommonSuperClass(TransformerClassWriter.java:102) ~[modlauncher-2.1.5.jar:?] {}
	at org.objectweb.asm.SymbolTable.addMergedType(SymbolTable.java:1209) ~[asm-6.2.jar:?] {}
```

**Forge 1.13.2 to 1.19.4 sometimes fails its first start while it corrects `fml.toml`, or writes keys twice:**

```text
com.electronwill.nightconfig.core.io.ParsingException: Invalid TOML data: entry "[disableOptimizedDFU]" defined twice in its table.
Exception in thread "main" java.util.ConcurrentModificationException
	at com.electronwill.nightconfig.core.ConfigSpec.correct(ConfigSpec.java:655)
	at net.minecraftforge.fml.loading.FMLConfig.loadFrom(FMLConfig.java:54)
```

**Forge 1.13.2 to 1.21.4 and NeoForge 1.20.1 sometimes crash while they load mods:**

```text
java.lang.ClassCastException: class java.util.HashMap$Node cannot be cast to class java.util.HashMap$TreeNode (java.util.HashMap$Node and java.util.HashMap$TreeNode are in module java.base of loader 'bootstrap')
	at java.util.HashSet.add(HashSet.java:221)
	at net.minecraftforge.registries.ObjectHolderRegistry.addHandler(ObjectHolderRegistry.java:64)
```

**Forge 1.14.2 and 1.14.3 clients sometimes hang at startup and never reach their menu.** A thread dump shows the client thread waiting in `EventListenerHelper.getListenerListInternal` while a `modloading-worker` thread holds EventBus's lock.

**Forge, NeoForge and vanilla clients from 1.14 to 1.20.4 sometimes crash the JVM as they leave a world while a sound streams:**

```text
# A fatal error has been detected by the Java Runtime Environment:
#  SIGSEGV (0xb) at pc=0x00007522e862e144
# Problematic frame:
# C  [liblwjgl_stb.so+0x2e144]
Current thread: JavaThread "Sound engine"
J  org.lwjgl.stb.STBVorbis.nstb_vorbis_decode_frame_pushdata
J  net.minecraft.client.audio.OggAudioStream.func_216460_a
```

**Forge, Paper, Spigot and vanilla 1.16 to 1.18.2 servers sometimes hang at startup and never finish loading their data packs.** The log stops after `Reloading ResourceManager`, and a thread dump shows the main thread waiting while the worker threads sit idle:

```text
"main" #1 ... WAITING (parking)
	at java.util.concurrent.CompletableFuture.get(CompletableFuture.java:1908)
	at net.minecraft.server.v1_16_R3.Main.main(Main.java:188)
```

**Forge and vanilla 1.7 clients, and Forge, NeoForge and vanilla clients from 1.13, drop the chat a server sends just before it disconnects them**, such as a proxy's explanation of a kick. 1.7 clients never show it. Clients from 1.13 drop it now and then, and from 1.14.4 they log `Ignoring packet due to disconnection` for it with debug logging.

**Forge and NeoForge 1.20.1 to 1.21.4 clients sometimes crash before the game's window opens:**

```text
java.lang.RuntimeException: java.util.concurrent.ExecutionException: java.lang.NullPointerException: Cannot invoke "java.lang.Boolean.booleanValue()" because the return value of "net.minecraftforge.fml.loading.FMLConfig$ConfigValue.getConfigValue(com.electronwill.nightconfig.core.file.CommentedFileConfig)" is null
	at net.minecraftforge.fml.earlydisplay.DisplayWindow.setupMinecraftWindow(DisplayWindow.java:527)
	at net.minecraftforge.fml.loading.ImmediateWindowHandler.setupMinecraftWindow(ImmediateWindowHandler.java:54)
```

**Forge clients from 1.20.1 and NeoForge 1.20.1 to 1.21.3 clients give up at startup when the game's window takes more than 10 seconds to open**, as it can on a busy machine or one that renders in software:

```text
[main/INFO]: Trying GL version 4.6
[pool-2-thread-1/ERROR]: ERROR DISPLAY
Failed to initialize the mod loading system and display.

Failure details:
Timed out trying to setup the Game Window.
```

**Forge and NeoForge clients from 1.20.6, and vanilla clients from 26.1, sometimes drop their connection as they join or as a proxy moves them to another server:**

```text
[Render thread] [WARN] Client disconnected with reason: Internal Exception: io.netty.handler.codec.EncoderException: Pipeline has no outbound protocol configured, can't process packet ServerboundChunkBatchReceivedPacket
[Netty Epoll IO #0] [ERROR] io.netty.handler.codec.EncoderException: Sending unknown packet 'serverbound/minecraft:chunk_batch_received'
```

## 🚀 Usage

Download the agent from the [**latest release**](https://github.com/void-community/ModLoaderFixes/releases/latest):

```sh
curl -fsSL -O https://github.com/void-community/ModLoaderFixes/releases/latest/download/modloaderfixes.jar
```

Add it to the Java arguments of the server or the launcher profile:

```sh
java -javaagent:modloaderfixes.jar -jar forge-1.12.2-14.23.5.2859.jar nogui
```

🐳 With [**itzg/minecraft-server**](https://github.com/itzg/docker-minecraft-server):

```sh
docker run -d -e EULA=TRUE -e TYPE=FORGE -e VERSION=1.12.2 \
  -e JVM_OPTS=-javaagent:/patcher/modloaderfixes.jar \
  -v "$PWD/modloaderfixes.jar:/patcher/modloaderfixes.jar:ro" \
  itzg/minecraft-server:java8
```

✅ The log shows a line for each fix as the agent applies it:

```text
ModLoaderFixes: patched net/minecraftforge/fml/common/network/handshake/NetworkDispatcher (serverInitiateHandshake()I: 1 invocation of net/minecraft/network/NetworkManager.func_150723_a redirected)
```

## 📦 Requirements

- 🐧 Linux x64 or ARM64 with glibc 2.17 or later: CentOS 7, Ubuntu 18.04, Debian 10 and newer, and every itzg image. One jar serves both.
- To skip the jar, pass `modloaderfixes-linux-x64.so` or `modloaderfixes-linux-arm64.so` from the release to `-agentpath` with an absolute path.
- 🧪 CI tests every commit: real Forge and NeoForge servers start with the agent, many with 200 players logging in at once, and the client fixes run against reproductions of their bugs.

## 📄 License

[**MIT**](LICENSE)
