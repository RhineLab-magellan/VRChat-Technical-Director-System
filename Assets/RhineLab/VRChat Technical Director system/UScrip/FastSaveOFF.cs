
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FastSaveOFF : UdonSharpBehaviour
{
    public UdonBehaviour Main;
    public TMP_Text Debug;
    private GameObject[] System;
    private UdonBehaviour SystemUdon;
    private int Index;
    private string Debugtext;

    [UdonSynced] private int SystemIndex;
    [UdonSynced] private int VoidNameID;
    [UdonSynced] private int CameraTrackingTarget;
    [UdonSynced] private string DisPlayName;
    [UdonSynced] private bool Slarp;

    [UdonSynced] private bool UdonUse;

    private int[] CameraTrackingTargets;
    private int[] VoidNameIDs;
    private string[] DisPlayNames;
    private bool[] Slarps;

    private bool[] UdonUses;


    void Start()
    {
        System = (GameObject[])Main.GetProgramVariable("System");
        Index = System.Length;
        CameraTrackingTargets = new int[Index];
        DisPlayNames = new string[Index];
        Slarps = new bool[Index];
        UdonUses = new bool[Index];
        VoidNameIDs = new int[Index];

        for (int i = 0; i < Index; i++)
        {
            CameraTrackingTargets[i] = 0;
            DisPlayNames[i] = "0";
            Slarps[i] = false;
            UdonUses[i] = false;
            VoidNameIDs[i] = 0;
        }
    }

    public void StartRead()
    {
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);

        SystemIndex = (int)Main.GetProgramVariable("SystemIndex");
        SystemUdon = System[SystemIndex].GetComponent<UdonBehaviour>();
        VoidNameID = (int)SystemUdon.GetProgramVariable("VoidNameID");
        CameraTrackingTarget = (int)SystemUdon.GetProgramVariable("CameraTrackingTarget");
        DisPlayName = (string)SystemUdon.GetProgramVariable("DisPlayName");
        UdonUse = (bool)SystemUdon.GetProgramVariable("UseUdon");
        Slarp = (bool)SystemUdon.GetProgramVariable("Slarp");

        RequestSerialization();
        OnDeserialization();
    }

    public override void OnDeserialization()
    {
        CameraTrackingTargets[SystemIndex] = CameraTrackingTarget;
        DisPlayNames[SystemIndex] = DisPlayName;
        Slarps[SystemIndex] = Slarp;
        UdonUses[SystemIndex] = UdonUse;
        VoidNameIDs[SystemIndex] = VoidNameID;
        Debugtext = "Save Succssful<br>System Camera = " + SystemIndex.ToString()
            + "<br>Camera Mod = " + VoidNameID.ToString()
            + "<br>Target = " + CameraTrackingTarget.ToString()
            + "<br>Next Tracking Player =  " + DisPlayName
            + "<br>Slarp is " + Slarp.ToString();
        Debug.text = Debugtext;
    }

    public void ChackInfo()
    {
        SystemIndex = (int)Main.GetProgramVariable("SystemIndex");
        CameraTrackingTarget = CameraTrackingTargets[SystemIndex];
        DisPlayName = DisPlayNames[SystemIndex];
        Slarp = Slarps[SystemIndex];
        UdonUse = UdonUses[SystemIndex];
        VoidNameIDs[SystemIndex] = VoidNameID;
        Debugtext = "Check Succssful<br>System Camera = " + SystemIndex.ToString()
            + "<br>Camera Mod = " + VoidNameID.ToString()
            + "<br>Target = " + CameraTrackingTarget.ToString()
            + "<br>Next Tracking Player =  " + DisPlayName
            + "<br>Slarp is " + Slarp.ToString();
        Debug.text = Debugtext;
    }

    public void StartLoad()
    {
        SystemIndex = (int)Main.GetProgramVariable("SystemIndex");
        VoidNameID = VoidNameIDs[SystemIndex];
        CameraTrackingTarget = CameraTrackingTargets[SystemIndex];
        DisPlayName = DisPlayNames[SystemIndex];
        Slarp = Slarps[SystemIndex];
        UdonUse = UdonUses[SystemIndex];
        Debugtext = "Set Succssful<br>System Camera = " + SystemIndex.ToString()
            + "<br>Camera Mod = " + VoidNameID.ToString()
            + "<br>Target = " + CameraTrackingTarget.ToString()
            + "<br>Next Tracking Player =  " + DisPlayName
            + "<br>Slarp is " + Slarp.ToString();
        Debug.text = Debugtext;
        Main.SetProgramVariable("CameraTrackingTarget", CameraTrackingTarget);
        Main.SetProgramVariable("VoidNameID", VoidNameID);
        Main.SetProgramVariable("DisPlayName", DisPlayName);
        Main.SetProgramVariable("Slarp", Slarp);
        Main.SetProgramVariable("UseUdon", UdonUse);
        Main.SendCustomEvent("QuickSave");

    }


    //附加Json快速导出导入整体预设
    public TMP_InputField JsonOutput;
    private DataToken DataJson;
    private string Json;
    private DataDictionary DefectData;
    private DataList List;
    private int JsonIndex;

    //导入并转换为DataList
    public void FromJson()
    {
        Json = JsonOutput.text;
        VRCJson.TryDeserializeFromJson(Json, out DataJson);
        if (DataJson.TokenType == TokenType.DataList)
        {
            List = (DataList)DataJson;

        }
        else
        {
            UnityEngine.Debug.Log(DataJson);
            UnityEngine.Debug.Log("DataList");
            Debug.text = $"Reading the Json failed, and it is possible that the data has been corrupted";
            return;
        }

        SendCustomEventDelayedFrames("ResetArray", 1);
        Debug.text = "Get Data Successful , Set Start";
    }
    //将DataList转换为Array

    public void ResetArray()
    {
        DataDictionary DataD;
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
            DataD.TryGetValue("VoidNameID", out DataToken DATAVoidNameID);
            int.TryParse(DATAVoidNameID.ToString(), out int VoidID);
            VoidNameIDs[i] = VoidID;

            DataD.TryGetValue("CameraTrackingTarget", out DataToken DATACameraTrackingTarget);
            int.TryParse(DATACameraTrackingTarget.ToString(), out int Targe);
            CameraTrackingTargets[i] = Targe;

            DataD.TryGetValue("DisPlayName", out DataToken DATADisPlayName);
            DisPlayNames[i] = DATADisPlayName.ToString();

            DataD.TryGetValue("Slarp", out DataToken DATASlarp);
            Slarps[i] = DATASlarp.Boolean;

            DataD.TryGetValue("UdonUse", out DataToken DATAUdonUse);
            UdonUses[i] = DATAUdonUse.Boolean;

        }
        JsonIndex = 0;
        RelodeData();


    }
    //每五秒写入一次系统
    public void RelodeData()
    {
        if (JsonIndex < VoidNameIDs.Length)
        {
            VoidNameID = VoidNameIDs[JsonIndex];
            CameraTrackingTarget = CameraTrackingTargets[JsonIndex];
            DisPlayName = DisPlayNames[JsonIndex];
            Slarp = Slarps[JsonIndex];
            UdonUse = UdonUses[JsonIndex];
            Main.SetProgramVariable("CameraTrackingTarget", CameraTrackingTarget);
            Main.SetProgramVariable("VoidNameID", VoidNameID);
            Main.SetProgramVariable("DisPlayName", DisPlayName);
            Main.SetProgramVariable("Slarp", Slarp);
            Main.SetProgramVariable("UseUdon", UdonUse);
            Main.SetProgramVariable("SystemIndex", JsonIndex);
            Main.SendCustomEvent("QuickSave");
            JsonIndex++;
            SendCustomEventDelayedFrames("RelodeData", 5);

        }
        else
        {
            Debug.text = "Recovery of stored data was successful";
        }
    }

    //导出为Json
    public void SaveJson()
    {
        List = new DataList();
        for (int i = 0; i < VoidNameIDs.Length; i++)
        {
            VoidNameID = VoidNameIDs[i];
            CameraTrackingTarget = CameraTrackingTargets[i];
            DisPlayName = DisPlayNames[i];
            Slarp = Slarps[i];
            UdonUse = UdonUses[i];

            //新建字典
            DefectData = new DataDictionary();
            //模式ID
            DefectData.Add("VoidNameID", VoidNameID);
            //点位
            DefectData.Add("CameraTrackingTarget", CameraTrackingTarget);
            //跟踪对象
            DefectData.Add("DisPlayName", DisPlayName);
            //缓速
            DefectData.Add("Slarp", Slarp);
            //UseUdon
            DefectData.Add("UdonUse", UdonUse);

            //如果索引值小于整个长度，就覆盖，大于整个长度，就新建
            if (i >= 0 && i < List.Count)
            {
                // 移除指定索引处的字典
                List.RemoveAt(Index);
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
            VRCJson.TrySerializeToJson(List, JsonExportType.Minify, out DataJson);
            JsonOutput.text = $"{DataJson}";
            Debug.text =
                $" The attempt to export has ended. Here are the results of your attempt<br>{DataJson} ";
        }
    }
}
