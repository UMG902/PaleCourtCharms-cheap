using UnityEngine;
using SFCore.Utils;

namespace PaleCourtCharms
{
    // Copied from ModCommon so it doesn't have to be rebuilt again 
    // https://github.com/Kerr1291/ModCommon/blob/master/ModCommon/Extensions/GameObjectExtensions.cs
    public static class GameObjectExtensions
    {
        public static bool FindAndDestroyGameObjectInChildren(this GameObject gameObject, string name)
        {
            bool found = false;
            GameObject toDestroy = gameObject.FindGameObjectInChildren(name);
            if (toDestroy != null)
            {
                GameObject.Destroy(toDestroy);
                found = true;
            }

            return found;
        }

        public static GameObject FindGameObjectNameContainsInChildren(this GameObject gameObject, string name)
        {
            if (gameObject == null)
                return null;

            foreach (var t in gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains(name))
                    return t.gameObject;
            }

            return null;
        }

        public static string PrintSceneHierarchyPath(this GameObject gameObject)
        {
            if (gameObject == null)
                return "WARNING: NULL GAMEOBJECT";

            string objStr = gameObject.name;

            if (gameObject.transform.parent != null)
                objStr = gameObject.transform.parent.gameObject.PrintSceneHierarchyPath() + "\\" + gameObject.name;

            return objStr;
        }

    }

}
