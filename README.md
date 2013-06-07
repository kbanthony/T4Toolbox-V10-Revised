T4Toolbox-V10-Revised
=====================

T4 Toolbox from Codeplex, but for Visual Studio 2012 and thus based on version 10.
I wanted to see how the Directive Processor worked, but the VS2010 version would not open or compile in VS2012.
May 2013

Version 1.0.2 7-Jun-2013
Added RenderToFileIfNotExists(file) in preference to PreserveExistingFile, as it's easy to make a mistake with a separate property.
Internally, changed MSBuild Api to .Net 4.0 code.
Another test program provided, TestT4Debugging10R, which demonstrates Custom partial classes and Generated Classes

Version 1.0.0
AppDomain switching removed because the Toolbox DLL needs to be in a common place for the serialisation/deserialisation to work.
DTEDirectiveProcessor changed to a static class called OutputProcesser, as a quick way to make a code change.
