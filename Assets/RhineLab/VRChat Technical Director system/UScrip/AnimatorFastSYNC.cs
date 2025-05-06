
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class AnimatorFastSYNC : UdonSharpBehaviour
{
    private Animator AnimatorUSE;
    
    [Tooltip("同步补偿")]
    public float Compensation;
    public Slider Slider;
    [UdonSynced] private bool ISUSE = false;
    public UdonBehaviour Main;
    private VRCPlayerApi LocalPlayer;
    private VRCPlayerApi Owner;
    private float SlarpV;
    [UdonSynced] private float SliderValue;

    private void Start()
    {
        LocalPlayer = Networking.LocalPlayer;
        if (Networking.LocalPlayer == Networking.GetOwner(this.gameObject)){ SendCustomEventDelayedSeconds("Start1",1); }
        if (ISUSE == false) { this.GetComponent<UdonBehaviour>().enabled = false; }
    }
    public void Start1()
    {
        if (ISUSE == false)
        {
            this.GetComponent<UdonBehaviour>().enabled = true;
            ISUSE = true;
            SendCustomEventDelayedSeconds("Start1", 1);
        }
        else
        {
            this.GetComponent<UdonBehaviour>().enabled = false;
        }
    }
    public void SliderChanger()
    {
        if (ISUSE == false) { return; };
        SliderValue = Slider.value;
        AnimatorUSE.SetFloat("Float1", SliderValue);
    }

    private void Update()
    {
        if (AnimatorUSE == null) {  AnimatorUSE = (Animator)Main.GetProgramVariable("AnimatorUSE");  }
        if(LocalPlayer == Owner) { return; }
        var FloatV = AnimatorUSE.GetFloat("Float1");
        FloatV = (SliderValue - FloatV)*Time.deltaTime*Compensation;
        //FOVL += (FOV - FOVL)*Time.deltaTime*Compensation;


        AnimatorUSE.SetFloat("Float1", FloatV);
        Slider.value = FloatV;
    }
    private void OnEnable()
    {
        Owner = Networking.GetOwner(this.gameObject);
        ISUSE = true;
        AnimatorUSE = (Animator)Main.GetProgramVariable("AnimatorUSE");
        if (AnimatorUSE != null) 
        {
            SliderValue = Slider.value;
            AnimatorUSE.SetFloat("Float1", SliderValue);
        }
        
    }
    private void OnDisable()
    {
        ISUSE = false;
    }
}
