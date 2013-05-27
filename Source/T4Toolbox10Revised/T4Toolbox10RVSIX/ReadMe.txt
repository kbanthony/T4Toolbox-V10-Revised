T4 Toolbox from version 10 on Codeplex, migrated to VS2012.
https://t4toolbox.codeplex.com/
Many thanks to the T4 Toolbox team.

AppDomain switching removed because the Toolbox DLL needs to be in a common place for the serialisation/deserialisation to work.
DTEDirectiveProcessor changed to a static class called OutputProcesser, as a quick way to make a code change.

Build:
The VSIX is configured to load into the Experimental Instance of VS2012.
Right-clicking the VSIX for the Debug menu will start this instance, where you can access the test solution included with the source archihve.
See https://github.com/jradxl/T4Toolbox-V10-Revised

Test:
In the Experimental Instance of VS2012 open the T4ToolboxV10RTest solution.
Use Tools > Extensions to check present of the VSIX, and read this file.
Transform the T4 template by Run Custom Tool

jradxl
May 2013
