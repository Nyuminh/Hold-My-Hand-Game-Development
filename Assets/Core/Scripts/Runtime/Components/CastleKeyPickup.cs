using System;
using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Chìa Khóa Cổ Thành (Castle Gate Key) đặt dưới xác thuyền (Shipwreck Boat).
    /// Người chơi nhặt chìa khóa này để đem lên mở cổng Lâu Đài.
    /// </summary>
    public class CastleKeyPickup : NetworkBehaviour
    {
        public static CastleKeyPickup Instance { get; private set; }
        public static bool HasKey { get; set; } = false;

        [Header("Visual & Light")]
        [SerializeField] private Light keyLight;
        [SerializeField] private float bobSpeed = 2.5f;
        [SerializeField] private float bobHeight = 0.2f;
        [SerializeField] private float rotateSpeed = 65.0f;

        private Vector3 startPos;
        private bool isPickedUp = false;
        private float bannerTimer = 0f;

        private void Awake()
        {
            Instance = this;
            HasKey = false;
        }

        private void Start()
        {
            startPos = transform.position;
            if (keyLight == null) keyLight = GetComponentInChildren<Light>();
        }

        private void Update()
        {
            if (!isPickedUp)
            {
                // Hover and rotate
                float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                transform.position = new Vector3(startPos.x, newY, startPos.z);
                transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

                CheckPlayerDistance();
            }

            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
            }
        }

        private void CheckPlayerDistance()
        {
            var colliders = Physics.OverlapSphere(transform.position, 2.2f);
            foreach (var col in colliders)
            {
                if (col.isTrigger) continue;
                string colName = col.gameObject.name.ToLower();
                Transform p = col.transform;
                bool isPlayer = false;
                while (p != null)
                {
                    string pName = p.name.ToLower();
                    if (pName.Contains("dad") || pName.Contains("son") || pName.Contains("child") || pName.Contains("player") || pName.Contains("character"))
                    {
                        isPlayer = true;
                        break;
                    }
                    p = p.parent;
                }

                if (isPlayer || col.CompareTag("Player"))
                {
                    CollectKey();
                    break;
                }
            }
        }

        public void CollectKey()
        {
            if (isPickedUp) return;
            isPickedUp = true;
            HasKey = true;
            bannerTimer = 6.0f;

            Debug.Log("<color=gold>🔑 [CastleKeyPickup] ĐÃ THU THẬP CHÌA KHÓA CỔ THÀNH TỪ DƯỚI THUYỀN!</color>");

            if (keyLight != null)
            {
                keyLight.range = 10f;
                keyLight.intensity = 3f;
            }

            // Hide mesh
            var r = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
            if (r != null) r.enabled = false;

            // Notify objective director
            if (PlayScene3ObjectiveDirector.Instance != null)
            {
                Debug.Log("<color=green>[ObjectiveDirector] Đã có chìa khóa lâu đài!</color>");
            }
        }

        private void OnGUI()
        {
            if (bannerTimer > 0f)
            {
                GUIStyle bannerStyle = new GUIStyle(GUI.skin.box);
                bannerStyle.fontSize = 18;
                bannerStyle.fontStyle = FontStyle.Bold;
                bannerStyle.alignment = TextAnchor.MiddleCenter;
                bannerStyle.normal.textColor = new Color(1f, 0.88f, 0.2f);

                float width = 520f;
                float height = 55f;
                float x = (Screen.width - width) * 0.5f;
                float y = 90f;

                GUI.backgroundColor = new Color(0.1f, 0.12f, 0.16f, 0.92f);
                GUI.Box(new Rect(x, y, width, height), "🔑 ĐÃ THU THẬP CHÌA KHÓA CỔ THÀNH!\n(Hãy mang chìa khóa lên đỉnh tháp canh để mở cổng Lâu Đài)", bannerStyle);
            }
        }
    }
}
