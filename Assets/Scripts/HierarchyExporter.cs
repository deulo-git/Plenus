using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;

public class HierarchyExporter
{
    //[MenuItem("Tools/Export Hierarchy")]
    //static void Export()
    //{
    //    StringBuilder sb = new StringBuilder();

    //    foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
    //    {
    //        ExportObject(root.transform, sb, 0);
    //    }

    //    File.WriteAllText("Hierarchy.txt", sb.ToString());

    //    Debug.Log("Hierarchy exportada.");
    //}

    //static void ExportObject(Transform t, StringBuilder sb, int depth)
    //{
    //    sb.AppendLine($"{new string(' ', depth * 2)}{t.name}");

    //    foreach (Component c in t.GetComponents<Component>())
    //    {
    //        if (c != null)
    //            sb.AppendLine($"{new string(' ', depth * 2 + 2)}- {c.GetType().Name}");
    //    }

    //    foreach (Transform child in t)
    //        ExportObject(child, sb, depth + 1);
    //} 
}