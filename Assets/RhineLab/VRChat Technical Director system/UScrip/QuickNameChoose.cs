

using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class QuickNameChoose : UdonSharpBehaviour
{
    public Text DisplayerName;
    public UdonBehaviour Main;
    private int Index;
    private VRCPlayerApi LocalPlayer;
    private VRCPlayerApi[] Players;

    public string[] Displayers;

    public GameObject _prefab;
    public Transform ObjParent;
    public GameObject[] Buttoms;

    void Start()
    {
        LocalPlayer = Networking.LocalPlayer;
        Buttoms = new GameObject[0];
    }

    public void Display()
    {
        Main.SendCustomEvent("ResetDisplay") ;
        //更新玩家列表
        Players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(Players);
        Displayers = new string[Players.Length];
        for (int i = 0; i < Players.Length; i++)
        {
            var Name = Players[i].displayName;
            Displayers[i] = Name;
        }
        //更新玩家名称

        var Buttomss = new GameObject[Displayers.Length];
        if (Displayers.Length > Buttoms.Length)
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
        else if (Displayers.Length < Buttoms.Length)
        {
            for (int i = Displayers.Length; i < Buttoms.Length; i++)
            {
                Destroy(Buttoms[i]);
            }
            for (int i = 0; i < Buttomss.Length; i++)
            {
                Buttomss[i] = Buttoms[i];
            }
            Buttoms = Buttomss;
        }

        

        for (int i = 0; i < Displayers.Length; i++)
        {
            var cmpObj = Buttoms[i].GetComponent<UdonBehaviour>();
            cmpObj.SetProgramVariable("DefectName", $"#{i}  " + Displayers[i]);
            cmpObj.SetProgramVariable("Index", i);
            cmpObj.SendCustomEvent("Init");
            Buttoms[i] = cmpObj.gameObject;
        }



    }
    public void SetToken()
    {
        if (Index >= Displayers.Length){return;}
        Main.SetProgramVariable("DisPlayName",Displayers[Index]) ;
        Main.SendCustomEvent("QuickPlayerName");
    }

    public void CheckToken()
    {
        DisplayerName.text = Displayers[Index];
    }


}