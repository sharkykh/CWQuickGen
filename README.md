# CWQuickGen

CodeWalker Quick Generator for Audio Occlusion XML files.  
**This is a Proof-of-Concept (PoC).**  

## What is this?
This program can generate `.rel` and `.ymt` files from `.rel.xml` and `.ymt.pso.xml` files respectively, by directly using CodeWalker's DLLs (without having to open CodeWalker).  
  
Haven't coded in C# in a while, so the code might not be great.

## Basic usage
```
.\CWQuickGen.exe {rel,ymt} {target} {sources}
```

On the first run it will ask for the path to CodeWalker's folder.  
Tested with CodeWalker version `CodeWalker30_dev33`.

### Arguments
Position|Description
:------:|:----------
1       |File type to search for: `rel` OR `ymt`
2       |Target path - where `*.rel`/`*.ymt` files will get created
3       |Sources path - where `*.rel.xml`/`*.ymt.pso.xml` files are located
