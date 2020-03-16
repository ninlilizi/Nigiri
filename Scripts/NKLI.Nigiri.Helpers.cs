/// <summary>
/// NKLI     : Nigiri - Helpers
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System;
using UnityEngine;

namespace NKLI.Nigiri
{
    public class Helpers : ScriptableObject
    {
        /// <summary>
        /// Dispose and optionally destroy a texture
        /// </summary>
        /// <param name="rt"></param>
        public static void DisposeTextureRef(ref RenderTexture rt, bool destroy)
        {
            if (rt != null)
            {
                rt.Release();
                if (destroy)
                {
                    if (Application.isEditor) DestroyImmediate(rt);
                    else Destroy(rt);
                }
            }
        }

        /// <summary>
        /// Release a compute buffer
        /// </summary>
        /// <param name="cb"></param>
        public static void ReleaseBufferRef(ref ComputeBuffer cb)
        {
            if (cb != null)
            {
                cb.Release();
            }
        }

        /// <summary>
        /// Destroy a scriptable object using optimal method
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="refereceObject"></param>
        public static void DestroyScriptableObject<T>(ref T refereceObject)
        {
            var scriptableOject = Convert.ChangeType(refereceObject, typeof(T));
            if (Application.isEditor) DestroyImmediate((ScriptableObject)scriptableOject);
            else Destroy((ScriptableObject)scriptableOject);
        }
    }
}



