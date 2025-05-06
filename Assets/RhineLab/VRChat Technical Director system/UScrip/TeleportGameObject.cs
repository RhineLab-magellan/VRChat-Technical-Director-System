using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using System;

namespace cdse_presets
{
    public class TeleportGameObject : UdonSharpBehaviour
    {
        [Header("这是一个对象传送脚本")]
        [Header("目标对象")]
        public Transform[] targetGameObject;
        [Header("目标位置")]
        public Transform[] targetPosition;
        [Header("按钮")]
        public Button buttonTeleport;
        private int currentnumber = 0;

        public void isTrigger()
        {
            foreach (var from in targetGameObject)
            {
                targetGameObject[currentnumber].position = targetPosition[currentnumber].position;
                targetGameObject[currentnumber].rotation = targetPosition[currentnumber].rotation;
                currentnumber++;
            }
            currentnumber = 0;
        }
    }
}