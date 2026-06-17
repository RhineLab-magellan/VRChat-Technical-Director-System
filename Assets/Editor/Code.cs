using UnityEngine;
using UnityEditor;
using System.IO;

public class CalcCodeLine
{
    static string calcPath = "Assets/RhineLab"; //代码统计路径【可自定义】

    [MenuItem("Tools/统计代码行数")]
    static void CalcCode()
    {
        if (!Directory.Exists(calcPath))
        {
            Debug.LogError(string.Format("Path Not Exist  : \"{0}\" ", calcPath));
            return;
        }
        string[] fileName = Directory.GetFiles(calcPath, "*.cs", SearchOption.AllDirectories);
        int totalLine = 0; //代码总行数
        foreach (var temp in fileName)
        {
            int nowLine = 0; //当前文件累计行数
            StreamReader sr = new StreamReader(temp);
            while (sr.ReadLine() != null)
            {
                nowLine++;
            }
            totalLine += nowLine;
        }
        Debug.Log(string.Format("代码总行数: {0} -> 代码文件数:{1}", totalLine, fileName.Length));
    }
}
