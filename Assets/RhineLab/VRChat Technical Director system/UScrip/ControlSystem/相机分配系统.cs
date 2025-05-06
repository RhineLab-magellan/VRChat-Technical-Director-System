
using UdonSharp;
using Unity.Mathematics;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class 相机分配系统 : UdonSharpBehaviour
{
    [Tooltip("摄像机锚点")]
    public GameObject[] Targets;
    [Tooltip("脚本锚点")]
    public GameObject[] SystemUdon;
    [Tooltip("启动抖动补偿模式后的默认插值")]
    public float SlarpV = 0.5f;


    [Tooltip("是否启用独立位置缓动")]
    public bool positionSlarp = true;
    [Tooltip("独立位置缓动插值")]
    public float positionSlarpV = 0.5f;
    private int TargetsLength;

    private bool Ready;
    private Transform Camera;
    private bool Slarp;
    private bool UseUdon;
    private GameObject transformTarget;
    private int CameraTrackingTarget = 0;

    void Start()
    {
        Ready = false;
        var parent = gameObject.transform.parent;
        Camera = parent.Find("CameraSystem");
        for (int i = 0; i < Targets.Length; i++)
        {
            Targets[i].SetActive(false);
            if (i < SystemUdon.Length)
            {
                SystemUdon[i].GetComponent<UdonBehaviour>().enabled = false;
            }
            
        }
        TargetsLength = Targets.Length;
    }
    public void ChangerTarget()
    {
        Debug.Log("ChangerTarget Is Start");
        Ready = false;
        for (int i = 0; i < Targets.Length; i++) 
        {
          Targets[i].SetActive(false);
            if ( i < SystemUdon.Length) 
            {
                SystemUdon[i].GetComponent<UdonBehaviour>().enabled = false;
            }
          
        }

        if (CameraTrackingTarget > Targets.Length - 1)
        {
            Debug.Log("The point does not exist");
            return;
        }

        Targets[CameraTrackingTarget].SetActive(true);

        if (CameraTrackingTarget < SystemUdon.Length)
        {
            if(SystemUdon[CameraTrackingTarget] != null) 
            {
                if(UseUdon)
                {
                    SystemUdon[CameraTrackingTarget].GetComponent<UdonBehaviour>().enabled = true;
                }
                else
                {
                    SystemUdon[CameraTrackingTarget].GetComponent<UdonBehaviour>().enabled = false;
                }
                  
            }
        }
        transformTarget = Targets[CameraTrackingTarget];
        Ready = true;
    }

    private void Update()
    {
        if (Ready == true) 
        {
            if(Slarp == true) 
            {
                
                var quaternion = Quaternion.Slerp(Camera.transform.rotation,transformTarget.transform.rotation, SlarpV*Time.deltaTime*20);
                Vector3 vector3 = transformTarget.transform.position;
                if (!positionSlarp)
                //联动缓动
                {
                    vector3 = Vector3.Slerp(Camera.transform.position, transformTarget.transform.position,SlarpV*Time.deltaTime*20);
                }
                //独立缓动
                else
                {
                    vector3 = Vector3.Slerp(Camera.transform.position, transformTarget.transform.position, positionSlarpV*Time.deltaTime*20);
                }

                Camera.transform.SetPositionAndRotation(vector3, quaternion);
            }
            else 
            {
                Camera.transform.SetPositionAndRotation(transformTarget.transform.position, transformTarget.transform.rotation);
            }

        }
    }

    private void OnDisable()
    {
        Ready = false;
        for (int i = 0; i < Targets.Length; i++)
        {
         Targets[i].SetActive(false);
        if (i < SystemUdon.Length)
            {
                SystemUdon[i].GetComponent<UdonBehaviour>().enabled = false;
            }

        }
    }

    private void OnEnable()
    {
        if (Camera == null) 
        {
            var parent = gameObject.transform.parent;
            //Debug.Log(parent.name);
            Camera = parent.Find("CameraSystem");
        }

    }
    

    

}
