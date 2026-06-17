
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;


[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class AnimatorControl : UdonSharpBehaviour
{
    //缓存LocalPlayer
    private VRCPlayerApi LocalPlayer;
    //应用动画器数组
    public Animator[] Animators;
    private Animator AnimatorUSE;
    

    //应用动画器索引获取
    public Dropdown AnimatorDrop;
    [UdonSynced] private int AnimatorIndex;

    //动画名称获取
    public InputField InputField;
    [UdonSynced]private string animeName;

    //浮点参数获取
    public Slider Slider;
    [UdonSynced] private float SliderValue;
    public Text SliderText;

    //[Tooltip("是否直接更新浮点值而不等待同步？")]
    private bool FastUpdate;
    //[Tooltip("是否每次值改变都发送同步（可能会导致性能下降）")]
    private bool FastSYNC;
    public UdonBehaviour FastSYNCMod;
    public float SlarpV;
    //Int参数容器
    public Text int1T;
    public Text int2T;
    public Text int3T;

    [UdonSynced] private int int1;
    [UdonSynced] private int int2;
    [UdonSynced] private int int3;

    //布尔参数容器
    public Image[] boolb;

    [UdonSynced] private bool bool1;
    [UdonSynced] private bool bool2;
    [UdonSynced] private bool bool3;
    [UdonSynced] private bool bool4;

    //DEBUG事件
    public Text DebugT;
    private float Debugtime;
    private bool DebugMOD;

    //自动播放参数Bool
    private bool AUTOplaying;
    [Tooltip("动画从开始到结束所需要的时间.单位为秒.此为动画器无PlayTime属性或属性值=0时的默认值")]
    public float AutoTime;
    private float AutoPlayUseTime;

    //网络重传机制
    private bool NetworkingStart;
    private bool NetworkingOn;

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if(player != LocalPlayer) { return; }
        OnDeserialization();
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "FastSYNCJoin");
    }
    private void Start()
    {
        LocalPlayer = Networking.LocalPlayer;
        AutoPlayUseTime = AutoTime;
        AnimatorUSE = Animators[0];
        if (FastSYNC)
        {
            boolb[5].color = Color.white;
        }
        else
        {
            boolb[5].color = Color.black;
        }
        if (FastUpdate)
        {
            boolb[4].color = Color.white;
        }
        else
        {
            boolb[4].color = Color.black;
        }
        FastSYNCMod.SetProgramVariable("SlarpV", SlarpV);

    }
    
    
    //网络重传机制-更安全的网络同步
    public void SafeSync()
    {
        NetworkingOn = true;
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "NetworkStart1");
        DebugT.text = "Safe SYNC parameter Is Start,Your value Set Successful";
        OnDeserialization();
    }

    public void NetworkS()
    {   if(NetworkingOn == false) { return; }
        if (LocalPlayer == Networking.GetOwner(this.gameObject)) { RequestSerialization(); }
        NetworkingOn = false;
        DebugT.text = "Safe SYNC Sending data";
        
    }

    public void NetworkStart1()
    {
        if (LocalPlayer == Networking.GetOwner(this.gameObject)) { DebugT.text = "Wait Other Player Return Info"; return; }
        NetworkingStart = true;
        SendCustomEventDelayedSeconds("NetwokEnd", 10);
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetworkS");
        
    }

    public void NetwokEnd()
    {
        if (NetworkingStart) 
        { 
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetworkError");
            DebugT.text = "Oh,Your SYNC is Error,System will be ReSync";
        }
    }

    public void NetworkError()
    {
        DebugT.text = "SomeBody SYNC is Error,System will be ReSync";
        RequestSerialization();
    }
    

    //同步解压
    public override void OnDeserialization() 
    {
        //接受成功
        NetworkingStart = false;
        //更新动画控制器
        AnimatorUSE = Animators[AnimatorIndex];
        AnimatorDrop.value = AnimatorIndex;
        if (LocalPlayer != Networking.GetOwner(this.gameObject)){ SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "SYNCsuccseeful"); }
        //参数写入
        AnimatorUSE.SetFloat("Float1", SliderValue);
        AnimatorUSE.SetInteger("Int1", int1);
        AnimatorUSE.SetInteger("Int2", int2);
        AnimatorUSE.SetInteger("Int3", int3);
        AnimatorUSE.SetBool("Bool1", bool1);
        AnimatorUSE.SetBool("Bool2", bool2);
        AnimatorUSE.SetBool("Bool3", bool3);
        AnimatorUSE.SetBool("Bool4", bool4);

        NetworkingStart = false;
        if (LocalPlayer != Networking.GetOwner(this.gameObject)){ DisplayValue(); }
    }

    //其他玩家写入参数
    private void DisplayValue()
    {
        Slider.SetValueWithoutNotify(SliderValue);
        SliderText.text = SliderValue.ToString();
        int1T.text = int1.ToString();
        int2T.text = int2.ToString();
        int3T.text = int3.ToString();
        if (bool1)
        {
            boolb[0].color = Color.white;
        }
        else
        {
            boolb[0].color = Color.black;
        }
        if (bool2)
        {
            boolb[1].color = Color.white;
        }
        else
        {
            boolb[1].color = Color.black;
        }
        if (bool3)
        {
            boolb[2].color = Color.white;
        }
        else
        {
            boolb[2].color = Color.black;
        }
        if (bool4)
        {
            boolb[3].color = Color.white;
        }
        else
        {
            boolb[3].color = Color.black;
        }
    }

    //Debug信息

    public void SYNCsuccseeful() 
    {
        if (LocalPlayer == Networking.GetOwner(this.gameObject))
        {
            DebugT.text = "Other Player SYNC Successful";
        }
        else 
        {
            DebugT.text = "Player SYNC Successful"; 
        }
        DebugMOD = true;
        Debugtime = 0;
    }

    private void CleanSYNC()
    {
        if (Debugtime >= 5)
        {
            Debugtime = 0;
            DebugMOD = false;
            DebugT.text = null;
            return;
        }
        Debugtime += Time.deltaTime;
    }
    //参数同步
    public void SYNC() 
    {
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        SafeSync();
    }

    //切换控制的动画器
    public void AnimatorChanger() 
    {
        AnimatorIndex = AnimatorDrop.value;
        AnimatorUSE = Animators[AnimatorIndex];
        if (FastSYNC == true)
        { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "FastSyncOff"); }
        AnimatorDATAchanger();
        DebugT.text = "Animator Changer Successful";
        DebugMOD = true;
        Debugtime = 0;
        AUTOplaying = false;
        
    }

    private void AnimatorDATAchanger()
    {
        //获取并更新Float值
        var Speed= AnimatorUSE.GetFloat("PlayTime");
        if (Speed != 0f) { AutoPlayUseTime = Speed; }
        else { AutoPlayUseTime = AutoTime; }
        SliderValue = AnimatorUSE.GetFloat("Float1");
        Slider.SetValueWithoutNotify(SliderValue);
        SliderText.text = SliderValue.ToString();

        //获取并更新Int值
        int1 = AnimatorUSE.GetInteger("Int1");
        int2 = AnimatorUSE.GetInteger("Int2");
        int3 = AnimatorUSE.GetInteger("Int3");
        int1T.text = int1.ToString();
        int2T.text = int2.ToString();
        int3T.text = int3.ToString();

        //获取并更新Bool值
        bool1 = AnimatorUSE.GetBool("Bool1");
        bool2 = AnimatorUSE.GetBool("Bool2");
        bool3 = AnimatorUSE.GetBool("Bool3");
        bool4 = AnimatorUSE.GetBool("Bool4");

        if (bool1)
        {
            boolb[0].color = Color.white;
        }
        else
        {
            boolb[0].color = Color.black;
        }
        if (bool2)
        {
            boolb[1].color = Color.white;
        }
        else
        {
            boolb[1].color = Color.black;
        }
        if (bool3)
        {
            boolb[2].color = Color.white;
        }
        else
        {
            boolb[2].color = Color.black;
        }
        if (bool4)
        {
            boolb[3].color = Color.white;
        }
        else
        {
            boolb[3].color = Color.black;
        }
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        
        RequestSerialization();
        OnDeserialization();
    }

    //动画名称输入
    public void AnimeNameChanger() 
    {
        animeName = InputField.text;
        DebugT.text = "Next play Anime is " + animeName;
        DebugMOD = true;
        Debugtime = 0;
    }
    
    //动画名称触发与同步
    public void AnimePlay() 
    {
        if(animeName == null) { return; }
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "AnimePlaySYNC");
        DebugT.text = animeName + " Is Start Play (If is exist)";
        DebugMOD = true;
        Debugtime = 0;
    }
    public void AnimePlaySYNC()
    {
        AnimatorUSE.Play("Defect");
        SendCustomEventDelayedFrames("AnimePlay2", 1);
    }
    public void AnimePlay2()
    {
        if (animeName != null)
        {
            AnimatorUSE.Play(animeName);
        }
    }

    //Float值变化
    public void SliderChanger()
    {
        SliderValue = Slider.value;
        SliderText.text = SliderValue.ToString();
        if (FastUpdate == true) 
        {AnimatorUSE.SetFloat("Float1", SliderValue);}
        
    }

    //自动播放按钮触发-同步
    public void AutoPlay() 
    {
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "AUTOPLAYSYNC");
    }

    public void AUTOPLAYSYNC()
    {
        
        AUTOplaying = !AUTOplaying;
        if(AUTOplaying) 
        {
            DebugT.text = "AutoPlay IS Start";
            DebugMOD = true;
            Debugtime = 0;

        }
        else
        {
            DebugT.text = "AutoPlay IS Stop";
            DebugMOD = true;
            Debugtime = 0;
        }
    }

    //自动播放
    private void Update()
    {
        //AUTOPlay部分
        if(AUTOplaying==true)
        {
            //float increment = (1f - SliderValue) * (Time.deltaTime / AutoPlayUseTime);
            float increment = (Time.deltaTime / AutoPlayUseTime);
            SliderValue += increment;

            if (SliderValue >= 1f)
            {
                SliderValue = 1f;
                AUTOplaying = false;   
            }
            AnimatorUSE.SetFloat("Float1", SliderValue);
            Slider.SetValueWithoutNotify(SliderValue);
            SliderText.text = SliderValue.ToString();
        }
        //Debug部分
        if (DebugMOD)
        {
            CleanSYNC();
        }
    }

    //Fast模式设置
     public void FastsetChanger()
     {
            FastUpdate = !FastUpdate;
            if (FastUpdate)
            {
                boolb[4].color = Color.white;
                DebugT.text = "Becareful:Fast Set Mod Is Open";
                DebugMOD = true;
                Debugtime = 0;
        }
            else
            {
                boolb[4].color = Color.black;
                DebugT.text = "Fast Set Mod Is OFF";
                DebugMOD = true;
                Debugtime = 0;
        }

     }

    public void FastSYNCChanger()
    {
        Networking.SetOwner(LocalPlayer, FastSYNCMod.gameObject);
        FastSYNC = !FastSYNC;
        if (FastSYNC)
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "FastSYNCOn");
        }
        else
        {
            
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "FastSyncOff");
        }

    }

    public void FastSYNCOn()
    {
        RequestSerialization();
        if(AnimatorUSE == null) { AnimatorUSE = Animators[AnimatorIndex]; }
        boolb[5].color = Color.white;
        DebugT.text = "Becareful:Fast SYNC Mod Is Open ";
        DebugMOD = true;
        Debugtime = 0;
        FastSYNCMod.SetProgramVariable("AnimatorUSE", AnimatorUSE);
        FastSYNCMod.SetProgramVariable("ISUSE", true);
        FastSYNCMod.enabled = true;
        FastSYNC = true;
    }
    public void FastSyncOff()
    {
        boolb[5].color = Color.black;
        DebugT.text = "Fast SYNC Mod Is OFF ";
        FastSYNCMod.SetProgramVariable("ISUSE", false);
        DebugMOD = true;
        Debugtime = 0;
        FastSYNCMod.enabled = false;
        FastSYNC = false;
    }

    //加入同步FastSync模式
    public void FastSYNCJoin() 
    {
        if (LocalPlayer != Networking.GetOwner(this.gameObject)){ return; }
        if (FastSYNC)
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "FastSYNCOn");
        }
        else
        {

            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "FastSYNCOFF");
        }
    }

    //int值变化部分

    public void UP1()
    {
        int1 = int1 + 1;
        int1T.text = int1.ToString();
    }

    public void Down1()
    {
        int1 = int1 - 1;
        if (int1 <= 0)
        { int1 = 0; }
        int1T.text = int1.ToString();
    }

    public void UP2()
    {
        int2 = int2 + 1;
        int2T.text = int2.ToString();
    }

    public void Down2()
    {
        int2 = int2 - 1;
        if (int2 <= 0)
        { int2 = 0; }
        int2T.text = int2.ToString();
    }

    public void UP3()
    {
        int3 = int3 + 1;
        int3T.text = int3.ToString();
    }

    public void Down3()
    {
        int3 = int3 - 1;
        if (int3 <= 0)
        { int3 = 0; }
        int3T.text = int3.ToString();
    }

    //Bool值变化部分

    public void Bool1()
    {
        bool1 = !bool1; 
        if (bool1)
        {
            boolb[0].color = Color.white;
        }
        else
        {
            boolb[0].color = Color.black;
        }
    }
    public void Bool2()
    { 
        bool2 = !bool2;
        if (bool2)
        {
            boolb[1].color = Color.white;
        }
        else
        {
            boolb[1].color = Color.black;
        }
    }
    public void Bool3()
    { 
        bool3 = !bool3;
        if (bool3)
        {
            boolb[2].color = Color.white;
        }
        else
        {
            boolb[2].color = Color.black;
        }
    }
    public void Bool4()
    { 
        bool4 = !bool4;
        if (bool4)
        {
            boolb[3].color = Color.white;
        }
        else
        {
            boolb[3].color = Color.black;
        }

    }

}
