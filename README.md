T4Toolbox-V10-Revised
=====================

T4 Toolbox from Codeplex, but for Visual Studio 2012 and thus based on version 10.
I wanted to see how the Directive Processor worked, but the VS2010 version would not open or compile in VS2012.
I have created a VSIX to easily load the T4 Toolbox to Extensions, and in doing so removed the dual AppDomain feature of the Codeplex version.
I have also changed the DTEDirectiveProcessor to a static class renamed to OutputProcessor.
It was intended to be quick and dirty. It might not be reliable in all circumstances.
May 2013

