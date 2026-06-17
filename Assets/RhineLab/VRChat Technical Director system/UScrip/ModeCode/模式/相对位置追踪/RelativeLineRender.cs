
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class RelativeLineRender : UdonSharpBehaviour
{
    public LineRenderer lineRenderer;
    public Transform[] points;
    void Start()
    {

    }

    void Update()
    {
        if (lineRenderer != null && points != null)
        {
            lineRenderer.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                lineRenderer.SetPosition(i, points[i].position);
            }
        }
    }

}

