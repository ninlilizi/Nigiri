<h1 align=center>Nigiri</h1>

# Features.
* 2D, Single Pass Stereo Rendering
* Runs on Unity version 2018.4 ONLY
* Depth buffer based voxelisation is less GPU intensive that other methods.
* Supports adding GI to 3rd party plugins that otherwise don't expose geometary to Unity.

An almost-dynamic voxel-based global illumination system for Unity.

<p align="center" style="display: inline-block;">
  <img height="256px" width="597px" src="https://i.imgur.com/qZMa7px.jpg">
</p>

# Installation
Check the [Releases](https://github.com/ninlilizi/SEGI/releases) section above to download a version of CKGI that is a simple .unitypackage file which is ready for you to import into your project. 

You can also click the "Clone or Download" button and select "Download Zip", then extract the contents to "Assets/Plugins/SEGI" in your project to test out the latest unreleased versions of SEGI.

Install Unity Post Processing v2. (https://github.com/Unity-Technologies/PostProcessing/wiki/Installation)

Then add SEGI as a PP2 effect.  (https://github.com/Unity-Technologies/PostProcessing/wiki/Quick-start)

Add 'SEGI_Sun_Light.cs' to your main directional light

Add 'SEGI_Follow_Transform.cs' to your player character

Please refer to the User Guide.pdf for usage instructions.

# Community
If you need some help, feel free to ask any questions in the [official thread](https://forum.unity.com/threads/segi-fully-dynamic-global-illumination.410310) on Unity forums.

# Licence
 Nigiri as a whole is CC BY-NC 4.0. With select attributions as stated below being MIT.

# Attributions
* Sonic Ether for SEGI (https://github.com/sonicether/SEGI)<br>
* Cat Like Coding for FXAA (https://catlikecoding.com/unity/tutorials/advanced-rendering/fxaa/)<br>
* Cat Like Coding for Bulk of Spherical Harmics shader (https://catlikecoding.com/unity/tutorials/rendering/part-20/)<br>
* keijiro for MiniEngineAO (https://github.com/keijiro/MiniEngineAO)<br>
* keijiro for Gaussian Blur(https://github.com/keijiro)
* For depth based voxelization (https://github.com/parikshit6321/PVGI/blob/master/Assets/ProgressiveVoxelizedGI/Shaders/VoxelGridEntry.compute)
