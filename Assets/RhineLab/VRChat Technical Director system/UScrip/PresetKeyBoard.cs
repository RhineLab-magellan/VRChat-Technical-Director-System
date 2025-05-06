
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

public class PresetKeyBoard : UdonSharpBehaviour
{
    //Main脚本
    public string[] DefectNames;
    public UdonBehaviour Main;

    //预设数据
    private DataDictionary DefectData;
    
    //常规部分-实际数据
    private string DefectName;
    private int VoidNameID;
    private int CameraTrackingTarget;
    private string DisPlayName;
    private bool Slarp;

    private bool UdonUse;
    private string Info;

    public Image SystemImage;
    private bool SystemOn;



    //Json写入面板
    public InputField JsonInput;
    //检查用DataToken
    private DataToken DataJson;
    //Debug
    public TMP_Text Debug;
    public TMP_Text InfoT;
    //列表本体
    private DataList List;

    //预设索引-按钮匹配
    private int Index;



    void Start()
    {
        Index = 0;
        List = new DataList();
        DefectName = "";
        VoidNameID = 0; CameraTrackingTarget = 0;
        DisPlayName = "";
        Slarp = false;
        Info = "This is a Info , You can change the text in your text editor";
        DefectNames = new string[0];
    
    }

    public void ControlCenter()
    {
        SystemOn = !SystemOn;
        if (SystemOn)
        {
            SystemImage.color = Color.white;
        }
        else
        {
            SystemImage.color = Color.black;
        }
    }

    //写入预设
    public void FromJson()
    {
        string Json = JsonInput.text;
        VRCJson.TryDeserializeFromJson(Json, out DataJson);
        if (DataJson.TokenType == TokenType.DataList)
        {
            List = (DataList)DataJson;
            
        }
        else
        {
            Debug.text = $"Reading the token failed, and it is possible that the data has been corrupted";
            return;
        }
        ResetArray();
    }

    public void ResetArray()
    {
        InfoT.text = "";
        DefectNames = new string[List.Count];
        DataDictionary DataD;
        string DebugText = "DefectNames = <br>";
        for (int i = 0; i < List.Count; i++)
        {
            List.TryGetValue(i, out DataToken Token);
            if (Token.TokenType == TokenType.DataDictionary)
            {
                DataD = (DataDictionary)Token;
            }
            else
            {                
                Debug.text = $"Reading the token failed, and it is possible that the data has been corrupted";
                return;
            }
            
            DataD.TryGetValue("Name", out DataToken DataName);
            DefectNames[i] = DataName.ToString();
            DebugText = DebugText + DataName + "、";
            DataD.TryGetValue("Info", out DataToken DATAInfo);
        }
        string Text = Debug.text + $"<br>Initialization succeeds for {DefectNames.Length} presets <br> {DebugText}";
        Text = Text.Substring(0, Text.Length - 1);
        Debug.text = Text;
        string InfoText = "";
        for (int i = 0; i < DefectNames.Length; i++)
        {
            InfoText = InfoText +i.ToString()+":"+ DefectNames[i] + "<br>";
        }
        InfoT.text = InfoText;
    }



    //每帧激活按键检测 Ctrl+0-9
    void Update()
    {
        if (SystemOn == false) { return; }
        if (Input.GetKey(KeyCode.LeftShift))
        {
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                Index = 0;
                SendCustomEvent(nameof(LoadToken));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                Index = 1;
                SendCustomEvent(nameof(LoadToken));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                Index = 2;
                SendCustomEvent(nameof(LoadToken));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                Index = 3;
                SendCustomEvent(nameof(LoadToken));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                Index = 4;
                SendCustomEvent(nameof(LoadToken));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                Index = 5;
                SendCustomEvent(nameof(LoadToken));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                Index = 6;
                SendCustomEvent(nameof(LoadToken));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha7))
            {
                Index = 7;
                SendCustomEvent(nameof(LoadToken));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha8))
            {
                Index = 8;
                SendCustomEvent(nameof(LoadToken));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha9))
            {
                Index = 9;
                SendCustomEvent(nameof(LoadToken));
            }
        }
    }

    //加载发送预设
    public void LoadToken()
    {
        UnityEngine.Debug.Log(Index);
        if (Index > List.Count)
        { return; }
        List.TryGetValue(Index, out var Token);
        if (Token.TokenType == TokenType.DataDictionary)
        {
            DefectData = (DataDictionary)Token;
        }
        else
        {
            UnityEngine.Debug.Log(Token.TokenType);
            UnityEngine.Debug.Log(DefectData.GetType());
            Debug.text = $"Reading the token failed, and it is possible that the data has been corrupted";
            return;
        }

        DefectData.TryGetValue("Name", out DataToken DataName);
        DefectName = DataName.ToString();

        DefectData.TryGetValue("VoidNameID", out DataToken DATAVoidNameID);
        int.TryParse(DATAVoidNameID.ToString(), out int VoidID);
        VoidNameID = VoidID;

        DefectData.TryGetValue("CameraTrackingTarget", out DataToken DATACameraTrackingTarget);
        int.TryParse(DATACameraTrackingTarget.ToString(), out int Targe);
        CameraTrackingTarget = Targe;

        DefectData.TryGetValue("DisPlayName", out DataToken DATADisPlayName);
        DisPlayName = DATADisPlayName.ToString();

        DefectData.TryGetValue("Slarp", out DataToken DATASlarp);
        Slarp = DATASlarp.Boolean;

        DefectData.TryGetValue("UdonUse", out DataToken DataUdonUse);
        UdonUse = DataUdonUse.Boolean;

        DefectData.TryGetValue("Info", out DataToken DATAInfo);
        Info = DATAInfo.ToString();

        //输出Debug信息
        Debug.text =
            $"Succseeful to Set The {DefectName} to The System 。<br>" +
            $"Data:<br>" +
            $"VoidNameID = {VoidNameID}<br>" +
            $"CameraTarget = {CameraTrackingTarget}<br>" +
            $"DisPlayName = {DisPlayName},<br>" +
            $"Slarp = {Slarp}<br>" +
            $"UseUdon = {UdonUse}<br>" +
            $"Info :<br>{Info}";
        //输出到Main脚本
        Main.SetProgramVariable("VoidNameID", VoidNameID);
        Main.SetProgramVariable("CameraTrackingTarget", CameraTrackingTarget);
        if(DisPlayName == string.Empty) { Main.SetProgramVariable("DisPlayName", DisPlayName); }
        Main.SetProgramVariable("Slarp", Slarp);
        Main.SetProgramVariable("UseUdon", UdonUse);
        Main.SendCustomEvent("QuickSave");
    }


}
