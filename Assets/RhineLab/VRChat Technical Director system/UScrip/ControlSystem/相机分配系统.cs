
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

    [Tooltip("FOV同步指数-0为不同步，大于0为实际数量")]
    public float[] FOVIndex;
    [Tooltip("点提示字符串")]
    public string[] PointString;
    [Tooltip("运行该模块是否需要满帧率运行")]
    public bool NeedRuning = false;

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
    private bool VoidObjectActive;

    [HideInInspector]
    public Transform TrackingIndicator;

    [HideInInspector] public ControlCenter controlCenter;

    void Start()
    {
        Ready = false;
        var parent = gameObject.transform.parent;
        Camera = parent.Find("CameraTranform");
        UdonBehaviour udonBehaviour;
        UdonBehaviour Local = GetComponent<UdonBehaviour>();

        for (int i = 0; i < Targets.Length; i++)
        {
            Targets[i].SetActive(false);
            if (i < SystemUdon.Length)
            {
                udonBehaviour = SystemUdon[i].GetComponent<UdonBehaviour>();
                if (udonBehaviour != null)
                {
                    udonBehaviour.enabled = false;
                    udonBehaviour.SetProgramVariable("Managerudon", Local);
                }

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
            if (i < SystemUdon.Length)
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
            UdonBehaviour udonBehaviour = SystemUdon[CameraTrackingTarget].GetComponent<UdonBehaviour>();
            if (udonBehaviour != null)
            {
                if (UseUdon)
                {
                    udonBehaviour.enabled = true;
                    udonBehaviour.SendCustomEventDelayedFrames("OnEnable", 1);
                    udonBehaviour.SetProgramVariable("VoidObjectActive", VoidObjectActive);
                }
                else
                {
                    udonBehaviour.enabled = false;
                }

            }
        }
        transformTarget = Targets[CameraTrackingTarget];
        Ready = true;
        SendCustomEventDelayedFrames("ReSyncFOV", 1);
    }

    private void Update()
    {
        if (Ready)
        {
            if (Slarp == true)
            {

                var quaternion = Quaternion.Slerp(Camera.transform.rotation, transformTarget.transform.rotation, SlarpV * Time.deltaTime * 20);
                Vector3 vector3;
                if (!positionSlarp)
                //联动缓动
                {
                    vector3 = Vector3.Slerp(Camera.transform.position, transformTarget.transform.position, SlarpV * Time.deltaTime * 20);
                }
                //独立缓动
                else
                {
                    vector3 = Vector3.Slerp(Camera.transform.position, transformTarget.transform.position, positionSlarpV * Time.deltaTime * 20);
                }

                //Camera.transform.SetPositionAndRotation(vector3, quaternion);
                Camera.transform.SetPositionAndRotation(vector3, quaternion);
            }
            else
            {
                //Camera.transform.SetPositionAndRotation(transformTarget.transform.position, transformTarget.transform.rotation);
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
            Camera = parent.Find("CameraTranform").GetChild(0);
        }

    }

    public void ChangerUsingPlayer()
    {
        Networking.SetOwner(Networking.LocalPlayer, Camera.gameObject);
        Networking.SetOwner(Networking.LocalPlayer, Camera.GetChild(0).gameObject);
    }

    public void ReSyncFOV()
    {
        if (FOVIndex[CameraTrackingTarget] > 0)
        {
            controlCenter.FOVControllerSet(FOVIndex[CameraTrackingTarget]);
        }
    }




}
