/// <summary>
/// NKLI     : Nigiri - Helpers_Debug
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace NKLI.Nigiri
{
    public class Helpers_Debug : ScriptableObject
    {
        /// <summary>
        /// Loads PNG
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static Texture2D LoadPNG(string filePath)
        {

            Texture2D tex = null;
            byte[] fileData;

            if (File.Exists(filePath))
            {
                fileData = File.ReadAllBytes(filePath);
                tex = new Texture2D(2, 2);
                tex.LoadImage(fileData);
            }
            return tex;
        }

        /// <summary>
        /// Saves built-in RenderTexture to disk for use in Test Unit generation
        /// </summary>
        /// <param name="name"></param>
        /// <param name="rtb"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public static void SaveRenderTextureBuiltIn(string name, BuiltinRenderTextureType rtb, int width, int height)
        {
            string file = Application.dataPath + name;
            if (!System.IO.File.Exists(file + ".png"))
            {
                RenderTexture rt = RenderTexture.GetTemporary(width, height);
                CommandBuffer cb = new CommandBuffer();

                cb.Blit(rtb, rt);

                Graphics.ExecuteCommandBuffer(cb);

                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                RenderTexture rtCache = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                RenderTexture.active = rtCache;

                rtCache.Release();
                rt.Release();
                cb.Dispose();

                byte[] tbytes = tex.EncodeToPNG();

                FileStream fs0 = System.IO.File.Create(file + ".png");
                fs0.Write(tbytes, 0, tbytes.Length);
                fs0.Close();

                fs0.Dispose();
            }
        }

        /// <summary>
        /// Saves RenderTexture to disk for use in Test Unit generation
        /// </summary>
        /// <param name="name"></param>
        /// <param name="rtb"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public static void SaveRenderTexture(string name, RenderTexture rt, int width, int height)
        {
            string file = Application.dataPath + name;
            if (!System.IO.File.Exists(file + ".png"))
            {
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                RenderTexture rtCache = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                RenderTexture.active = rtCache;
                rtCache.Release();

                byte[] tbytes = tex.EncodeToPNG();

                FileStream fs0 = System.IO.File.Create(file + ".png");
                fs0.Write(tbytes, 0, tbytes.Length);
                fs0.Close();

                fs0.Dispose();
            }
        }
    }
}



