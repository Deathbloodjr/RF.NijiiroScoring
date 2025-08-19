# RF.NijiiroScoring
 A Rhythm Festival mod to change from the current Gen3 scoring to Nijiiro (Gen4) scoring.


   
 This mod will overwrite all of your highscores, replacing the old Gen3 score with Nijiro's score. It will keep your actual performance though (goods, oks, etc).\
 Just to be safe, make sure you backup your save from the tkdat folder in the Taiko directory.

 This mod is automatically disabled in online matches.

 It may take a bit of time to calculate all the proper point values for every song on first launch. Afterward, it loads all the values from a local json file.\
 If you find any values that are incorrect, you can manually adjust this json file to fix them.\
 Most oni/ura chart values should only be off by about 10 points at most, while easy through hard may be off by a bit more.
 
# Requirements
 Visual Studio 2022 or newer\
 Taiko no Tatsujin: Rhythm Festival
 
 
# Build
 Install [BepInEx 6.0.0-pre.2](https://github.com/BepInEx/BepInEx/releases/tag/v6.0.0-pre.2) into your Rhythm Festival directory and launch the game.\
 This will generate all the dummy dlls in the interop folder that will be used as references.\
 Make sure you install the Unity.IL2CPP-win-x64 version.\
 Newer versions of BepInEx could have breaking API changes until the first stable v6 release, so those are not recommended at this time.
 
 Attempt to build the project, or copy the .csproj.user file from the Resources file to the same directory as the .csproj file.\
 Edit the .csproj.user file and place your Rhythm Festival file location in the "GameDir" variable.\
 Download or build the [SaveProfileManager](https://github.com/Deathbloodjr/RF.SaveProfileManager) mod, and place that dll full path in SaveProfileManagerPath.

Add BepInEx as a nuget package source (https://nuget.bepinex.dev/v3/index.json)


# Links 
 [My Other Rhythm Festival Mods](https://docs.google.com/spreadsheets/d/1xY_WANKpkE-bKQwPG4UApcrJUG5trrNrbycJQSOia0c)\
 [My Taiko Discord Server](https://discord.gg/6Bjf2xP)
 
