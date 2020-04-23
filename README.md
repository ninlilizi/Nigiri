*Please, note. This project is considered a work in progress and is still evolving over time.*
<h1 align=center>Nigiri</h1>
An almost-dynamic voxel-based global illumination system for Unity.
<p align="center" style="display: inline-block;">
  <img height="256px" width="597px" src="https://i.imgur.com/qZMa7px.jpg">
</p>

# Features.
* 2D, Single Pass Stereo Rendering
* Runs on Unity version 2018.4 ONLY (Will update, time permitting.)
* Depth buffer based voxelisation is less GPU intensive that other methods.
* Supports adding GI to 3rd party plugins that otherwise don't expose geometary to Unity.

# Roadmap
https://github.com/ninlilizi/Nigiri/wiki/Doc:-Roadmap

# Installation
Check the [Releases](https://github.com/ninlilizi/Nigiri/releases) section above to download a version of Nigiri that is a simple .unitypackage file which is ready for you to import into your project. 

You can also click the "Clone or Download" button and select "Download Zip", then extract the contents to "Assets/Plugins/Nigiri" in your project to test out the latest unreleased versions of Nigiri.

Some of the files are store using LFS and some resources in SubModules. For now please checkout using git commandline or github desktop app to ensure git lfs is initiated and can download those files.

#### To clone using Git command line
```
git clone --recurse-submodules git@github.com:ninlilizi/Nigiri.git
```
* Quick start instructions pending in this space...


# Using Nigiri
Ensure your player settings are set to linear color space. 
Create or select a camera. Make sure the camera is set to deferred rendering path.
Add Nigiri component to the camera.
Tweak your almost realtime gi.


# Community
If you need some help, feel free to ask any questions in the [Discord](https://discord.gg/QQspUgm).   
A Unity Forum thread will also be available at a later time.

# Licence
All code contributions and inclusions are to the best of my knowledge, MIT.

Nigiri as a whole, is also provided under standard MIT licence terms. With a simple request, that as I am a disabled person, struggling to attain suitable employment due to my health issues making a 9-5 commitment unviable. That if you profit from this work, donations are appreciated, but offers of actual paid work would make so much difference to my life. I'm dying of exposure out here!</br>
[Link to my resume](https://nkli.net/Files/Abigail%20Hocking%20-%20Resume.pdf)</br>

* Special mention to the following, for testing and introducing me to a world of new ideas and techniques:</br>
Sonikku A, neoshaman, [ddutchie](https://github.com/ddutchie), jefferytitan, shinyclef

[Major contributions for specific components detailed here](https://github.com/ninlilizi/Nigiri/blob/master/LICENSE)</br>


# Attributions
* [Sonic Ether for SEGI](https://github.com/sonicether/SEGI)<br>
* [Cat Like Coding for FXAA](https://catlikecoding.com/unity/tutorials/advanced-rendering/fxaa/)<br>
* [Cat Like Coding for Bulk of Spherical Harmics shader](https://catlikecoding.com/unity/tutorials/rendering/part-20/)<br>
* [keijiro for MiniEngineAO](https://github.com/keijiro/MiniEngineAO)<br>
* [keijiro for Gaussian Blur](https://github.com/keijiro)
* [keijiro for Tonemapping](https://github.com/keijiro/ColorSuite)
* [For depth based voxelization](https://github.com/parikshit6321/PVGI/blob/master/Assets/ProgressiveVoxelizedGI/Shaders/VoxelGridEntry.compute)
