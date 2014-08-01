#Deobfuscate-7dtd
================

Deobfuscate-7dtd is a module-based deobfuscator for [7 Days to Die](http://7daystodie.com/).

Trying to make modding much easier for the community, flexibility is a main aim so everybody can contribute 
without coming into conflict with existing code.

##Usage
Drag-and-drop the original Assembly-CSharp.dll in the "7 Days To Die\7DaysToDie_Data\Managed" folder into Deobfuscate-Main.exe.
A new Assembly-CSharp.deobf.dll will be put into that folder.

##Internals
Deobfuscate-Main loads Assembly-CSharp.dll into an [AssemblyDefinition](https://github.com/jbevain/cecil/blob/master/Mono.Cecil/AssemblyDefinition.cs) using Mono.Cecil.
Then, each module listed in patchers/patchers.xml will be called in order of the listing.
Here's an example :
> <?xml version="1.0"?>
> <Patchers>
>     <Patcher file="NamePatcher.dll" class="NamePatcher.NamePatcher"/>
> </Patchers>

The modules make usage of [Mono.Cecil](https://github.com/jbevain/cecil/tree/master/Mono.Cecil), additionaly Mono.Cecil.Rocks is provided.

Each module's main class should provide these methods :
> public static string getName()
> public static string[] getAuthors()
> public static void Patch(DeobfuscateMain.Logger logger, AssemblyDefinition asmCSharp, AssemblyDefinition __reserved)

##Compiling
You can use [MonoDevelop](http://monodevelop.com/Download) or Microsoft Visual Studio to open the .sln file.
To compile, you need to copy Mono.Cecil.dll and Mono.Cecil.Rocks.dll (which you can find in one of the releases)
 into the path of the project.

##Contributing
If you want to contribute, please write me a private message in the 7 Days to Die forums.
