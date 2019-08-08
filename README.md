<h1 align=center>Nigiri</h1>

# Features.
* 2D, Single Pass Stereo Rendering
* Runs on Unity version 2018.4 ONLY (Will update, time permitting.)
* Depth buffer based voxelisation is less GPU intensive that other methods.
* Supports adding GI to 3rd party plugins that otherwise don't expose geometary to Unity.

An almost-dynamic voxel-based global illumination system for Unity.

<p align="center" style="display: inline-block;">
  <img height="256px" width="597px" src="https://i.imgur.com/qZMa7px.jpg">
</p>

# Installation
Check the [Releases](https://github.com/ninlilizi/Nigiri/releases) section above to download a version of Nigiri that is a simple .unitypackage file which is ready for you to import into your project. 

You can also click the "Clone or Download" button and select "Download Zip", then extract the contents to "Assets/Plugins/SEGI" in your project to test out the latest unreleased versions of SEGI.

Some of the files are store using LFS. For now please checkout using git commandline or github desktop app to ensure git lfs is initiated and can download those files.


* Quick start instructions pending in this space...


# Using Nigiri
Ensure your player settings are set to linear color space. 
Create or select a camera. Make sure the camera is set to deferred rendering path.
Add Nigiri component to the camera.
Tweak your almost realtime gi.


# Community
If you need some help, feel free to ask any questions in the [Discord](https://discord.gg/QQspUgm) on Unity forums.

# Licence
 Nigiri as a whole is CC BY-NC 4.0. With select attributions as stated below being MIT.

# Attributions
* Sonic Ether for SEGI (https://github.com/sonicether/SEGI)<br>
* Cat Like Coding for FXAA (https://catlikecoding.com/unity/tutorials/advanced-rendering/fxaa/)<br>
* Cat Like Coding for Bulk of Spherical Harmics shader (https://catlikecoding.com/unity/tutorials/rendering/part-20/)<br>
* keijiro for MiniEngineAO (https://github.com/keijiro/MiniEngineAO)<br>
* keijiro for Gaussian Blur(https://github.com/keijiro)
* For depth based voxelization (https://github.com/parikshit6321/PVGI/blob/master/Assets/ProgressiveVoxelizedGI/Shaders/VoxelGridEntry.compute)
* This list, plus MIT declarations will be correct and actually acurate before this goes public!
