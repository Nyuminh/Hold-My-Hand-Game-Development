using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cơ Chế Khóa Cửa Lâu Đài Bằng Chìa Khóa Cổ Thành (Castle Gate Key Unlocker).
    /// Yêu cầu người chơi phải lấy được Chìa Khóa Cổ Thành từ dưới xác thuyền (CastleKeyPickup)
    /// thì mới có thể mở cánh cổng đôi bằng gỗ sồi kiên cố để tiến vào Sân Lâu Đài Hoàng Gia.
    /// </summary>
    public class CastleGateKeyUnlocker : NetworkBehaviour
    {
        public static CastleGateKeyUnlocker Instance { get; private set; }

        [Header("Door References")]
        [SerializeField] private Transform doorLeft;
        [SerializeField] private Transform doorRight;
        [SerializeField] private Transform lockObject;

        [Header("Door Hinges / Transforms")]
        [SerializeField] private float openAngle = 95.0f;
        [SerializeField] private float swingSpeed = 1.8f;
        [SerializeField] private float interactRadius = 4.5f;

        [Header("Audio & Effects")]
        [SerializeField] private AudioSource unlockAudio;
        [SerializeField] private ParticleSystem unlockParticles;

        private bool isUnlocked = false;
        private bool isSwinging = false;
        private bool showPrompt = false;
        private string promptText = "";
        private Color promptColor = Color.yellow;

        private Quaternion doorLeftClosedRot;
        private Quaternion doorRightClosedRot;
        private Quaternion doorLeftOpenRot;
        private Quaternion doorRightOpenRot;

        public bool IsUnlocked => isUnlocked;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            FindReferencesIfNull();
            InitDoorRotations();
        }

        private void FindReferencesIfNull()
        {
            if (doorLeft == null)
            {
                var dl = GameObject.Find("Castle_Gate_Door_L");
                if (dl != null) doorLeft = dl.transform;
            }
            if (doorRight == null)
            {
                var dr = GameObject.Find("Castle_Gate_Door_R");
                if (dr != null) doorRight = dr.transform;
            }
            if (lockObject == null)
            {
                var lk = GameObject.Find("Castle_Gate_Ancient_Lock");
                if (lk != null) lockObject = lk.transform;
            }
        }

        private void InitDoorRotations()
        {
            if (doorLeft != null)
            {
                doorLeftClosedRot = doorLeft.rotation;
                doorLeftOpenRot = doorLeftClosedRot * Quaternion.Euler(0f, 0f, openAngle);
            }
            if (doorRight != null)
            {
                doorRightClosedRot = doorRight.rotation;
                doorRightOpenRot = doorRightClosedRot * Quaternion.Euler(0f, 0f, -openAngle);
            }
        }

        private void Update()
        {
            if (!isUnlocked)
            {
                CheckPlayerDistance();
            }

            if (isSwinging)
            {
                UpdateDoorSwing();
            }
        }

        private void CheckPlayerDistance()
        {
            Vector3 center = transform.position;
            if (doorLeft != null && doorRight != null)
            {
                center = (doorLeft.position + doorRight.position) * 0.5f;
            }

            var cols = Physics.OverlapSphere(center, interactRadius);
            bool playerNear = false;

            foreach (var col in cols)
            {
                if (col.isTrigger) continue;
                Transform p = col.transform;
                while (p != null)
                {
                    string n = p.name.ToLower();
                    if (n.Contains("dad") || n.Contains("son") || n.Contains("child") || n.Contains("player") || n.Contains("character"))
                    {
                        playerNear = true;
                        break;
                    }
                    p = p.parent;
                }
                if (playerNear) break;
            }

            if (playerNear)
            {
                showPrompt = true;
                if (CastleKeyPickup.HasKey)
                {
                    promptText = "🔑 [E] Tra Chìa Khóa Cổ Thành để Mở Cửa Lâu Đài";
                    promptColor = new Color(1f, 0.85f, 0.2f);

                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        UnlockCastleGate();
                    }
                }
                else
                {
                    promptText = "🔒 Cửa khóa chặt! Cần Chìa Khóa Cổ Thành (ở dưới xác thuyền)";
                    promptColor = new Color(1f, 0.35f, 0.35f);
                }
            }
            else
            {
                showPrompt = false;
            }
        }

        public void UnlockCastleGate()
        {
            if (isUnlocked) return;
            isUnlocked = true;
            isSwinging = true;

            Debug.Log("<color=green>🔓 [CastleGateKeyUnlocker] CỔNG LÂU ĐÀI ĐÃ ĐƯỢC MỞ KHÓA THÀNH CÔNG!</color>");

            if (unlockAudio != null) unlockAudio.Play();
            if (unlockParticles != null) unlockParticles.Play();

            // Disable lock visual
            if (lockObject != null)
            {
                lockObject.gameObject.SetActive(false);
            }
        }

        private void UpdateDoorSwing()
        {
            bool leftDone = true;
            bool rightDone = true;

            if (doorLeft != null)
            {
                doorLeft.rotation = Quaternion.Slerp(doorLeft.rotation, doorLeftOpenRot, Time.deltaTime * swingSpeed);
                if (Quaternion.Angle(doorLeft.rotation, doorLeftOpenRot) > 1.0f) leftDone = false;
            }

            if (doorRight != null)
            {
                doorRight.rotation = Quaternion.Slerp(doorRight.rotation, doorRightOpenRot, Time.deltaTime * swingSpeed);
                if (Quaternion.Angle(doorRight.rotation, doorRightOpenRot) > 1.0f) rightDone = false;
            }

            if (leftDone && rightDone)
            {
                isSwinging = false;
            }
        }

        private void OnGUI()
        {
            if (showPrompt && !isUnlocked)
            {
                GUIStyle promptStyle = new GUIStyle(GUI.skin.box);
                promptStyle.fontSize = 17;
                promptStyle.fontStyle = FontStyle.Bold;
                promptStyle.normal.textColor = promptColor;
                promptStyle.alignment = TextAnchor.MiddleCenter;

                float w = 560f;
                float h = 52f;
                float x = (Screen.width - w) * 0.5f;
                float y = Screen.height - 180f;

                GUI.Box(new Rect(x, y, w, h), promptText, promptStyle);
            }
        }
    }
}
