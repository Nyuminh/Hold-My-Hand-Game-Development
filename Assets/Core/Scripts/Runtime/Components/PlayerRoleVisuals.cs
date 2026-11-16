using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

public class PlayerRoleVisuals : NetworkBehaviour
{
    [Header("Visual Models")]
    [Tooltip("Kéo GameObject chứa model Dad vào đây")]
    public GameObject dadModel;
    
    [Tooltip("Kéo GameObject chứa model Child vào đây")]
    public GameObject childModel;

    // Danh sách toàn bộ người chơi đang online trong session (tiết kiệm chi phí tìm kiếm)
    public static readonly System.Collections.Generic.List<PlayerRoleVisuals> AllPlayers = new System.Collections.Generic.List<PlayerRoleVisuals>();

    // Biến mạng lưu role của player này (True = Dad, False = Child)
    private NetworkVariable<bool> m_IsDad = new NetworkVariable<bool>(
        true, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner
    );

    /// <summary>
    /// Trả về true nếu nhân vật này là 🧔 Ba (Dad), false nếu là 👶 Con (Child).
    /// </summary>
    public bool IsDad => m_IsDad.Value;

    private void Awake()
    {
        EnsureCoopAbilities();
    }

    private void Reset()
    {
        EnsureCoopAbilities();
    }

    public void EnsureCoopAbilities()
    {
        if (GetComponent<CoopHoldHandAbility>() == null)
        {
            var handAbility = gameObject.AddComponent<CoopHoldHandAbility>();
            if (TryGetComponent<CoreMovement>(out var motor))
            {
                motor.AddAbility(handAbility);
            }
        }

        if (GetComponent<CoopBoostThrowAbility>() == null)
        {
            gameObject.AddComponent<CoopBoostThrowAbility>();
        }

        if (GetComponent<CoopHoldHandAuraVisual>() == null)
        {
            gameObject.AddComponent<CoopHoldHandAuraVisual>();
        }

        if (GetComponent<ChildFearHesitation>() == null)
        {
            gameObject.AddComponent<ChildFearHesitation>();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        EnsureCoopAbilities();

        if (!AllPlayers.Contains(this))
        {
            AllPlayers.Add(this);
        }

        if (IsOwner)
        {
            // Tự quyết định Role của chính mình dựa trên Lobby
            m_IsDad.Value = Lobby3DManager.AmIDad;
        }

        // Lắng nghe sự kiện đổi role (nếu có)
        m_IsDad.OnValueChanged += OnRoleChanged;
        
        // Cập nhật giao diện ngay lập tức
        UpdateVisuals(m_IsDad.Value);
    }

    public override void OnNetworkDespawn()
    {
        AllPlayers.Remove(this);
        m_IsDad.OnValueChanged -= OnRoleChanged;
        base.OnNetworkDespawn();
    }

    private void OnRoleChanged(bool previous, bool current)
    {
        UpdateVisuals(current);
    }

    private void UpdateVisuals(bool isDadActive)
    {
        if (dadModel == this.gameObject || childModel == this.gameObject)
        {
            Debug.LogError("[LỖI] Bạn đã kéo NHẦM cả gốc nhân vật (Root) vào ô dadModel hoặc childModel! Bạn chỉ được phép kéo cái MODEL (Mesh/Hình ảnh) nằm bên trong (làm con) của nhân vật vào thôi!");
            return;
        }

        if (dadModel != null) dadModel.SetActive(isDadActive);
        if (childModel != null) childModel.SetActive(!isDadActive);
    }
}
