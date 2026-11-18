using System;
using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cơ chế Phối Hợp Sân Lâu Đài Hoàng Gia (Grand Castle Courtyard Coop Mechanism).
    /// Ba và Con cùng bước lên 2 Bệ Cổ Ngữ (Father Sun Rune & Child Moon Rune)
    /// để kích hoạt năng lượng cổ xưa, mở Cổng Sập Hoàng Gia dẫn sang Cầu Khinh Khí Cầu Zephyr.
    /// </summary>
    public class CastleCourtyardCoopController : NetworkBehaviour
    {
        private static CastleCourtyardCoopController instance;
        public static CastleCourtyardCoopController Instance => instance;

        [Header("Rune Pressure Plates")]
        [SerializeField] private Transform fatherPlate;
        [SerializeField] private Transform childPlate;
        [SerializeField] private Transform centerDais;
        [SerializeField] private Light fatherLight;
        [SerializeField] private Light childLight;
        [SerializeField] private Light daisAuraLight;

        [Header("Portcullis Gate")]
        [SerializeField] private Transform portcullisGate;
        [SerializeField] private float openHeight = 5.2f;
        [SerializeField] private float openSpeed = 2.0f;

        [Header("State")]
        private bool isFatherOnPlate = false;
        private bool isChildOnPlate = false;
        private bool isGateUnlocked = false;
        private Vector3 gateInitialPos;
        private Vector3 gateTargetPos;

        public bool IsGateUnlocked => isGateUnlocked;

        private void Awake()
        {
            if (instance == null) instance = this;
        }

        private void Start()
        {
            FindReferencesIfNull();

            if (portcullisGate != null)
            {
                gateInitialPos = portcullisGate.position;
                gateTargetPos = gateInitialPos + Vector3.up * openHeight;
            }

            if (fatherLight != null) fatherLight.intensity = 0.5f;
            if (childLight != null) childLight.intensity = 0.5f;
            if (daisAuraLight != null) daisAuraLight.intensity = 0f;
        }

        private void FindReferencesIfNull()
        {
            if (fatherPlate == null)
            {
                var go = GameObject.Find("Castle_Coop_Plate_Father");
                if (go != null) fatherPlate = go.transform;
            }

            if (childPlate == null)
            {
                var go = GameObject.Find("Castle_Coop_Plate_Child");
                if (go != null) childPlate = go.transform;
            }

            if (centerDais == null)
            {
                var go = GameObject.Find("Castle_Coop_CenterDais");
                if (go != null) centerDais = go.transform;
            }

            if (portcullisGate == null)
            {
                var go = GameObject.Find("Castle_RearGate_Portcullis");
                if (go != null) portcullisGate = go.transform;
            }

            if (fatherLight == null && fatherPlate != null)
            {
                fatherLight = fatherPlate.GetComponentInChildren<Light>();
            }

            if (childLight == null && childPlate != null)
            {
                childLight = childPlate.GetComponentInChildren<Light>();
            }

            if (daisAuraLight == null && centerDais != null)
            {
                daisAuraLight = centerDais.GetComponentInChildren<Light>();
            }
        }

        private void Update()
        {
            if (!isGateUnlocked)
            {
                CheckPlateTriggers();

                if (isFatherOnPlate && isChildOnPlate)
                {
                    UnlockGate();
                }
            }
            else
            {
                // Smoothly raise the portcullis gate
                if (portcullisGate != null && Vector3.Distance(portcullisGate.position, gateTargetPos) > 0.02f)
                {
                    portcullisGate.position = Vector3.MoveTowards(portcullisGate.position, gateTargetPos, openSpeed * Time.deltaTime);
                }
            }
        }

        private void CheckPlateTriggers()
        {
            // Detect players on plates via OverlapSphere
            float checkRadius = 1.3f;

            if (fatherPlate != null)
            {
                bool wasOn = isFatherOnPlate;
                isFatherOnPlate = CheckPlayerNear(fatherPlate.position, checkRadius, true);
                if (fatherLight != null)
                {
                    fatherLight.intensity = Mathf.Lerp(fatherLight.intensity, isFatherOnPlate ? 3.5f : 0.6f, Time.deltaTime * 5f);
                }
            }

            if (childPlate != null)
            {
                bool wasOn = isChildOnPlate;
                isChildOnPlate = CheckPlayerNear(childPlate.position, checkRadius, false);
                if (childLight != null)
                {
                    childLight.intensity = Mathf.Lerp(childLight.intensity, isChildOnPlate ? 3.5f : 0.6f, Time.deltaTime * 5f);
                }
            }
        }

        private bool CheckPlayerNear(Vector3 center, float radius, bool isFatherCheck)
        {
            var colliders = Physics.OverlapSphere(center, radius);
            foreach (var col in colliders)
            {
                if (col.isTrigger) continue;
                string colName = col.gameObject.name.ToLower();
                Transform p = col.transform;
                while (p != null)
                {
                    string pName = p.name.ToLower();
                    if (isFatherCheck)
                    {
                        if (pName.Contains("dad") || pName.Contains("father") || pName.Contains("player_1") || pName.Contains("character_dad"))
                            return true;
                    }
                    else
                    {
                        if (pName.Contains("son") || pName.Contains("child") || pName.Contains("player_2") || pName.Contains("the child"))
                            return true;
                    }
                    p = p.parent;
                }

                // If general player tag and offline/single test
                if (col.CompareTag("Player") || colName.Contains("player") || colName.Contains("character"))
                {
                    return true;
                }
            }
            return false;
        }

        private void UnlockGate()
        {
            isGateUnlocked = true;
            Debug.Log("<color=cyan>[CastleCourtyard] ✨ CẢ HAI CHA CON ĐÃ KÍCH HOẠT ĐỒNG THỜI 2 BIA ĐÁ HOÀNG GIA! CỔNG SẬP ĐANG MỞ!</color>");

            if (daisAuraLight != null)
            {
                daisAuraLight.intensity = 5.0f;
            }

            if (PlayScene3ObjectiveDirector.Instance != null)
            {
                PlayScene3ObjectiveDirector.Instance.SetCastleCourtyardCompleted();
            }
        }
    }
}
