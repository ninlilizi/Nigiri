/// <summary>
/// NKLI     : Nigiri - Test unit hooks
/// Copywrite: Abigail Sara Hocking of Newbury, 2020. 
/// Licence  : The Nigiri 'Bits and pieces' Licence. [v3]
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NKLI.Nigiri.SVO
{
    public class TestUnitHooks
    {
        // Most recently instantiated tree
        public static Tree Most_Recent_Tree { get; private set; }

        /// <summary>
        /// Sets most recently instantiated tree
        /// </summary>
        /// <param name="tree"></param>
        public static void Set_Most_Recent_Tree(Tree tree)
        {
            Most_Recent_Tree = tree;
        }
    }
}
