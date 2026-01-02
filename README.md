# ParticleEditor

A plugin for Space Engineers game to allow modders to develop particles much easier.

![Image of its UI](https://i.imgur.com/EYtPgrc.png)


## Install:
Pick one:

### Standalone way
- Download `ParticleEditor.zip` from the [Releases](https://github.com/THDigi/ParticleEditor/releases) page
- Extract both `ParticleEditor.dll` and `ParticleEditorLauncher.exe` in your game's `Bin64` folder.
- Start `ParticleEditorLauncher.exe` which will launch the game with the ParticleEditor.dll automatically loaded.

### Third party plugin launcher way
Find a plugin launcher that you can trust. It most likely has this plugin in its list (as a lot of them inherited the [discontinued PluginLoader](https://github.com/sepluginloader/PluginLoader)'s list).  
I cannot recomend any particular launcher though.

If your chosen launcher doesn't have this plugin in its list you can either ask them to add it, or if it allows you to bring your own .dlls then you can download `ParticleEditor.zip` from the [Releases](https://github.com/THDigi/ParticleEditor/releases) page and extract only the `ParticleEditor.dll`. Refer to the launcher's documentation on how to load a .dll file.

The game itself used to support plugins but as of SE v202 the `-plugin` launch arg was (understandably) removed, [official statement](https://www.spaceengineersgame.com/plugins/).

## Usage: 
In the game, load any offline world.
Once spawned ensure you're first person view and press `Shift+F` to open the editor which will now ask you to either pick an existing particle or create a blank one.

From here onwards you'll have only the tooltips from the program itself, if anything is unclear then hover it!  
For questions about this or particles in general, ask in the [Keen discord server](https://discord.gg/keenswh)'s `#modding-art-sbc` channel.

Feedback and bugreports are welcome in the [Issues](https://github.com/THDigi/ParticleEditor/issues) tab.
