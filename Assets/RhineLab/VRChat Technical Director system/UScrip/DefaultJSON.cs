
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DefaultJSON : UdonSharpBehaviour
{
    //主脚本
    public UdonBehaviour Main;

    //Debug面板
    public TMP_Text Debug;
    //常规部分-索引
    public int Index;

    //常规部分-实际数据
    private string DefectName;
    private int VoidNameID;
    private int CameraTrackingTarget;
    private string DisPlayName;
    private bool Slarp;
    private bool UdonUse;
    private string Info;

    //常规部分-Json数组
    private DataDictionary DefectData;
    private DataList List;
    private string[] DefectNames;
    private string[] Infos;

    //输入部分-Json
    public InputField JsonInput;
    public TMP_InputField JsonOutput;
    private DataToken DataJson;
    private string Json;

    //刷新按钮
    public Text DefectNameText;
    public Text IndexText;

    //生成按钮使用
    public GameObject _prefab;
    public Transform ObjParent;
    public GameObject[] Buttoms;


    void Start()
    {
        Index = 0;
        List = new DataList();
        DefectName = "";
        VoidNameID = 0; CameraTrackingTarget = 0;
        DisPlayName = "";
        Slarp = false;
        UdonUse = false;
        Info = "This is a Info , You can change the text in your text editor";
        DefectNames = new string[0];
        Infos = new string[0];
    }

    //写入
    public void SaveToken()
    {
        VoidNameID = (int)Main.GetProgramVariable("VoidNameID");
        CameraTrackingTarget = (int)Main.GetProgramVariable("CameraTrackingTarget");
        DisPlayName = (string)Main.GetProgramVariable("DisPlayName");
        Slarp = (bool)Main.GetProgramVariable("Slarp");
        UdonUse = (bool)Main.GetProgramVariable("UseUdon");
        Info = "This is a Info , You can change the text in your text editor";

        //新建字典
        DefectData = new DataDictionary();
        //名称
        DefectData.Add("Name", "Null");
        //模式ID
        DefectData.Add("VoidNameID", VoidNameID);
        //点位
        DefectData.Add("CameraTrackingTarget", CameraTrackingTarget);
        //跟踪对象
        DefectData.Add("DisPlayName", DisPlayName);
        //缓速
        DefectData.Add("Slarp", Slarp);
        DefectData.Add("UdonUse", UdonUse);
        //备注
        DefectData.Add("Info", Info);
        

        //如果索引值小于整个长度，就覆盖，大于整个长度，就新建
        if (Index >= 0 && Index < List.Count)
        {
            // 获取原值
            DataDictionary originalData = (DataDictionary)List[Index];
            originalData.TryGetValue("Name", out DataToken Token);
            originalData.TryGetValue("Info", out DataToken InfoToken);
            // 移除指定索引处的字典
            List.RemoveAt(Index);
            // 从 originalData 中移除键为 "Name" 的条目
            DefectData.Remove("Name");
            DefectData.Remove("Info");
            // 将 Token 添加到 originalData 中，键为 "Name"
            DefectData.Add("Name", Token);
            DefectData.Add("Info", InfoToken);
            // 将修改后的字典插入到指定索引处
            List.Insert(Index, DefectData);
            Debug.text = "The default at index " + Index.ToString() + " was overwritten successfully";
        }
        else
        {
            var IndexNew = List.Count;
            DefectData.Remove("Name");
            DefectData.Add("Name", IndexNew.ToString());
            List.Add(DefectData);
            Debug.text = "Saved successfully, he was assigned to index " + IndexNew.ToString();
        }
        ResetArray();
        LoadToken();
    }
    //读取
    public void LoadToken()
    {
        if (Index < DefectNames.Length)
        {
            DefectNameText.text = DefectNames[Index];
            IndexText.text = Index.ToString();
        }
        else
        {
            Debug.text = "Data Not Find";
            DefectNameText.text = "NULL";
            IndexText.text = Index.ToString();
            return;
        }
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
            $"UdonUse = {UdonUse}<br>" +
            $"Info :<br>{Info}";
        //输出到Main脚本
        Main.SetProgramVariable("VoidNameID", VoidNameID);
        Main.SetProgramVariable("CameraTrackingTarget", CameraTrackingTarget);
        if(DisPlayName == string.Empty) { Main.SetProgramVariable("DisPlayName", DisPlayName); }
        Main.SetProgramVariable("Slarp", Slarp);
        Main.SetProgramVariable("UseUdon", UdonUse);
        Main.SendCustomEvent("QuickSave");
    }

    //检查
    public void CheckToken()
    {
        if (-1 < Index && Index < DefectNames.Length)
        {
            DefectNameText.text = DefectNames[Index];
            IndexText.text = Index.ToString();
        }
        else
        {
            Debug.text = "Data Not Find";
            DefectNameText.text = "NULL";
            IndexText.text = Index.ToString();
            return;
        }

        DefectData = (DataDictionary)List[Index];
        if (DefectData == null) { Debug.text = "Read failed, your Token may be corrupted"; return; }

        DefectData.TryGetValue("Name", out DataToken DataName);
        DefectName = DataName.ToString();

        DefectData.TryGetValue("VoidNameID", out DataToken DATAVoidNameID);
        VoidNameID = int.Parse(DATAVoidNameID.ToString()) ;

        DefectData.TryGetValue("CameraTrackingTarget", out DataToken DATACameraTrackingTarget);
        CameraTrackingTarget = int.Parse(DATACameraTrackingTarget.ToString());

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
            $" You are now inspecting the data of {DefectName}<br> " +
            $"Data:<br>" +
            $"VoidNameID = {VoidNameID}<br>" +
            $"CameraTarget = {CameraTrackingTarget}<br>" +
            $"DisPlayName = {DisPlayName},<br>" +
            $"Slarp = {Slarp}<br>" +
            $"UdonUse = {UdonUse}<br>" +
            $"Info =<br>{Info}";
        
    }
    //序列化为Json
    public void ToJson()
    {
        VRCJson.TrySerializeToJson(List, JsonExportType.Beautify, out DataJson);
        JsonOutput.text = $"{DataJson}";
        Debug.text =
            $" The attempt to export has ended. Here are the results of your attempt<br> " +
            $"{DataJson}" ;
    }
    //Json序列化为列表
    public void FromJson()
    {
        Json = JsonInput.text;
        VRCJson.TryDeserializeFromJson(Json, out DataJson);
        if (DataJson.TokenType == TokenType.DataList)
        {
            List = (DataList)DataJson;
            
        }
        else
        {
            UnityEngine.Debug.Log(DataJson);
            UnityEngine.Debug.Log("DataList");
            Debug.text = $"Reading the token failed, and it is possible that the data has been corrupted";
            return;
        }
        
        SendCustomEventDelayedFrames("ResetArray", 1);
        Debug.text = "Get Data Successful , Data initialization is complete";
    }
    //刷新缓存
    public void ResetArray()
    {
        DefectNames = new string[List.Count];
        Infos = new string[List.Count];
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
            Infos[i] = DATAInfo.ToString();
        }
        string Text = Debug.text + $"<br><br><br>Initialization succeeds for {DefectNames.Length} presets <br> {DebugText}";
        Text = Text.Substring(0, Text.Length - 1);
        Debug.text = Text;
        ResetButton();
    }
    

    //预读取参数 读取参数位置：LoadToken
    
    public void ResetButton()
    {
        var Buttomss = new GameObject[DefectNames.Length];
        if (DefectNames.Length > Buttoms.Length)
        {


            for (int i = Buttoms.Length; i < Buttomss.Length; i++)
            {
                var a = Instantiate(_prefab, ObjParent);
                Buttomss[i] = a;
            }
            for (int i = 0; i < Buttoms.Length; i++)
            {
                Buttomss[i] = Buttoms[i];
            }
            Buttoms = Buttomss;
        }
        else if (DefectNames.Length < Buttoms.Length)
        {
            for (int i = DefectNames.Length; i < Buttoms.Length; i++)
            {
                Destroy(Buttoms[i]);
            }
            for (int i = 0; i < Buttomss.Length; i++)
            {
                Buttomss[i] = Buttoms[i];
            }
            Buttoms = Buttomss;
        }

        for (int i = 0; i < DefectNames.Length; i++)
        {
            var cmpObj = Buttoms[i].GetComponent<UdonBehaviour>();
            cmpObj.SetProgramVariable("DefectName", DefectNames[i]);
            cmpObj.SetProgramVariable("Index",i );
            cmpObj.SendCustomEvent("Init");
            Buttoms[i] = cmpObj.gameObject;
        }
    }
    public void NewIndex()
    {
        Index = DefectNames.Length;
        Debug.text = "The new index is ready,The new ID is "+ Index.ToString();
        DefectNameText.text = "NULL";
        IndexText.text = Index.ToString();
        return;
    }
    public void Remove()
    {
        if (List.Count > Index) 
        {
            List.RemoveAt(Index);
            Debug.text = "Remove Successful";
            
            ResetArray();
        }
        
    }
}